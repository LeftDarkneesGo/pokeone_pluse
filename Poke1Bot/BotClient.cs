﻿using Poke1Bot.Modules;
using Poke1Bot.Scripting;
using Poke1Protocol;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Poke1Bot
{
    public class BotClient
    {
        public enum State
        {
            Stopped,
            Started,
            Paused
        };

        public AccountManager AccountManager { get; }
        public GameClient Game { get; private set; }
        public BattleAI AI { get; private set; }
        public BaseScript Script { get; private set; }
        public Random Rand { get; } = new Random();
        public Account Account { get; set; }
        public State Running { get; private set; }
        public MovementResynchronizer MovementResynchronizer { get; }
        public AutoReconnector AutoReconnector { get; }
        public PokemonEvolver PokemonEvolver { get; }
        public MoveTeacher MoveTeacher { get; }
        public AutoLootBoxOpener AutoLootBoxOpener { get; }
        public UserSettings Settings { get; }


        public event Action<State> StateChanged;
        public event Action<string> MessageLogged;
        public event Action<string, Brush> ColorMessageLogged;
        public event Action ClientChanged;
        public event Action ConnectionOpened;
        public event Action ConnectionClosed;


        private ProtocolTimeout _actionTimeout = new ProtocolTimeout();

        private bool _loginRequested;

        public BotClient()
        {
            AccountManager = new AccountManager("Accounts");
            PokemonEvolver = new PokemonEvolver(this);
            MovementResynchronizer = new MovementResynchronizer(this);
            MoveTeacher = new MoveTeacher(this);
            AutoReconnector = new AutoReconnector(this);
            AutoLootBoxOpener = new AutoLootBoxOpener(this);
            Settings = new UserSettings();
        }

        public void LogMessage(string message)
        {
            MessageLogged?.Invoke(message);
        }

        public void LogMessage(string message, Brush color)
        {
            ColorMessageLogged?.Invoke(message, color);
        }

        public void Login(Account account)
        {
            Account = account;
            _loginRequested = true;
        }
        private void LoginUpdate()
        {
            GameClient client;
            if (Account.Socks.Version != SocksVersion.None)
            {
                // TODO: Clean this code.
                client = new GameClient(new GameConnection((int)Account.Socks.Version, Account.Socks.Host, Account.Socks.Port, Account.Socks.Username, Account.Socks.Password),
                    new MapConnection((int)Account.Socks.Version, Account.Socks.Host, Account.Socks.Port, Account.Socks.Username, Account.Socks.Password));
            }
            else
            {
                client = new GameClient(new GameConnection(), new MapConnection());
            }
            SetClient(client);
            client.Open();
        }

        public void Logout(bool allowAutoReconnect)
        {
            if (!allowAutoReconnect)
            {
                AutoReconnector.IsEnabled = false;
            }
            Game.Close();
        }

        public void Update()
        {

            if (_loginRequested)
            {
                LoginUpdate();
                _loginRequested = false;
                return;
            }

            AutoReconnector.Update();
            AutoLootBoxOpener.Update();

            if (Running != State.Started)
            {
                return;
            }

            if (PokemonEvolver.Update()) return;
            if (MoveTeacher.Update()) return;

            if (Game.IsMapLoaded && Game.AreNpcReceived && Game.IsInactive)
            {
                ExecuteNextAction();
            }
        }

        public void Start()
        {
            if (Game != null && Script != null && Running == State.Stopped)
            {
                _actionTimeout.Set();
                Running = State.Started;
                StateChanged?.Invoke(Running);
                Script.Start();
            }
        }

        public void Pause()
        {
            if (Game != null && Script != null && Running != State.Stopped)
            {
                if (Running == State.Started)
                {
                    Running = State.Paused;
                    StateChanged?.Invoke(Running);
                    Script.Pause();
                }
                else
                {
                    Running = State.Started;
                    StateChanged?.Invoke(Running);
                    Script.Resume();
                }
            }
        }

        public void Stop()
        {
            if (Game != null)
                Game.ClearPath();

            if (Running != State.Stopped)
            {
                Running = State.Stopped;
                StateChanged?.Invoke(Running);
                if (Script != null)
                {
                    Script.Stop();
                }
            }
        }

        public async Task LoadScript(string filename)
        {
            using (var reader = new StreamReader(filename))
            {
                var input = await reader.ReadToEndAsync();


                List<string> libs = new List<string>();
                if (Directory.Exists("Libs"))
                {
                    string[] files = Directory.GetFiles("Libs");
                    foreach (string file in files)
                    {
                        if (file.ToUpperInvariant().EndsWith(".LUA"))
                        {
                            using (var streaReader = new StreamReader(file))
                            {
                                libs.Add(await streaReader.ReadToEndAsync());
                            }
                        }
                    }
                }

                BaseScript script = new LuaScript(this, Path.GetFullPath(filename), input, libs);

                Stop();
                Script = script;
            }

            try
            {
                Script.ScriptMessage += Script_ScriptMessage;
                Script.Initialize();
            }
            catch (Exception)
            {
                Script = null;
                throw;
            }
        }

        public bool TalkToNpc(Npc target)
        {
            bool canInteract = Game.Map.CanInteract(Game.PlayerX, Game.PlayerY, target.PositionX, target.PositionY);
            if (canInteract)
            {
                Game.TalkToNpc(target);
                return true;
            }
            return MoveToCell(target.PositionX, target.PositionY, 1);
        }

        public bool MoveToCell(int x, int y, int requiredDistance = 0)
        {
            MovementResynchronizer.CheckMovement(x, y);

            Pathfinding path = new Pathfinding(Game);
            bool result;

            if (Game.PlayerX == x && Game.PlayerY == y)
            {
                result = path.MoveToSameCell();
            }
            else
            {
                result = path.MoveTo(x, y, requiredDistance);
            }

            if (result)
            {
                MovementResynchronizer.ApplyMovement(x, y);
            }
            return result;
        }

        public bool MoveToAreaLink(string destinationArea)
        {
            IEnumerable<Tuple<int, int>> nearest = Game.Map.GetNearestAreaLinks(destinationArea, Game.PlayerX, Game.PlayerY);

            if (nearest != null)
            {
                foreach (Tuple<int, int> link in nearest)
                {
                    if (MoveToCell(link.Item1, link.Item2)) return true;
                }
            }
            return false;
        }

        public bool MoveLeftRight(int startX, int startY, int destX, int destY)
        {
            bool result;
            if (startX != destX && startY != destY)
            {
                // ReSharper disable once RedundantAssignment
                return false;
            }
            if (Game.PlayerX == destX && Game.PlayerY == destY)
            {
                result = MoveToCell(startX, startY);
            }
            else if (Game.PlayerX == startX && Game.PlayerY == startY)
            {
                result = MoveToCell(destX, destY);
            }
            else
            {
                result = MoveToCell(startX, startY);
            }
            return result;
        }

        public bool IsAreaLink(int x, int y)
        {
            var pathFinder = new Pathfinding(Game);
            return Game != null && Game.IsMapLoaded && Game.Map.IsAreaLink(x, y);
        }

        public void SetClient(GameClient client)
        {
            Game = client;
            AI = null;
            Stop();

            if (client != null)
            {
                AI = new BattleAI(client);
                client.ConnectionOpened += Client_ConnectionOpened;
                client.ConnectionFailed += Client_ConnectionFailed;
                client.ConnectionClosed += Client_ConnectionClosed;
                client.BattleMessage += Client_BattleMessage;
                client.SystemMessage += Client_SystemMessage;
                client.DialogOpened += Client_DialogOpened;
                client.TeleportationOccuring += Client_TeleportationOccuring;
                client.LogMessage += LogMessage;
            }
            ClientChanged?.Invoke();
        }

        private void ExecuteNextAction()
        {
            try
            {
                bool executed = Script.ExecuteNextAction();
                if (!executed && Running != State.Stopped && !_actionTimeout.Update())
                {
                    LogMessage("No action executed: stopping the bot.");
                    Stop();
                }
                else if (executed)
                {
                    _actionTimeout.Set();
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                LogMessage("Error during the script execution: " + ex);
#else
                LogMessage("Error during the script execution: " + ex.Message);
#endif
                Stop();
            }
        }

        private void Client_ConnectionOpened()
        {
            ConnectionOpened?.Invoke();
            Game.SendAuthentication(Account.Name, Account.Password);
        }
        private void Client_ConnectionClosed(Exception ex)
        {
            if (ex != null)
            {
#if DEBUG
                LogMessage("Disconnected from the server: " + ex, Brushes.OrangeRed);
#else
                LogMessage("Disconnected from the server: " + ex.Message, Brushes.OrangeRed);
#endif
            }
            else
            {
                LogMessage("Disconnected from the server.", Brushes.OrangeRed);
            }
            ConnectionClosed?.Invoke();
            SetClient(null);
        }
        private void Client_ConnectionFailed(Exception ex)
        {
            if (ex != null)
            {
#if DEBUG
                LogMessage("Could not connect to the server: " + ex, Brushes.OrangeRed);
#else
                LogMessage("Could not connect to the server: " + ex.Message, Brushes.OrangeRed);
#endif
            }
            else
            {
                LogMessage("Could not connect to the server.", Brushes.OrangeRed);
            }
            ConnectionClosed?.Invoke();
            SetClient(null);
        }
        private void Client_SystemMessage(string message)
        {
            if (Script != null)
                Script.OnSystemMessage(message);
        }
        private void Client_BattleMessage(string message)
        {
            if (Script != null)
                Script.OnBattleMessage(message);
        }

        private void Client_DialogOpened(string message)
        {
            if (Script != null)
                Script.OnDialogMessage(message);
        }

        private void Client_TeleportationOccuring(string map, int x, int y)
        {
            string message = "Position updated: [" + map + "] (" + x + ", " + y + ")";
            if (Game.Map == null || Game.IsTeleporting)
            {
                message += " [OK]";
            }
            else if (Game.MapName != map)
            {
                message += " [WARNING, different map] /!\\";
            }
            else
            {
                int distance = GameClient.DistanceBetween(x, y, Game.PlayerX, Game.PlayerY);
                if (distance < 8)
                {
                    message += " [OK, lag, distance=" + distance + "]";
                }
                else
                {
                    message += " [WARNING, distance=" + distance + "] /!\\";
                }
            }

            if (message.Contains("[OK]"))
            {
                LogMessage(message, Brushes.LimeGreen);
            }
            if (message.Contains("WARNING"))
            {
                LogMessage(message, Brushes.OrangeRed);
            }

            MovementResynchronizer.Reset();
        }

        private void Script_ScriptMessage(string message)
        {
            LogMessage(message);
        }
    }
}

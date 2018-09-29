using PSXAPI;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using PSXAPI.Response;
using System.Text.RegularExpressions;

namespace Poke1Protocol
{
    public class GameClient
    {
        #region From PSXAPI.DLL
        private TimeSpan pingUpdateTime = TimeSpan.FromSeconds(5.0);
        private readonly Timer timer;
        private DateTime lastPingResponseUtc;
        private bool receivedPing;
        private volatile int ping;
        private bool disposedValue;
        #endregion

        private ProtocolTimeout _movementTimeout = new ProtocolTimeout();
        private ProtocolTimeout _teleportationTimeout = new ProtocolTimeout();
        private ProtocolTimeout _battleTimeout = new ProtocolTimeout();
        private ProtocolTimeout _loadingTimeout = new ProtocolTimeout();
        private ProtocolTimeout _swapTimeout = new ProtocolTimeout();
        private ProtocolTimeout _mountingTimeout = new ProtocolTimeout();
        private ProtocolTimeout _lootBoxTimeout = new ProtocolTimeout();
        private ProtocolTimeout _dialogTimeout = new ProtocolTimeout();
        private ProtocolTimeout _itemUseTimeout = new ProtocolTimeout();

        public string PlayerName { get; private set; }

        private string mapChatChannel = "";
        private string _partyChannel = "";
        private string _guildChannel = "";
        public string MapName { get; private set; } = "";
        public string AreaName { get; private set; } = "";

        public PSXAPI.Response.Level Level { get; private set; }

        public Battle ActiveBattle { get; private set; }


        private int _lastTime;
        private bool _isLoggedIn = false;
        public bool IsConnected { get; private set; }

        public bool IsMapLoaded =>
            Map != null;
        public bool IsTeleporting =>
            _teleportationTimeout.IsActive;
        public bool IsInactive =>
                    _movements.Count == 0
                    && !_movementTimeout.IsActive
                    && !_battleTimeout.IsActive
                    && !_loadingTimeout.IsActive
                    && !_mountingTimeout.IsActive
                    && !_teleportationTimeout.IsActive
                    && !_swapTimeout.IsActive
                    && !_dialogTimeout.IsActive
                    && !_lootBoxTimeout.IsActive
                    && !_itemUseTimeout.IsActive;
        //&& !_dialogTimeout.IsActive
        //&& !_itemUseTimeout.IsActive
        //&& !_fishingTimeout.IsActive
        //&& !_refreshingPCBox.IsActive
        //&& !_npcBattleTimeout.IsActive
        //&& !_moveRelearnerTimeout.IsActive

        public const string Version = "0.57";

        private GameConnection _connection;

        public LootboxHandler RecievedLootBoxes;

        public event Action ConnectionOpened;
        public event Action AreaUpdated;
        public event Action<Exception> ConnectionFailed;
        public event Action<Exception> ConnectionClosed;
        public event Action<PSXAPI.Response.LoginError> AuthenticationFailed;
        public event Action LoggedIn;
        public event Action<string, string> GameTimeUpdated;
        public event Action<string, int, int> PositionUpdated;
        public event Action<string, int, int> TeleportationOccuring;
        public event Action InventoryUpdated;
        public event Action PokemonsUpdated;
        public event Action<string> MapLoaded;
        public event Action<string> SystemMessage;
        public event Action<string> LootBoxMessage;
        public event Action<LootboxHandler> RecievedLootBox;
        public event Action<string> LogMessage;
        public event Action BattleStarted;
        public event Action<string> BattleMessage;
        public event Action BattleEnded;
        public event Action<List<PokedexPokemon>> PokedexUpdated;
        public event Action<PlayerInfos> PlayerUpdated;
        public event Action<PlayerInfos> PlayerAdded;
        public event Action<PlayerInfos> PlayerRemoved;
        public event Action<Level, Level> LevelChanged;
        public event Action RefreshChannelList;
        public event Action<string, string, string> ChannelMessage;
        public event Action<string, string, string> PrivateMessage;
        public event Action<string, string, string> LeavePrivateMessage;
        public event Action<string> DialogOpened;
        public event Action<PSXAPI.Response.Payload.LootboxRoll[], PSXAPI.Response.LootboxType> LootBoxOpened;
        public event Action<Guid> Evolving;
        public event Action<PSXAPI.Response.Payload.PokemonMoveID, int, Guid> LearningMove;

        public string[] DialogContent { get; private set; }
        private Queue<object> _dialogResponses = new Queue<object>();
        public bool IsScriptActive { get; private set; }
        public string GameTime { get; private set; }
        public string Weather { get; private set; }
        public int PlayerX { get; private set; }
        public int PlayerY { get; private set; }
        public List<Pokemon> Team { get; private set; }
        public List<PlayerEffect> Effects { get; private set; }
        public List<InventoryItem> Items { get; private set; }
        public Dictionary<string, ChatChannel> Channels { get; private set; }
        public List<string> Conversations { get; }
        public Dictionary<string, PlayerInfos> Players { get; }
        private Dictionary<string, PlayerInfos> _removedPlayers { get; }
        private DateTime _updatePlayers;
        public Random Rand { get; }

        private MapClient _mapClient;

        public Map Map { get; private set; }
        public int Money { get; private set; }
        public int Gold { get; private set; }

        private List<Direction> _movements;
        private Direction? _slidingDirection;
        private bool _surfAfterMovement;

        public bool IsInBattle { get; private set; }
        public bool IsOnGround { get; private set; }
        public bool IsSurfing { get; private set; }
        public bool IsBiking { get; private set; }
        public bool IsLoggedIn => _isLoggedIn && IsConnected && _connection.IsConnected;
        public bool CanUseCut { get; private set; }
        public bool CanUseSmashRock { get; private set; }
        public int PokedexOwned { get; private set; }
        public int PokedexSeen { get; private set; }
        public bool AreNpcReceived { get; private set; }

        private ScriptRequestType _currentScriptType { get; set; }
        private Script _currentScript { get; set; }
        public List<PokedexPokemon> PokedexPokemons { get; private set; }
        private List<Script> Scripts { get; }
        private List<Script> _cachedScripts { get; }
        private MapUsers _cachedNerbyUsers { get; set; }
        public GameClient(GameConnection connection, MapConnection mapConnection)
        {
            _mapClient = new MapClient(mapConnection, this);
            _mapClient.ConnectionOpened += MapClient_ConnectionOpened;
            _mapClient.ConnectionClosed += MapClient_ConnectionClosed;
            _mapClient.ConnectionFailed += MapClient_ConnectionFailed;
            _mapClient.MapLoaded += MapClient_MapLoaded;

            _connection = connection;
            _connection.PacketReceived += OnPacketReceived;
            _connection.Connected += OnConnectionOpened;
            _connection.Disconnected += OnConnectionClosed;

            Rand = new Random();
            lastPingResponseUtc = DateTime.UtcNow;
            timer = new Timer(new TimerCallback(Timer), null, PingUpdateTime, PingUpdateTime);
            disposedValue = false;
            _movements = new List<Direction>();
            Team = new List<Pokemon>();
            Items = new List<InventoryItem>();
            Channels = new Dictionary<string, ChatChannel>();
            Conversations = new List<string>();
            Players = new Dictionary<string, PlayerInfos>();
            _removedPlayers = new Dictionary<string, PlayerInfos>();
            PokedexPokemons = new List<PokedexPokemon>();
            _cachedScripts = new List<Script>();
            Scripts = new List<Script>();
            Effects = new List<PlayerEffect>();
        }

        private void AddDefaultChannels()
        {
            Channels.Add("General", new ChatChannel("default", "General"));
            Channels.Add("System", new ChatChannel("default", "General"));
            Channels.Add("Map", new ChatChannel("default", "Map"));
            Channels.Add("Party", new ChatChannel("default", "Party"));
            Channels.Add("Battle", new ChatChannel("default", "Battle"));
            Channels.Add("Guild", new ChatChannel("default", "Guild"));
        }


        public void Move(Direction direction)
        {
            _movements.Add(direction);
        }

        public void PushDialogAnswer(int index)
        {
            _dialogResponses.Enqueue(index);
        }

        public void PushDialogAnswer(string text)
        {
            _dialogResponses.Enqueue(text);
        }

        public void Open()
        {
            _mapClient.Open();
        }

        public void Close(Exception error = null)
        {
            _connection.Close(error);
        }

        public void ClearPath() => _movements.Clear();

        public void Update()
        {
            _mapClient.Update();
            _connection.Update();

            _swapTimeout.Update();
            _movementTimeout.Update();
            _teleportationTimeout.Update();
            _battleTimeout.Update();
            _dialogTimeout.Update();
            _loadingTimeout.Update();
            _lootBoxTimeout.Update();
            _itemUseTimeout.Update();
            _mountingTimeout.Update();

            UpdateMovement();
            UpdateScript();
            UpdatePlayers();
            if (RecievedLootBoxes != null)
                RecievedLootBoxes.UpdateFreeLootBox();
        }

        private void UpdateMovement()
        {
            if (!IsMapLoaded) return;

            if (!_movementTimeout.IsActive && _movements.Count > 0)
            {

                Direction direction = _movements[0];
                _movements.RemoveAt(0);
                if (ApplyMovement(direction))
                {
                    SendMovement(direction.ToMoveActions());
                    _movementTimeout.Set(IsBiking ? 125 : 250);
                    if (Map.HasLink(PlayerX, PlayerY))
                    {
                        _teleportationTimeout.Set();
                    }
                    _lootBoxTimeout.Cancel();
                }
                if (_movements.Count == 0 && _surfAfterMovement)
                {
                    _movementTimeout.Set(Rand.Next(750, 2000));
                }
            }

            if (!_movementTimeout.IsActive && _movements.Count == 0 && _surfAfterMovement)
            {
                _surfAfterMovement = false;
                UseSurf();
            }
        }

        private void UpdatePlayers()
        {
            if (_updatePlayers < DateTime.UtcNow)
            {
                foreach (string playerName in Players.Keys.ToArray())
                {
                    if (Players[playerName].IsExpired())
                    {
                        var player = Players[playerName];

                        PlayerRemoved?.Invoke(player);

                        if (_removedPlayers.ContainsKey(player.Name))
                            _removedPlayers[player.Name] = player;
                        else
                            _removedPlayers.Add(player.Name, player);

                        Players.Remove(playerName);
                    }
                }
                _updatePlayers = DateTime.UtcNow.AddSeconds(5);
            }
        }

        private void UpdateScript()
        {
            if (IsScriptActive && !_dialogTimeout.IsActive && Scripts.Count > 0)
            {
                var script = Scripts[0];
                Scripts.RemoveAt(0);

                DialogContent = script.Data;

                var type = script.Type;
                switch (type)
                {
                    case ScriptRequestType.Choice:
                        if (_dialogResponses.Count <= 0)
                        {
                            SendScriptResponse(script.ScriptID, "0");
                        }
                        else
                        {
                            SendScriptResponse(script.ScriptID, GetNextDialogResponse().ToString());
                        }
                        _dialogTimeout.Set();
                        break;
                    case ScriptRequestType.WalkNpc:
                        SendScriptResponse(script.ScriptID, "");
                        _dialogTimeout.Set();
                        break;
                    case ScriptRequestType.WalkUser:
                        SendScriptResponse(script.ScriptID, "");
                        _dialogTimeout.Set();
                        break;
                    case ScriptRequestType.WaitForInput:
                        SendScriptResponse(script.ScriptID, "");
                        _dialogTimeout.Set();
                        break;
                    case ScriptRequestType.Unfreeze:
                        if (script.Text is null)
                        {
                            _dialogResponses.Clear();
                        }
                        break;
                }


            }
        }

        private int GetNextDialogResponse()
        {
            if (_dialogResponses.Count > 0)
            {
                object response = _dialogResponses.Dequeue();
                if (response is int)
                {
                    return (int)response;
                }
                else if (response is string)
                {
                    string text = ((string)response).ToUpperInvariant();
                    for (int i = 1; i < DialogContent.Length; ++i)
                    {
                        if (DialogContent[i].ToUpperInvariant().Equals(text))
                        {
                            return i;
                        }
                    }
                }
            }
            return 1;
        }

        private bool ApplyMovement(Direction direction)
        {
            int destinationX = PlayerX;
            int destinationY = PlayerY;
            bool isOnGround = IsOnGround;
            bool isSurfing = IsSurfing;

            direction.ApplyToCoordinates(ref destinationX, ref destinationY);
            Map.MoveResult result = Map.CanMove(direction, destinationX, destinationY, isOnGround, isSurfing, CanUseCut, CanUseSmashRock);
            if (Map.ApplyMovement(direction, result, ref destinationX, ref destinationY, ref isOnGround, ref isSurfing))
            {
                PlayerX = destinationX;
                PlayerY = destinationY;
                IsOnGround = isOnGround;
                IsSurfing = isSurfing;
                CheckArea();
                if (result == Map.MoveResult.Icing)
                {
                    _movements.Insert(0, direction);
                }

                if (result == Map.MoveResult.Sliding)
                {
                    int slider = Map.GetSlider(destinationX, destinationY);
                    if (slider != -1)
                    {
                        _slidingDirection = Map.SliderToDirection(slider);
                    }
                }

                if (_slidingDirection != null)
                {
                    _movements.Insert(0, direction);
                }

                return true;
            }
            return false;
        }

        private void MapClient_ConnectionOpened()
        {
#if DEBUG
            Console.WriteLine("[+++] Connecting to the game server");
#endif
            _connection.Connect();
        }

        private void MapClient_ConnectionFailed(Exception ex)
        {
            ConnectionFailed?.Invoke(ex);
        }

        private void MapClient_ConnectionClosed(Exception ex)
        {
            Close(ex);
        }

        private void OnPacketReceived(string packet)
        {
#if DEBUG
            Console.WriteLine("Receiving Packet [<]: " + packet);
#endif
            ProcessPacket(packet);
        }

        private void OnConnectionOpened()
        {
            IsConnected = true;
#if DEBUG
            Console.WriteLine("[+++] Connection opened");
#endif
            lastPingResponseUtc = DateTime.UtcNow;
            receivedPing = true;
            ConnectionOpened?.Invoke();
        }

        private void OnConnectionClosed(Exception ex)
        {
            _isLoggedIn = false;
            _mapClient.Close();
            if (!IsConnected)
            {
#if DEBUG
                Console.WriteLine("[---] Connection failed");
#endif
                ConnectionFailed?.Invoke(ex);
            }
            else
            {
                IsConnected = false;
#if DEBUG
                Console.WriteLine("[---] Connection closed");
#endif
                ConnectionClosed?.Invoke(ex);
            }
            if (!disposedValue)
            {
                timer.Dispose();
                disposedValue = true;
            }
        }

        private void Timer(object obj)
        {
            if (IsConnected && receivedPing)
            {
                receivedPing = false;
                SendProto(new PSXAPI.Request.Ping
                {
                    DateTimeUtc = DateTime.UtcNow
                });
            }
        }

        private void SendSwapPokemons(int poke1, int poke2)
        {
            PSXAPI.Request.Reorder packet = new PSXAPI.Request.Reorder
            {
                Pokemon = Team[poke1 - 1].PokemonData.Pokemon.UniqueID,
                Position = poke2
            };
            SendProto(packet);
        }

        private void SendScriptResponse(Guid id, string response)
        {
            SendProto(new PSXAPI.Request.Script
            {
                Response = response,
                ScriptID = id
            });
        }

        private void SendTalkToNpc(Guid npcId)
        {
            SendProto(new PSXAPI.Request.Talk
            {
                NpcID = npcId
            });
        }

        public void SendProto(PSXAPI.IProto proto)
        {
            var array = Proto.Serialize(proto);
            if (array == null)
            {
                return;
            }
            string packet = Convert.ToBase64String(array);
            packet = proto._Name + " " + packet;
            SendPacket(packet);
        }
        public void SendPacket(string packet)
        {
#if DEBUG
            Console.WriteLine("Sending Packet [>]: " + packet);
#endif
            _connection.Send(packet);
        }

        public void SendMessage(string channel, string message)
        {
            if (channel == "Map")
                channel = mapChatChannel;
            if (channel == "Party")
                channel = _partyChannel;
            if (channel == "Guild")
                channel = _guildChannel;
            List<Guid> pokeList = new List<Guid>();
            SendProto(new PSXAPI.Request.ChatMessage
            {
                Channel = channel,
                Message = message,
                Pokemon = pokeList.ToArray()
            });
        }

        public void CloseChannel(string channel)
        {
            if (Channels.Any(c => c.Key == channel))
            {
                SendProto(new PSXAPI.Request.ChatJoin
                {
                    Channel = channel,
                    Action = PSXAPI.Request.ChatJoinAction.Leave
                });
            }
        }

        public void CloseConversation(string pmName)
        {
            if (Conversations.Contains(pmName))
            {
                SendProto(new PSXAPI.Request.Message
                {
                    Event = PSXAPI.Request.MessageEvent.Closed,
                    Name = pmName,
                    Text = ""
                });
            }
        }

        private void SendMovement(PSXAPI.Request.MoveAction[] actions)
        {
            var movePacket = new PSXAPI.Request.Move
            {
                Actions = actions,
                Map = MapName,
                X = PlayerX,
                Y = PlayerY
            };
            SendProto(movePacket);
        }

        public bool OpenLootBox(PSXAPI.Request.LootboxType type)
        {
            if (RecievedLootBoxes != null)
            {
                if (RecievedLootBoxes.TotalLootBoxes > 0)
                {
                    SendOpenLootBox(type);
                    _lootBoxTimeout.Set();
                    return true;
                }
            }
            return false;
        }


        public void TalkToNpc(Npc npc)
        {
            SendTalkToNpc(npc.Id);
            _dialogTimeout.Set();
        }

        private void SendCharacterCustomization(int gender, int skin, int hair, int haircolour, int eyes)
        {
            var packet = new PSXAPI.Request.Script
            {
                Response = string.Concat(new string[]
                {
                    gender.ToString(),
                    ",",
                    skin.ToString(),
                    ",",
                    eyes.ToString(),
                    ",",
                    hair.ToString(),
                    ",",
                    haircolour.ToString()
                }),
                ScriptID = _currentScript.ScriptID
            };
            SendProto(packet);
        }

        private void SendOpenLootBox(PSXAPI.Request.LootboxType type)
        {
            SendProto(new PSXAPI.Request.Lootbox
            {
                Action = PSXAPI.Request.LootboxAction.Open,
                Type = type
            });
        }

        private void SendJoinChannel(string channel)
        {
            SendProto(new PSXAPI.Request.ChatJoin
            {
                Channel = channel,
                Action = PSXAPI.Request.ChatJoinAction.Join
            });
        }

        public void SendPrivateMessage(string nickname, string text)
        {
            if (!Conversations.Contains(nickname))
                Conversations.Add(nickname);
            SendProto(new Message
            {
                Event = MessageEvent.Message,
                Name = nickname,
                Text = text
            });
        }

        private void SendAttack(int id, bool megaEvo)
        {
            SendProto(new PSXAPI.Request.BattleBroadcast
            {
                RequestID = ActiveBattle.ResponseID,
                Message = string.Concat(new string[]
                {
                    "1|",
                    PlayerName,
                    "|",
                    ActiveBattle.SelectedOpponent.ToString(),
                    "|",
                    ActiveBattle.Turn.ToString(),
                    "|",
                    ActiveBattle.SelectedPokemonIndex.ToString(),
                    "|",
                    id.ToString()
                })
            });

            SendProto(new PSXAPI.Request.BattleMove
            {
                MoveID = id,
                Target = ActiveBattle.SelectedOpponent,
                Position = ActiveBattle.SelectedPokemonIndex + 1,
                RequestID = ActiveBattle.ResponseID,
                MegaEvo = megaEvo,
                ZMove = false
            });
        }

        private void SendRunFromBattle()
        {
            SendProto(new PSXAPI.Request.BattleRun
            {
                RequestID = ActiveBattle.ResponseID
            });

            SendProto(new PSXAPI.Request.BattleBroadcast
            {
                RequestID = ActiveBattle.ResponseID,
                Message = string.Concat(new string[]
                {
                    "5|",
                    PlayerName,
                    "|0|",
                    ActiveBattle.Turn.ToString(),
                    "|",
                    ActiveBattle.SelectedPokemonIndex.ToString()
                })
            });
        }

        private void SendChangePokemon(int currentPos, int newPos)
        {
            SendProto(new PSXAPI.Request.BattleSwitch
            {
                RequestID = ActiveBattle.ResponseID,
                Position = currentPos,
                NewPosition = newPos
            });
        }

        private void SendUseItemInBattle(int id, int targetId, bool isPokeBall = false, int moveTarget = 0)
        {
            if (!isPokeBall)
            {
                SendProto(new PSXAPI.Request.BattleBroadcast
                {
                    RequestID = ActiveBattle.ResponseID,
                    Message = string.Concat(new string[]
                    {
                        "2|",
                        PlayerName,
                        "|",
                        targetId.ToString(),
                        "|",
                        ActiveBattle.Turn.ToString(),
                        "|",
                        ActiveBattle.SelectedPokemonIndex.ToString()
                    })
                });

                SendProto(new PSXAPI.Request.BattleItem
                {
                    Item = id,
                    RequestID = ActiveBattle.ResponseID,
                    Target = targetId,
                    TargetMove = moveTarget,
                    Position = ActiveBattle.SelectedPokemonIndex + 1
                });
            }
            else
            {
                SendProto(new PSXAPI.Request.BattleBroadcast
                {
                    RequestID = ActiveBattle.ResponseID,
                    Message = string.Concat(new string[]
                    {
                        "3|",
                        PlayerName,
                        "|",
                        targetId.ToString(),
                        "|",
                        ActiveBattle.Turn.ToString(),
                        "|",
                        ActiveBattle.SelectedPokemonIndex.ToString()
                    })
                });

                SendProto(new PSXAPI.Request.BattleItem
                {
                    Item = id,
                    RequestID = ActiveBattle.ResponseID,
                    Target = targetId,
                    TargetMove = moveTarget,
                    Position = ActiveBattle.SelectedPokemonIndex + 1
                });
            }
        }

        public void SendAcceptEvolution(Guid evolvingPokemonUid)
        {
            SendProto(new PSXAPI.Request.Evolve
            {
                Accept = true,
                Pokemon = evolvingPokemonUid
            });
        }

        public void SendCancelEvolution(Guid evolvingPokemonUid)
        {
            SendProto(new PSXAPI.Request.Evolve
            {
                Accept = false,
                Pokemon = evolvingPokemonUid
            });
        }

        public TimeSpan PingUpdateTime
        {
            get => pingUpdateTime;
            set
            {
                if (PingUpdateTime == value)
                {
                    return;
                }
                pingUpdateTime = value;
                timer.Change(TimeSpan.FromSeconds(0.0), value);
            }
        }

        public int Ping
        {
            get
            {
                if (!IsConnected)
                {
                    return -1;
                }
                TimeSpan t = DateTime.UtcNow - lastPingResponseUtc;
                if (t > PingUpdateTime + TimeSpan.FromSeconds(2.0))
                {
                    return (int)t.TotalMilliseconds;
                }
                return ping;
            }
        }

        private void ProcessPacket(string packet)
        {
            var data = packet.Split(" ".ToCharArray());

            byte[] array = Convert.FromBase64String(data[1]);
            var type = Type.GetType($"PSXAPI.Response.{data[0]}, PSXAPI");

            if (type is null)
            {
                Console.WriteLine("Received Unknown Response: " + data[0]);
            }
            else
            {
                var proto = typeof(Proto).GetMethod("Deserialize").MakeGenericMethod(new Type[]
                {
                    type
                }).Invoke(null, new object[]
                {
                    array
                }) as IProto;

                if (proto is PSXAPI.Response.Ping)
                {
                    ping = (int)(DateTime.UtcNow - ((PSXAPI.Response.Ping)proto).DateTimeUtc).TotalMilliseconds;
                    lastPingResponseUtc = DateTime.UtcNow;
                    receivedPing = true;
                }
                else
                {
                    switch (proto)
                    {
                        case PSXAPI.Response.Greeting gr:
#if DEBUG
                            Console.WriteLine($"Server Version: {gr.ServerVersion}\nUsers Online: {gr.UsersOnline}");
#endif
                            break;
                        case PSXAPI.Response.LoginQueue queue:
                            LogMessage?.Invoke("[Login Queue]: Average Wait-Time: " + queue.EstimatedTime.FormatTimeString());
                            break;
                        case PSXAPI.Response.Money money:
                            Money = (int)money.Game;
                            Gold = (int)money.Gold;
                            InventoryUpdated?.Invoke();
                            break;
                        case PSXAPI.Response.Move move:
                            OnPositionUpdated(move);
                            break;
                        case PSXAPI.Response.Mount mtP:
                            OnMountUpdate(mtP);
                            break;
                        case PSXAPI.Response.Area area:

                            break;
                        case PSXAPI.Response.ChatJoin join:
                            OnChannels(join);
                            break;
                        case PSXAPI.Response.ChatMessage msg:
                            OnChatMessage(msg);
                            break;
                        case PSXAPI.Response.Message pm:
                            OnPrivateMessage(pm);
                            break;
                        case PSXAPI.Response.Time time:
                            OnUpdateTime(time);
                            break;
                        case PSXAPI.Response.Login login:
                            CheckLogin(login);
                            break;
                        case PSXAPI.Response.Sync sync:
                            OnPlayerSync(sync);
                            break;
                        case PSXAPI.Response.DebugMessage dMsg:
                            SystemMessage?.Invoke(dMsg.Message);
                            break;
                        case PSXAPI.Response.InventoryPokemon iPoke:
                            OnTeamUpdated(new PSXAPI.Response.InventoryPokemon[] { iPoke });
                            break;
                        case PSXAPI.Response.DailyLootbox dl:
                            OnLootBoxRecieved(dl);
                            break;
                        case PSXAPI.Response.Lootbox bx:
                            OnLootBoxRecieved(bx);
                            break;
                        case PSXAPI.Response.MapUsers mpusers:
                            OnUpdatePlayer(mpusers);
                            break;
                        case PSXAPI.Response.Inventory Inv:
                            OnInventoryUpdate(Inv);
                            break;
                        case PSXAPI.Response.InventoryItem invItm:
                            UpdateItems(new PSXAPI.Response.InventoryItem[] { invItm });
                            break;
                        case PSXAPI.Response.Script sc:
                            OnScript(sc);
                            break;
                        case PSXAPI.Response.Battle battle:
                            OnBattle(battle);
                            break;
                        case PSXAPI.Response.PokedexUpdate dexUpdate:
                            OnPokedexUpdate(dexUpdate);
                            break;
                        case PSXAPI.Response.Reorder reorder:
                            OnReorderPokemon(reorder);
                            break;
                        case PSXAPI.Response.Evolve evolve:
                            OnEvolved(evolve);
                            break;
                        case PSXAPI.Response.Effect effect:
                            OnEffects(effect);
                            break;
                        case PSXAPI.Response.Party party:
                            if (party.ChatID.ToString() != _partyChannel)
                            {
                                _partyChannel = party.ChatID.ToString();
                                SendJoinChannel(_partyChannel);
                            }
                            break;
                        case PSXAPI.Response.Guild guild:
                            if (guild.Chat.ToString() != _guildChannel)
                            {
                                _guildChannel = guild.Chat.ToString();
                                SendJoinChannel(_guildChannel);
                            }
                            break;
                        case PSXAPI.Response.UseItem itm:
                            
                            break;
                    }
#if DEBUG

                    Console.WriteLine(proto._Name);
#endif
                }
            }
        }

        private void OnEffects(Effect effect)
        {
            if (effect.Effects is null) return;

            if (effect.Type == EffectUpdateType.All)
            {
                Effects.Clear();
                foreach (var ef in effect.Effects)
                {
                    Effects.Add(new PlayerEffect(ef, DateTime.UtcNow));
                }
            }
            else if (effect.Type == EffectUpdateType.AddOrUpdate)
            {
                foreach (var ef in effect.Effects)
                {
                    var foundEf = Effects.Find(e => e.UID == ef.UID);
                    if (foundEf != null)
                    {
                        foundEf = new PlayerEffect(ef, DateTime.UtcNow);
                    }
                    else
                    {
                        Effects.Add(new PlayerEffect(ef, DateTime.UtcNow));
                    }
                }
            }
            else if (effect.Type == EffectUpdateType.Remove)
            {
                foreach (var ef in effect.Effects)
                {
                    var foundEf = Effects.Find(e => e.UID == ef.UID);
                    if (foundEf != null)
                    {
                        Effects.Remove(foundEf);
                    }
                }
            }
        }

        private void OnEvolved(Evolve evolve)
        {
            if (evolve.Result == EvolutionResult.Success)
                SystemMessage?.Invoke($"{PokemonManager.Instance.GetNameFromEnum(evolve.Previous)} evolved into " +
                    $"{PokemonManager.Instance.GetNameFromEnum(evolve.Pokemon.Pokemon.Payload.PokemonID)}");
            else if (evolve.Result == EvolutionResult.Failed)
                SystemMessage?.Invoke("Failed to evolve!");
            else if (evolve.Result == EvolutionResult.Canceled)
                SystemMessage?.Invoke($"{PokemonManager.Instance.GetNameFromEnum(evolve.Previous)} did not evolve!");

            if (evolve.Pokemon != null)
                OnTeamUpdated(new [] { evolve.Pokemon });
        }

        private void OnPrivateMessage(Message pm)
        {
            if (pm != null)
            {
                if (pm.Event == MessageEvent.Message)
                {
                    if (!Conversations.Contains(pm.Name))
                        Conversations.Add(pm.Name);
                    if (!string.IsNullOrEmpty(pm.Text))
                        PrivateMessage?.Invoke(pm.Name, pm.Name, pm.Text);
                }
                else
                {
                    Conversations.Remove(pm.Name);
                    var removeMsg = pm.Event == MessageEvent.Closed ? $"{pm.Name} closed the Chat Window." : $"{pm.Name} is offline.";
                    LeavePrivateMessage?.Invoke(pm.Name, "System", removeMsg);
                }
            }
        }

        private void OnChatMessage(ChatMessage msg)
        {

            if (string.IsNullOrEmpty(msg.Channel))
            {
                foreach (var m in msg.Messages)
                    SystemMessage?.Invoke(m.Message);

                return;
            }

            if (msg.Channel.ToLower() == mapChatChannel.ToLower() && !string.IsNullOrEmpty(msg.Channel))
            {
                msg.Channel = "Map";
            }
            if (msg.Channel.ToLower() == _partyChannel.ToLower() && !string.IsNullOrEmpty(msg.Channel))
            {
                msg.Channel = "Party";
            }
            if (msg.Channel.ToLower() == _guildChannel.ToLower() && !string.IsNullOrEmpty(msg.Channel))
            {
                msg.Channel = "Guild";
            }

            if (Channels.ContainsKey(msg.Channel) && !string.IsNullOrEmpty(msg.Channel))
            {
                var channelName = msg.Channel;
                foreach (var message in msg.Messages)
                {

                    ChannelMessage?.Invoke(channelName, message.Username, message.Message);
                }
            }
            else
            {
                Channels.Add(msg.Channel, new ChatChannel("", msg.Channel));
                RefreshChannelList?.Invoke();
            }
        }

        private void OnUpdatePlayer(MapUsers mpusers)
        {
            var data = mpusers.Users;
            DateTime expiration = DateTime.UtcNow.AddSeconds(20);

            bool isNewPlayer = false;
            foreach (var user in data)
            {
                PlayerInfos player;
                if (Players.ContainsKey(user.Username))
                {
                    player = Players[user.Username];
                    player.Update(user, expiration);
                }
                else if (_removedPlayers.ContainsKey(user.Username))
                {
                    player = _removedPlayers[user.Username];
                    player.Update(user, expiration);
                }
                else
                {
                    isNewPlayer = true;
                    player = new PlayerInfos(user, expiration);
                }
                player.Updated = DateTime.UtcNow;

                Players[player.Name] = player;

                if (isNewPlayer || player.Actions.Any(ac => ac.Action == PSXAPI.Response.Payload.MapUserAction.Enter))
                {
                    PlayerAdded?.Invoke(player);
                }
                else if (player.Actions.Any(ac => ac.Action == PSXAPI.Response.Payload.MapUserAction.Leave))
                {
                    PlayerRemoved?.Invoke(player);
                    if (_removedPlayers.ContainsKey(player.Name))
                        _removedPlayers[player.Name] = player;
                    else
                        _removedPlayers.Add(player.Name, player);
                    Players.Remove(player.Name);
                }
                else if (player.Actions.Any(ac => ac.Action != PSXAPI.Response.Payload.MapUserAction.Leave))
                {
                    PlayerUpdated?.Invoke(player);
                }
            }
        }

        private void OnReorderPokemon(Reorder reorder)
        {
            if (reorder.Box == 0)
            {
                if (reorder.Pokemon != null)
                {
                    if (reorder.Pokemon.Length > 0)
                    {
                        var i = 0;
                        foreach (var id in reorder.Pokemon)
                        {
                            var poke = Team.Find(x => x.PokemonData.Pokemon.UniqueID == id);
                            var index = Team.IndexOf(poke);
                            if (i <= Team.Count - 1)
                                poke.UpdatePosition(i + 1);

                            Team[index] = poke;

                            i++;
                        }
                    }
                }
            }

            SortTeam();

            PokemonsUpdated?.Invoke();
            //else PC box..
        }

        private void SortTeam()
        {
            Team = Team.OrderBy(poke => poke.Uid).ToList();
        }

        private void OnBattle(PSXAPI.Response.Battle battle)
        {
            _movements.Clear();
            _slidingDirection = null;

            IsInBattle = !battle.Ended;
            if (ActiveBattle != null && !ActiveBattle.OnlyInfo)
            {
                ActiveBattle.UpdateBattle(battle, Team);
            }
            else
            {
                ActiveBattle = new Battle(PlayerName, battle, Team);
            }
            if (!ActiveBattle.OnlyInfo && IsInBattle)
                BattleStarted?.Invoke();

            if (ActiveBattle != null)
                ActiveBattle.AlreadyCaught = IsCaughtById(ActiveBattle.OpponentId);

            OnBattleMessage(battle.Log);
        }

        private void ActiveBattleMessage(string txt) 
            => BattleMessage?.Invoke(txt);

        private void OnBattleMessage(string[] logs)
        {
            ActiveBattle.ProcessLog(logs, Team, ActiveBattleMessage);

            PokemonsUpdated?.Invoke();

            if (ActiveBattle.IsFinished)
            {
                _battleTimeout.Set(Rand.Next(1500, 5000));
            }
            else
            {
                _battleTimeout.Set(Rand.Next(2000, 4000));
            }
            if (ActiveBattle.IsFinished)
            {
                IsInBattle = false;
                ActiveBattle = null;
                Resync();
                BattleEnded?.Invoke();
            }
        }

        private void OnMountUpdate(Mount mtP)
        {
            if (mtP.MountType == MountType.Bike || mtP.MountType == MountType.Pokemon)
            {
                IsBiking = true;
                IsOnGround = true;
            }
            else if (mtP.MountType == MountType.None)
            {
                IsBiking = false;
                IsOnGround = true;
                IsSurfing = false;
            }
            else if (mtP.MountType == MountType.Surfing)
            {
                IsBiking = false;
                IsOnGround = false;
                IsSurfing = true;
            }
        }

        private void OnScript(PSXAPI.Response.Script data)
        {
            if (data is null) return;

            var id = data.ScriptID;
            var type = data.Type;

#if DEBUG
            if (data.Text != null)
            {
                foreach (var s in data.Text)
                {
                    Console.WriteLine(s.Text);
                }
            }
#endif

            var active = true;

            _currentScriptType = type;
            _currentScript = data;
            if (IsLoggedIn && _cachedScripts.Count > 0) // processing _cachedScripts, these scripts are received before getting fully logged int!
            {
                switch (type)
                {
                    case ScriptRequestType.Choice:
                        if (data.Text != null)
                        {
                            if (data.Text.ToList().Any(x => x.Text.Contains("start your journey"))
                                || data.Data.ToList().Any(x => x == "Kanto" || x == "Johto"))
                            {
                                SendProto(new PSXAPI.Request.Script
                                {
                                    Response = "0",
                                    ScriptID = data.ScriptID
                                });
                                active = false;
                            }
                        }
                        break;
                    case ScriptRequestType.Unfreeze:
                        if (data.Text != null)
                        {
                            foreach (var text in data.Text)
                            {
                                var st = text.Text;
                                var index = st.IndexOf("(");
                                var scriptType = st.Substring(0, index);
                                switch (scriptType)
                                {
                                    case "setlos":
                                        var command = st.Replace(scriptType, "").Replace("(", "").Replace(")", "");
                                        var npcId = Guid.Parse(command.Split(',')[0]);
                                        var los = Convert.ToInt32(command.Split(',')[1]);
                                        if (Map.OriginalNpcs.Find(x => x.Id == npcId) != null)
                                        {
                                            Map.OriginalNpcs.Find(x => x.Id == npcId).UpdateLos(los);
                                        }
                                        active = false;
                                        break;
                                    case "enablenpc":
                                        command = st.Replace(scriptType, "").Replace("(", "").Replace(")", "");
                                        npcId = Guid.Parse(command.Split(',')[0]);
                                        var hide = command.Split(',')[1] == "0";
                                        if (Map.OriginalNpcs.Find(x => x.Id == npcId) != null)
                                        {
                                            Map.OriginalNpcs.Find(x => x.Id == npcId).Visible(hide);
                                        }
                                        active = false;
                                        break;
                                }
                            }
                        }
                        break;
                }
            }
            else if (!IsLoggedIn)
                _cachedScripts.Add(data);

            //_cachedScripts.Remove(data);

            if (_cachedScripts.Count <= 0 && active && IsLoggedIn && data.Text != null)
            {
                foreach (var scriptText in data.Text)
                {
                    if (!scriptText.Text.EndsWith(")") && scriptText.Text.IndexOf("(") == -1)
                        DialogOpened?.Invoke(scriptText.Text);
                    else
                        ProcessScriptMessage(scriptText.Text);
                }
            }


            IsScriptActive = active && IsLoggedIn && _cachedScripts.Count <= 0;
            if (active && IsLoggedIn && _cachedScripts.Count <= 0)
            {
                _dialogTimeout.Set(Rand.Next(1500, 4000));
                Scripts.Add(data);
            }
        }

        private void ProcessScriptMessage(string text)
        {
            var st = text;
            var index = st.IndexOf("(");
            var scriptType = st.Substring(0, index);
            switch (scriptType)
            {
                case "setlos":
                    var command = st.Replace(scriptType, "").Replace("(", "").Replace(")", "");
                    var npcId = Guid.Parse(command.Split(',')[0]);
                    var los = Convert.ToInt32(command.Split(',')[1]);
                    if (Map.OriginalNpcs.Find(x => x.Id == npcId) != null)
                    {
                        Map.OriginalNpcs.Find(x => x.Id == npcId).UpdateLos(los);
                    }
                    break;
                case "enablenpc":
                    command = st.Replace(scriptType, "").Replace("(", "").Replace(")", "");
                    npcId = Guid.Parse(command.Split(',')[0]);
                    var hide = command.Split(',')[1] == "0";
                    if (Map.OriginalNpcs.Find(x => x.Id == npcId) != null)
                    {
                        Map.OriginalNpcs.Find(x => x.Id == npcId).Visible(hide);
                    }
                    break;
            }
        }

        private void OnLootBoxRecieved(IProto dl, TimeSpan? timeSpan = null)
        {
            if (RecievedLootBoxes is null)
            {
                RecievedLootBoxes = new LootboxHandler();
                RecievedLootBoxes.LootBoxMessage += RecievedLootBoxMsg;
                RecievedLootBoxes.RecievedBox += RecievedLootBoxes_RecievedBox;
                RecievedLootBoxes.BoxOpened += RecievedLootBoxes_BoxOpened;
            }
            if (dl is PSXAPI.Response.DailyLootbox db)
                RecievedLootBoxes.HandleDaily(db, timeSpan.GetValueOrDefault() == null ? db.Timer : timeSpan);
            else if (dl is PSXAPI.Response.Lootbox lb)
                RecievedLootBoxes.HandleLootbox(lb);
        }

        private void RecievedLootBoxes_BoxOpened(PSXAPI.Response.Payload.LootboxRoll[] rewards, LootboxType type)
        {
            LootBoxOpened?.Invoke(rewards, type);
            _lootBoxTimeout.Set(Rand.Next(2500, 3200));
        }

        private void RecievedLootBoxes_RecievedBox(LootboxHandler obj)
        {
            RecievedLootBox?.Invoke(obj);
            _lootBoxTimeout.Set(Rand.Next(1500, 2000));
        }

        private void RecievedLootBoxMsg(string ob) => LootBoxMessage?.Invoke(ob);

        private void OnPlayerSync(PSXAPI.Response.Sync sync)
        {
            PSXAPI.Response.Move move = new PSXAPI.Response.Move
            {
                Action = PSXAPI.Response.MoveAction.Set,
                Direction = PSXAPI.Response.PlayerDirection.Default,
                Map = sync.Map,
                X = sync.PosX,
                Y = sync.PosY,
                Height = sync.Height,
                Scripts = sync.Scripts,
            };
            OnPositionUpdated(move, false);
        }

        private void CheckLogin(PSXAPI.Response.Login login)
        {
            if (login.Result == PSXAPI.Response.LoginResult.Success)
            {
                OnLoggedIn(login);
            }
            else
            {
                AuthenticationFailed?.Invoke(login.Error);
                Close();
            }
        }

        private void OnPositionUpdated(PSXAPI.Response.Move movement, bool sync = true)
        {
            bool teleported = false;
            if (MapName != movement.Map || PlayerX != movement.X || PlayerY != movement.Y)
            {
                PlayerX = movement.X;
                PlayerY = movement.Y;

                string prev = mapChatChannel;
                mapChatChannel = "map:" + movement.Map;
                if (mapChatChannel != prev && MapName != movement.Map)
                {
                    SendJoinChannel(mapChatChannel);
                }
                LoadMap(movement.Map);
                teleported = true;
            }
            if (sync)
                Resync(MapName != movement.Map);
            CheckArea();
            if (teleported)
            {
                TeleportationOccuring?.Invoke(movement.Map, PlayerX, PlayerY);
            }
        }

        private void OnPokedexData(PSXAPI.Response.Pokedex data)
        {
            if (data.Entries != null)
            {
                foreach (var en in data.Entries)
                {
                    PokedexPokemons.Add(new PokedexPokemon(en));
                }
                PokedexOwned = data.Caught;
                PokedexSeen = data.Seen;
            }
            PokedexUpdated?.Invoke(PokedexPokemons);
        }

        private void OnPokedexUpdate(PSXAPI.Response.PokedexUpdate data)
        {
            if (data.Entry != null)
            {
                var dexPoke = new PokedexPokemon(data.Entry);
                if (PokedexPokemons.Count > 0)
                {
                    var findDex = PokedexPokemons.Find(x => x.Id == dexPoke.Id);
                    if (findDex != null)
                    {
                        PokedexPokemons.Insert(PokedexPokemons.IndexOf(findDex), dexPoke);
                        PokedexPokemons.Remove(findDex);
                    }
                    else
                    {
                        PokedexPokemons.Add(dexPoke);
                    }
                }
                else
                    PokedexPokemons.Add(dexPoke);
            }
            RequestArea(MapName, AreaName);
            PokedexUpdated?.Invoke(PokedexPokemons);
        }

        public void RequestArea(string Map, string Areaname)
        {
            SendProto(new PSXAPI.Request.Area
            {
                Map = Map,
                AreaName = Areaname
            });
        }

        public bool IsCaughtById(int id)
        {
            return PokedexPokemons.Count > 0 && PokedexPokemons.Any(x => x.Caught && x.Id == id);
        }

        public bool IsCaughtByName(string name)
        {
            return PokedexPokemons.Count > 0 && PokedexPokemons.Any(x => x.Caught && x.Name.ToLowerInvariant() == name.ToLowerInvariant());
        }

        private void OnLoggedIn(PSXAPI.Response.Login login)
        {
            _isLoggedIn = true;
            PlayerName = login.Username;

            Console.WriteLine("[Login] Authenticated successfully");
            LoggedIn?.Invoke();

            if (_currentScript != null)
            {
                switch (_currentScriptType)
                {
                    case ScriptRequestType.Customize:
                        //Character!

                        SendCharacterCustomization(0, Rand.Next(0, 3), Rand.Next(0, 13), Rand.Next(0, 27), Rand.Next(0, 4));

                        break;
                    case ScriptRequestType.Unfreeze:

                        break;
                }
            }
            if (login.Level != null)
                OnLevel(login.Level);
            OnTeamUpdated(login.Inventory.ActivePokemon);
            OnInventoryUpdate(login.Inventory);
            OnMountUpdate(login.Mount);
            if (ActiveBattle != null)
                OnPositionUpdated(login.Position, true);
            else
                OnPositionUpdated(login.Position, false);

            if (login.Pokedex != null)
            {
                OnPokedexData(login.Pokedex);
            }

            if (login.Battle != null)
                OnBattle(login.Battle);

            if (login.Effects != null)
                OnEffects(login.Effects);

            if (login.Time != null)
            {
                OnUpdateTime(login.Time);
            }

            if (login.DailyLootbox != null)
                OnLootBoxRecieved(login.DailyLootbox, login.DailyReset);
            if (login.Lootboxes != null)
                foreach (var lootBox in login.Lootboxes)
                    OnLootBoxRecieved(lootBox);
            if (login.NearbyUsers != null)
                _cachedNerbyUsers = login.NearbyUsers;

            if (login.Party != null)
            {
                if (login.Party.ChatID.ToString() != _partyChannel)
                {
                    _partyChannel = login.Party.ChatID.ToString();
                    SendJoinChannel(_partyChannel);
                }
            }
            if (login.Guild != null)
            {
                if (login.Guild.Chat.ToString() != _guildChannel)
                {
                    _guildChannel = login.Guild.Chat.ToString();
                    SendJoinChannel(_guildChannel);
                }
            }


            AddDefaultChannels();
            if (RecievedLootBoxes?.TotalLootBoxes > 0)
            {
                RecievedLootBox?.Invoke(RecievedLootBoxes);
            }
        }

        private void OnLevel(Level data)
        {
            var preLevel = Level;
            Level = data;
            LevelChanged?.Invoke(preLevel, Level);
        }

        private void OnChannels(PSXAPI.Response.ChatJoin join)
        {
#if DEBUG
            Console.WriteLine("Received Channel: " + join.Channel);
#endif
            if (join.Result == PSXAPI.Response.ChatJoinResult.Joined)
            {

                if (join.Channel.ToLower() == mapChatChannel.ToLower())
                {
                    join.Channel = "Map";
                }
                if (join.Channel.ToLower() == _partyChannel.ToLower())
                {
                    join.Channel = "Party";
                }
                if (join.Channel.ToLower() == _guildChannel.ToLower())
                {
                    join.Channel = "Guild";
                }
                if (!Channels.ContainsKey(join.Channel))
                {
                    Channels.Add(join.Channel, new ChatChannel("", join.Channel));
                }
            }
            else if (join.Result == ChatJoinResult.Left)
            {
                if (Channels.ContainsKey(join.Channel))
                    Channels.Remove(join.Channel);
            }
            RefreshChannelList?.Invoke();
        }
        private void OnTeamUpdated(PSXAPI.Response.InventoryPokemon[] pokemons)
        {
            if (pokemons is null) return;
            if (pokemons.Length <= 1)
            {
#if DEBUG
                Console.WriteLine("Received One Pokemon Data!");
#endif
                if (Team.Count > 0)
                {
                    var poke = new Pokemon(pokemons[0]);
                    var foundPoke = Team.Find(x => x.PokemonData.Pokemon.UniqueID == pokemons[0].Pokemon.UniqueID);
                    if (foundPoke != null)
                    {
                        var index = Team.IndexOf(foundPoke);
                        Team[index] = poke;
                    }
                    else
                    {                       
                        Team.Add(poke);
                    }
                    if (poke.CanEvolveTo > PSXAPI.Response.Payload.PokemonID.missingno)
                    {
                        OnEvolving(pokemons[0]);
                    }
                    if (pokemons[0].CanLearnMove != null && pokemons[0].CanLearnMove.Length > 0)
                    {
                        OnLearningMove(pokemons[0]);
                    }
                }
                else
                {
                    Team.Clear();
                    foreach (var poke in pokemons)
                    {
                        if (poke.CanEvolve > PSXAPI.Response.Payload.PokemonID.missingno)
                        {
                            OnEvolving(poke);
                        }
                        if (poke.CanLearnMove != null && poke.CanLearnMove.Length > 0)
                        {
                            OnLearningMove(poke);
                        }
                        Team.Add(new Pokemon(poke));
                    }
                }
            }
            else
            {
                Team.Clear();
                foreach (var poke in pokemons)
                {
                    if (poke.CanEvolve > PSXAPI.Response.Payload.PokemonID.missingno)
                    {
                        OnEvolving(poke);
                    }
                    if (poke.CanLearnMove != null && poke.CanLearnMove.Length > 0)
                    {
                        OnLearningMove(poke);
                    }
                    Team.Add(new Pokemon(poke));
                }
            }

            CanUseCut = HasCutAbility();
            CanUseSmashRock = HasRockSmashAbility();
            SortTeam();
            PokemonsUpdated?.Invoke();
        }

        private void OnLearningMove(InventoryPokemon learningPoke)
        {
            LearningMove?.Invoke(learningPoke.CanLearnMove[0],
                learningPoke.Position, learningPoke.Pokemon.UniqueID);
        }

        private void OnEvolving(InventoryPokemon poke)
        {
            Evolving?.Invoke(poke.Pokemon.UniqueID);
        }

        public bool SwapPokemon(int pokemon1, int pokemon2)
        {
            if (IsInBattle || pokemon1 < 1 || pokemon2 < 1 || Team.Count < pokemon1 || Team.Count < pokemon2 || pokemon1 == pokemon2)
            {
                return false;
            }
            if (!_swapTimeout.IsActive)
            {
                SendSwapPokemons(pokemon1, pokemon2);
                _swapTimeout.Set();
                return true;
            }
            return false;
        }

        private void OnInventoryUpdate(PSXAPI.Response.Inventory data)
        {
            Money = (int)data.Money;
            Gold = (int)data.Gold;
            UpdateItems(data.Items);
        }

        private void UpdateItems(PSXAPI.Response.InventoryItem[] items)
        {
            if (items is null) return;
            if (items.Length == 1)
            {
                var item = new InventoryItem(items[0]);
                if (Items.Count > 0)
                {
                    var foundItem = Items.Find(x => x.Id == item.Id);
                    if (foundItem != null)
                    {
                        Items[Items.IndexOf(foundItem)] = item;
                    }
                    else
                    {
                        Items.Add(item);
                    }
                }
                else
                {
                    Items.Clear();
                    Items.Add(item);
                }
            }
            else
            {
                Items.Clear();
                foreach (var item in items)
                {
                    Items.Add(new InventoryItem(item));
                }
            }
            InventoryUpdated?.Invoke();
        }

        private void OnUpdateTime(PSXAPI.Response.Time time)
        {
            GameTime = time.GameDayTime.ToString() + " " + GetGameTime(time.GameTime, time.TimeFactor, DateTime.UtcNow);
            Weather = time.Weather.ToString();
            GameTimeUpdated?.Invoke(GameTime, Weather);
        }

        public void SendAuthentication(string username, string password)
        {
            SendProto(new PSXAPI.Request.Login
            {
                Name = username,
                Password = password,
                Platform = PSXAPI.Request.ClientPlatform.PC,
                Version = Version
            });
        }
        public string GetGameTime(TimeSpan time, double sc, DateTime dt)
        {
            TimeSpan timeSpan = time;
            timeSpan = timeSpan.Add(TimeSpan.FromSeconds((DateTime.UtcNow - dt).TotalSeconds * sc));
            if ((int)timeSpan.TotalDays > 0)
            {
                timeSpan = timeSpan.Subtract(TimeSpan.FromDays(((int)timeSpan.TotalDays)));
            }
            if (_lastTime != timeSpan.Seconds)
            {
                SendProto(new PSXAPI.Request.Time());
            }
            _lastTime = timeSpan.Seconds;
            string result;
            if (timeSpan.Hours >= 12)
            {
                if (timeSpan.Hours == 12)
                {
                    result = "12:" + timeSpan.Minutes.ToString().PadLeft(2, '0') + " PM";
                }
                else
                {
                    result = (timeSpan.Hours - 12).ToString() + ":" + timeSpan.Minutes.ToString().PadLeft(2, '0') + " PM";
                }
            }
            else if (timeSpan.Hours == 0)
            {
                result = "12:" + timeSpan.Minutes.ToString().PadLeft(2, '0') + " AM";
            }
            else
            {
                result = timeSpan.Hours.ToString() + ":" + timeSpan.Minutes.ToString().PadLeft(2, '0') + " AM";
            }
            return result;
        }

        public void RunFromBattle()
        {
            SendRunFromBattle();
            _battleTimeout.Set();
        }

        public void UseAttack(int number, bool megaEvolve = false)
        {
            SendAttack(number, megaEvolve);
            _battleTimeout.Set();
        }

        public void UseItem(int id, int pokemonUid = 0, int moveId = 0)
        {
            if (!(pokemonUid >= 0 && pokemonUid <= 6) || !HasItemId(id))
            {
                return;
            }
            var item = GetItemFromId(id);
            if (item == null || item.Quantity == 0)
            {
                return;
            }
            if (pokemonUid == 0) // simple use
            {
                
                if (!_battleTimeout.IsActive && IsInBattle && item.CanBeUsedInBattle)
                {
                    SendUseItemInBattle(item.Id, ActiveBattle.SelectedOpponent, item.IsPokeball(), moveId);
                    _battleTimeout.Set();
                }
            }
            else // use item on pokemon
            {
                if (!_battleTimeout.IsActive && IsInBattle && item.CanBeUsedOnPokemonInBattle)
                {
                    SendUseItemInBattle(item.Id, pokemonUid);
                    _battleTimeout.Set();
                }
            }
        }

        public void LearnMove(Guid pokemonUniqueId, PSXAPI.Response.Payload.PokemonMoveID learningMoveId, int moveToForget)
        {
            _swapTimeout.Set();
            SendProto(new PSXAPI.Request.Learn
            {
                Accept = true,
                Move = learningMoveId,
                Pokemon = pokemonUniqueId,
                Position = moveToForget
            });
        }

        public void ChangePokemon(int number)
        {
            SendChangePokemon(ActiveBattle.SelectedPokemonIndex + 1, number);
            _battleTimeout.Set();
        }

        public void UseSurfAfterMovement()
        {
            _surfAfterMovement = true;
        }

        public void UseSurf()
        {
            SendProto(new PSXAPI.Request.Effect
            {
                Action = PSXAPI.Request.EffectAction.Use,
                UID = GetEffectFromName("Surf").UID
            });
            _mountingTimeout.Set();
        }

        public int DistanceTo(int cellX, int cellY)
        {
            return Math.Abs(PlayerX - cellX) + Math.Abs(PlayerY - cellY);
        }

        public static int DistanceBetween(int fromX, int fromY, int toX, int toY)
        {
            return Math.Abs(fromX - toX) + Math.Abs(fromY - toY);
        }

        private void LoadMap(string mapName)
        {
            mapName = MapClient.RemoveExtension(mapName);

            _loadingTimeout.Set(Rand.Next(1500, 4000));

            _movements.Clear();
            _slidingDirection = null;
            _movementTimeout.Cancel();
            _lootBoxTimeout.Cancel();

            if (Map is null || mapName != MapName)
            {
                DownloadMap(mapName);
            }
        }

        private void DownloadMap(string mapName)
        {

            Console.WriteLine("[Map] Requesting: " + MapName);

            AreNpcReceived = false;

            Map = null;
            MapName = mapName;
            _mapClient.DownloadMap(mapName);
            Players.Clear();
            _removedPlayers.Clear();
        }
        private void MapClient_MapLoaded(string mapName, Map map)
        {
            Map = map;
            Map.AreaUpdated += Map_AreaUpdated;

            if (Map.IsSessioned)
            {
                Resync();
            }
            if (mapName.ToLowerInvariant() == MapName.ToLowerInvariant()) // well the received map is always upper case, meh idk if I did something wrong.
            {
                Players.Clear();
                _removedPlayers.Clear();
                CheckArea();
                MapLoaded?.Invoke(AreaName);
            }
            CheckForCachedScripts();
            if (_cachedNerbyUsers != null && _cachedNerbyUsers.Users != null)
            {
                OnUpdatePlayer(_cachedNerbyUsers);
            }

            CanUseCut = HasCutAbility();
            CanUseSmashRock = HasRockSmashAbility();

            AreNpcReceived = Map.MapDump != null && Map.MapDump?.NPCs != null && Map.OriginalNpcs.Count > 0;

            if (Map.MapDump.Areas != null && Map.MapDump.Areas.Count > 0)
            {
                foreach (var area in Map.MapDump.Areas)
                {
#if DEBUG
                    Console.WriteLine($"[{Map.MapDump.Areas.IndexOf(area)}]: {area.AreaName}");
#endif
                }
            }
        }

        private void CheckForCachedScripts()
        {
            if (_cachedScripts.Count > 0)
            {
                foreach (var script in _cachedScripts)
                {
                    OnScript(script);                   
                }
                _cachedScripts.Clear();
            }
        }

        private void Map_AreaUpdated()
        {
            AreaUpdated?.Invoke();
        }

        public void CheckArea()
        {
            if (MapName.ToLowerInvariant() == "default")
                return;

            if (Map != null)
            {
                Map?.UpdateArea();
                if (Map.MapDump.Areas != null && Map.MapDump.Areas.Count > 0)
                {
                    foreach (MAPAPI.Response.Area area in Map.MapDump.Areas)
                    {
                        if (PlayerX >= area.StartX && PlayerX <= area.EndX && PlayerY >= area.StartY && PlayerY <= area.EndY)
                        {
                            if (AreaName != area.AreaName)
                                _loadingTimeout.Set(Rand.Next(2500, 4000));
                            AreaName = area.AreaName;
                            PositionUpdated?.Invoke(AreaName, PlayerX, PlayerY);
                            return;
                        }
                    }
                    if (AreaName != Map.MapDump.Settings.MapName)
                    {
                        _loadingTimeout.Set(Rand.Next(2500, 4000));
                        AreaName = Map.MapDump.Settings.MapName;
                    }
                }
                else if (AreaName != Map.MapDump.Settings.MapName)
                {
                    _loadingTimeout.Set(Rand.Next(2500, 4000));
                    AreaName = Map.MapDump.Settings.MapName;
                }
                    

                PositionUpdated?.Invoke(AreaName, PlayerX, PlayerY);
            }
        }
        public void Resync(bool mapLoad = true)
        {
            var _syncId = Guid.NewGuid();
            SendProto(new PSXAPI.Request.Sync
            {
                ID = _syncId,
                MapLoad = mapLoad
            });
        }

        public bool HasSurfAbility()
        {
            return HasMove("Surf") && HasEffectName("Surf");
        }

        public bool HasCutAbility()
        {
            return (HasMove("Cut")) &&
                (Map?.Region == "1" && HasItemName("Cascade Badge") ||
                Map?.Region == "2" && HasItemName("Hive Badge") ||
                Map?.Region == "3" && HasItemName("Stone Badge") ||
                Map?.Region == "4" && HasItemName("Forest Badge"));
        }
        public bool HasRockSmashAbility()
        {
            return HasMove("Rock Smash");
        }

        public bool PokemonUidHasMove(int pokemonUid, string moveName)
        {
            return Team.FirstOrDefault(p => p.Uid == pokemonUid)?.Moves.Any(m => m.Name?.Equals(moveName, StringComparison.InvariantCultureIgnoreCase) ?? false) ?? false;
        }

        public bool HasMove(string moveName)
        {
            return Team.Any(p => p.Moves.Any(m => m.Name?.Equals(moveName, StringComparison.InvariantCultureIgnoreCase) ?? false));
        }

        public int GetMovePosition(int pokemonUid, string moveName)
        {
            return Team[pokemonUid].Moves.FirstOrDefault(m => m.Name?.Equals(moveName, StringComparison.InvariantCultureIgnoreCase) ?? false)?.Position ?? -1;
        }

        public InventoryItem GetItemFromId(int id)
        {
            return Items.FirstOrDefault(i => i.Id == id && i.Quantity > 0);
        }

        public bool HasItemId(int id)
        {
            return GetItemFromId(id) != null;
        }

        public bool HasPokemonInTeam(string pokemonName)
        {
            return FindFirstPokemonInTeam(pokemonName) != null;
        }

        public Pokemon FindFirstPokemonInTeam(string pokemonName)
        {
            return Team.FirstOrDefault(p => p.Name.Equals(pokemonName, StringComparison.InvariantCultureIgnoreCase));
        }

        public InventoryItem GetItemFromName(string itemName)
        {
            return Items.FirstOrDefault(i => ItemsManager.Instance.ItemClass.items.Any(itm => itm.BattleID.Equals(itemName, StringComparison.InvariantCultureIgnoreCase) && i.Quantity > 0));
        }

        public bool HasItemName(string itemName)
        {
            return GetItemFromName(itemName) != null;
        }

        public PlayerEffect GetEffectFromName(string effectName)
        {
            return Effects.FirstOrDefault(e => e.Name.Equals(effectName, StringComparison.InvariantCultureIgnoreCase) && e.UID != Guid.Empty);
        }

        public bool HasEffectName(string effectName) => GetEffectFromName(effectName) != null;
    }
}

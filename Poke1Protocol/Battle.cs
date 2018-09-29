using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Poke1Protocol
{
    public class Battle
    {
        //public Action<string> BattleMessage;

        public int OpponentId { get; private set; }
        public int OpponentHealth { get; private set; }
        public int CurrentHealth { get; private set; }
        public int OpponentLevel { get; private set; }
        public bool IsShiny { get; private set; }
        public bool IsWild { get; private set; }
        public string OpponentGender { get; private set; }
        public string OpponentStatus { get; private set; }
        public string TrainerName { get; private set; }
        public bool IsPvp { get; }
        public int PokemonCount { get; private set; }
        public string BattleText { get; }
        public bool AlreadyCaught { get; set; }
        public int SelectedPokemonIndex { get; private set; }
        public bool IsFinished { get; private set; }
        public string _playerName { get; set; }

        public bool OnlyInfo { get; private set; } = false;

        public PokemonStats OpponentStats { get; private set; }
        public int ResponseID { get; private set; }

        public int SelectedOpponent { get; private set; }

        public int Turn { get; private set; } = 1;

        public bool IsTrapped { get; private set; }

        public bool RepeatAttack { get; set; }

        public PSXAPI.Response.Payload.BattleSide PlayerBattleSide { get; private set; }

        public int PlayerSide { get; private set; } = 1;

        public PSXAPI.Response.Battle Data { get; private set; }

        public Battle(string playerName, PSXAPI.Response.Battle data, List<Pokemon> team)
        {
            Data = data;

            _playerName = playerName;

            IsWild = data.CanCatch;

            IsFinished = data.Ended;

            if (data.Mapping1 != null && !string.IsNullOrEmpty(playerName))
            {
                if (data.Mapping1.ToList().Any(name => name.ToLowerInvariant() == _playerName.ToLowerInvariant()))
                    PlayerSide = 1;
            }

            if (data.Mapping2 != null && !string.IsNullOrEmpty(playerName))
            {
                if (data.Mapping2.ToList().Any(name => name.ToLowerInvariant() == _playerName.ToLowerInvariant()))
                    PlayerSide = 2;
            }      

            if (data.Request1 != null)
            {
                HandleBattleRequest(data.Request1, PlayerSide == 1, team);
            }
            if (data.Request2 != null)
            {
                HandleBattleRequest(data.Request2, PlayerSide == 2, team);
            }

            //if (data.Log is null && data.Request1 is null && data.Request2 is null && Turn == 1)
            //    IsFinished = true;
        }

        private List<Pokemon> _tempTeam = new List<Pokemon>();

        public void UpdateBattle(PSXAPI.Response.Battle data, List<Pokemon> team)
        {
            IsWild = data.CanCatch;

            IsFinished = data.Ended;

            _tempTeam.Clear();
            team.ForEach(ps => _tempTeam.Add(ps));

            if (data.Mapping1 != null && !string.IsNullOrEmpty(_playerName))
            {
                if (data.Mapping1.ToList().Any(name => name.ToLowerInvariant() == _playerName.ToLowerInvariant()))
                    PlayerSide = 1;

                if (team[SelectedPokemonIndex].BattleCurrentHealth <= 0)
                {
                    team.FindAll(p => p.BattleCurrentHealth <= 0)
                        .ForEach(pok => _tempTeam.Remove(pok));

                    SelectedPokemonIndex = team.IndexOf(_tempTeam[0]);
                }
            }

            if (data.Mapping2 != null && !string.IsNullOrEmpty(_playerName))
            {
                if (data.Mapping2.ToList().Any(name => name.ToLowerInvariant() == _playerName.ToLowerInvariant()))
                    PlayerSide = 2;

                if (team[SelectedPokemonIndex].BattleCurrentHealth <= 0)
                {
                    team.FindAll(p => p.BattleCurrentHealth <= 0)
                        .ForEach(pok => _tempTeam.Remove(pok));

                    SelectedPokemonIndex = team.IndexOf(_tempTeam[0]);
                }
            }

            if (data.Request1 != null)
            {
                HandleBattleRequest(data.Request1, PlayerSide == 1, team);
                Data = data;
            }
            if (data.Request2 != null)
            {
                HandleBattleRequest(data.Request2, PlayerSide == 2, team);
                Data = data;
            }
        }

        private void HandleBattleRequest(PSXAPI.Response.Payload.BattleRequest request, bool isPlayerSide, List<Pokemon> team)
        {
            if (isPlayerSide)
            {
                var p1 = request;
                var active = p1.RequestInfo.active;
                PokemonCount = p1.RequestInfo.side.pokemon.Length;
                var activePokemon = p1.RequestInfo.side.pokemon.ToList().Find(x => x.active);
                if (team is null || team.Count <= 0)
                    SelectedPokemonIndex = p1.RequestInfo.side.pokemon.ToList().IndexOf(activePokemon);
                else
                    SelectedPokemonIndex = team.IndexOf(team.Find(p => p.PokemonData.Pokemon.Payload.Personality == activePokemon.personality));
                var condition = activePokemon.condition;
                if (condition.Contains("/"))
                {
                    var currentHpBool = int.TryParse(condition.Split('/')[0], out int curHp);
                    var maxHpBool = int.TryParse(condition.Split('/')[1], out int maxHp);
                    if (currentHpBool)
                        CurrentHealth = curHp;
                    if (maxHpBool)
                        team[SelectedPokemonIndex].UpdateHealth(CurrentHealth, maxHp);
                }
                else if (condition.Contains("fnt"))
                {
                    CurrentHealth = 0;
                    team[SelectedPokemonIndex].UpdateHealth(CurrentHealth, team[SelectedPokemonIndex].BattleMaxHealth);
                }
                ResponseID = p1.RequestID;
                PlayerBattleSide = p1.RequestInfo.side;
            }
            else
            {
                var p2 = request;
                var opponent = p2.RequestInfo.side.pokemon.ToList().Find(x => x.active);
                var condition = opponent.condition;
                var currentHpBool = int.TryParse(condition.Split('/')[0], out int curHp);
                var maxHpBool = int.TryParse(condition.Split('/')[1], out int maxHp);
                if (currentHpBool)
                    OpponentHealth = curHp;
                var details = opponent.details.Split(new string[]
                    {
                        ", "
                    }, StringSplitOptions.None);
                OpponentGender = details.Length == 3 ? details[2] : "";
                OpponentId = PokemonManager.Instance.GetIdByName(details[0]);
                OpponentStats = new PokemonStats(opponent.stats, curHp);
                IsShiny = opponent.details.ToLowerInvariant().Contains("shiny");
                OpponentLevel = int.Parse(details[1].Substring(1));
                if (!IsWild && opponent.trainer != null)
                    TrainerName = opponent.trainer;
                var status = opponent.condition.Split(new string[]
                    {
                        " "
                    }, StringSplitOptions.None);
                if (status.Length > 1)
                    OpponentStatus = status[1].ToUpperInvariant();
                else
                    OpponentStatus = "OK";
                ResponseID = p2.RequestID;
                SelectedOpponent = p2.RequestInfo.side.pokemon.ToList().IndexOf(opponent);
                var opAbility = opponent.baseAbility.ToLowerInvariant().Replace(" ", "");
                IsTrapped = opAbility == "arenatrap" || opAbility == "shadowtag" || opAbility == "magnetpull" || opponent.item == "smokeball";
            }
        }

        public void ProcessLog(string[] logs, List<Pokemon> team, Action<string> BattleMessage)
        {
            if (logs is null) return;

            foreach(var log in logs)
            {
                string[] info = log.Split(new char[]
                {
                    '|'
                });
                if (info.Length > 0)
                {
                    var type = info[1];
                    if (type == "--online")
                        OnlyInfo = true;
                    else
                        OnlyInfo = false;

                    var ranAway = logs.Any(sf => sf.Contains("--run"));

                    switch (type)
                    {
                        case "turn":
                            var s = int.TryParse(info[2], out int turn);
                            if (s)
                                Turn = turn;
                            break;
                        case "-damage":
                            var damageTaker = info[2].Split(new string[]
                                {
                                    ": "
                                }, StringSplitOptions.None);
                            if (((damageTaker[0].Contains("p1") && PlayerSide == 1)
                                || (damageTaker[0].Contains("p2") && PlayerSide == 2)) 
                                && !info[3].Contains("fnt"))
                            {
                                var st = Regex.Replace(info[3], @"[a-zA-Z]+", "");
                                int currentHp = Convert.ToInt32(st.Split('/')[0]);
                                int maxHp = Convert.ToInt32(st.Split('/')[1]);
                                team[SelectedPokemonIndex].UpdateHealth(currentHp, maxHp);
                            }
                            else if (info[3].Contains("fnt"))
                            {
                                CurrentHealth = 0;
                                team[SelectedPokemonIndex].UpdateHealth(0, team[SelectedPokemonIndex].BattleMaxHealth);
                            }
                            break;
                        case "move":
                            //Attacker
                            var attacker = info[2].Split(new string[]
                                {
                                    ": "
                                }, StringSplitOptions.None);
                            //Move
                            var move = info[3];
                            // Attack Taker
                            var attackTaker = info[4].Split(new string[]
                            {
                                ": "
                            }, StringSplitOptions.None);
                            if ((attacker[0].Contains("p1") && PlayerSide == 1) || (attacker[0].Contains("p2") && PlayerSide == 2))
                            {
                                var findMove = team[SelectedPokemonIndex].Moves.ToList().Find(x => x.Name.ToLowerInvariant() == move.ToLowerInvariant());
                                if (findMove.CurrentPoints > 0)
                                    findMove.CurrentPoints -= 1;
                            }
                            BattleMessage?.Invoke($"{attacker[1]} used {move}!");
                            break;
                        case "faint":
                            var died = info[2].Split(new string[]
                            {
                                ": "
                            }, StringSplitOptions.None);
                            team[SelectedPokemonIndex].UpdateHealth(0, team[SelectedPokemonIndex].BattleMaxHealth);
                            BattleMessage?.Invoke(!died[0].Contains("p1") ? IsWild ? $"Wild {died[1]} fainted!" : $"Opponent's {died[1]} fainted!" : $"Your {died[1]} fainted!");
                            break;
                        case "--run":
                            if (info[3] == "0")
                            {
                                BattleMessage?.Invoke($"{info[4]} failed to run away!");
                            }
                            else
                            {
                                BattleMessage?.Invoke("You got away safely!");
                            }
                            break;
                        case "win":
                            IsFinished = true;
                            var winner = info[2];
                            if (!ranAway)
                                BattleMessage?.Invoke((winner == "p1" && PlayerSide == 1) || (winner == "p2" && PlayerSide == 2)
                                    ? "You have won the battle!" : "You have lost the battle!");                         
                            break;
                        case "nothing":
                            BattleMessage?.Invoke("But nothing happened!");
                            break;
                        case "-notarget":
                            BattleMessage?.Invoke("But it failed!");
                            break;
                        case "-ohko":
                            BattleMessage?.Invoke("It's a one-hit KO!");
                            break;
                        case "--catch":
                            var isMySide = Convert.ToInt32(info[2].Substring(0, 2).Replace("p", "")) == PlayerSide;
                            if (isMySide)
                            {
                                BattleMessage?.Invoke($"{info[8]} threw a " +
                                    $" {ItemsManager.Instance.ItemClass.items.ToList().Find(itm => itm.BattleID == info[4] || itm.Name == info[4]).Name}!");
                            }
                            else
                            {
                                var enemyName = PlayerSide == 1 && Data.Mapping2 != null ? 
                                    Data.Mapping2[0] : Data.Mapping1 != null && 
                                    Data.Mapping2 != null ? Data.Mapping1[0] : "Enemy"; 

                                BattleMessage?.Invoke($"{info[8]} threw a " +
                                    $" {ItemsManager.Instance.ItemClass.items.ToList().Find(itm => itm.BattleID == info[4] || itm.Name == info[4]).Name}!");

                                int pokeID = Convert.ToInt32(info[3]);
                                int success = Convert.ToInt32(info[7]);
                                int shakes = Convert.ToInt32(info[5]);

                                if (shakes < 0)
                                {
                                    BattleMessage?.Invoke("But it failed.");
                                }
                                else
                                {
                                    if (success == 0)
                                    {
                                        BattleMessage?.Invoke($"Gotcha! {PokemonManager.Instance.Names[OpponentId]} was caught!");
                                    }
                                    else if (shakes == 0)
                                    {
                                        BattleMessage?.Invoke($"Oh no! The Pokémon broke free!");
                                    }
                                    else if (shakes < 2)
                                    {
                                        BattleMessage?.Invoke("Aww! It appeared to be caught!");
                                    }
                                    else
                                    {
                                        BattleMessage?.Invoke("Aargh! Almost had it!");
                                    }
                                }
                            }
                            break;
                    }
                }
            }
        }
    }
}

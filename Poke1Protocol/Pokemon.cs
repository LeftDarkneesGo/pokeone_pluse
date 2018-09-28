using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PSXAPI.Response;

namespace Poke1Protocol
{
    public class Pokemon
    {
        public int Uid { get; private set; }
        public int Id { get; }
        public InventoryPokemon PokemonData { get; }
        public int Level {
            get;
        }
        public int MaxHealth { get; private set; }
        public int CurrentHealth { get; private set; }
        public PokemonMove[] Moves { get; }
        public PokemonExperience Experience { get; }
        public bool IsShiny { get; }
        public string Gender { get; }
        public string Nature
        {
            get;
        }
        public PokemonAbility Ability { get; }
        public int Happiness { get; }
        public string ItemHeld { get; }
        public PokemonStats Stats { get; }
        public PokemonStats IV { get; }
        public PokemonStats EV { get; }
        public PokemonStats EVsCollected { get; }
        public string OriginalTrainer { get; }
        public PokemonType Type1 { get; }
        public PokemonType Type2 { get; }
        public string Types => Type2 == PokemonType.None ? Type1.ToString() : Type1.ToString() + "/" + Type2.ToString();

        private string _status;
        public string Status
        {
            get => CurrentHealth == 0 ? "KO" : _status;
            set => _status = value;
        }
        public string Name { get; }
        public string Health => CurrentHealth + "/" + MaxHealth;
        public int BattleCurrentHealth { get; private set; }
        public int BattleMaxHealth { get; private set; }
        public PSXAPI.Response.Payload.PokemonMoveID[] LearnableMoves { get; }
        public PSXAPI.Response.Payload.PokemonID CanEvolveTo { get; }
        internal Pokemon(InventoryPokemon data)
        {
            if (data != null)
            {
                PokemonData = data;
                Ability = new PokemonAbility(data.Pokemon.Ability, data.Pokemon.Payload.AbilitySlot);
                Stats = new PokemonStats(data.Pokemon.Stats);
                IV = new PokemonStats(data.Pokemon.Payload.IVs);
                EV = new PokemonStats(data.Pokemon.Payload.EVs);
                EVsCollected = new PokemonStats(data.Pokemon.Payload.EVsCollected);
                Id = PokemonManager.Instance.GetIdByName(data.Pokemon.Payload.PokemonID.ToString());
                Type1 = TypesManager.Instance.Type1[Id];
                Type2 = TypesManager.Instance.Type2[Id];
                Experience = new PokemonExperience
                    (data.Pokemon.Payload.Level, data.Pokemon.Payload.Exp,
                    data.Pokemon.ExpNext, data.Pokemon.ExpStart, 
                    PokemonManager.Instance.GetExpGroup(data.Pokemon.Payload.PokemonID.ToString()));
                OriginalTrainer = data.Pokemon.OriginalTrainer;
                _status = data.Pokemon.Payload.Condition.ToString();
                BattleMaxHealth = data.Pokemon.Stats.HP;
                BattleCurrentHealth = data.Pokemon.Payload.HP;
                LearnableMoves = data.CanLearnMove;
                CanEvolveTo = data.CanEvolve;
                if (data.Pokemon.Payload.Moves != null && data.Pokemon.Payload.Moves.Length > 0)
                {
                    Moves = new PokemonMove[data.Pokemon.Payload.Moves.Length];
                    var i = 0;
                    foreach(var move in data.Pokemon.Payload.Moves)
                    {
                        Moves[i] = new PokemonMove(data.Pokemon.Payload.Moves.ToList().IndexOf(move) + 1, MovesManager.Instance.GetMoveId(move.Move.ToString()), move.MaxPP, move.PP);
                        i++;
                    }
                }
                Uid = data.Position;
                Level = Experience.CurrentLevel;
                MaxHealth = data.Pokemon.Stats.HP;
                CurrentHealth = data.Pokemon.Payload.HP;
                Happiness = data.Pokemon.Payload.Happiness;
                ItemHeld = data.Pokemon.Payload.HoldItem <= 0 ? "" : ItemsManager.Instance.ItemClass.items.ToList().Find(x => x.ID == data.Pokemon.Payload.HoldItem)?.Name;
                IsShiny = data.Pokemon.Payload.Shiny;
                Gender = data.Pokemon.Payload.Gender.ToString();
                Nature = data.Pokemon.Payload.Nature.ToString().FirstOrDefault().ToString().ToUpperInvariant() + data.Pokemon.Payload.Nature.ToString().Substring(1);
                Name = PokemonManager.Instance.Names[Id];
            }
        }

        public void UpdateHealth(int current, int max)
        {
            BattleCurrentHealth = current;
            BattleMaxHealth = max;
        }

        public void UpdatePosition(int uid)
        {
            Uid = uid;
        }
    }
}

﻿using Common;
using Common.Database.World.Creatures;
using FrameWork;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using SystemData;
using WorldServer.Services.World;
using WorldServer.World.AI.Abilities;
using WorldServer.World.Interfaces;
using WorldServer.World.Objects;

namespace WorldServer.World.AI
{
    public class AggressiveBrain : ABrain
    {
        private static readonly long ABILITY_COOLDOWN = 5000;
        public long NextAbilityExecution { get; set; }
        public Conditions ConditionManager { get; set; }
        public Executions ExecutionManager { get; set; }
        public IEnumerable<CreatureSmartAbilities> Abilities { get; set; }
        public Dictionary<CreatureSmartAbilities, long> AbilityTracker { get; set; }

        private new static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public Creature_proto Proto { get; set; }

        public AggressiveBrain(Unit myOwner) : base(myOwner)
        {
            NextAbilityExecution = 0;
            ConditionManager = new Conditions(_unit, Combat);
            ExecutionManager = new Executions(_unit, Combat, this);
            Abilities = new List<CreatureSmartAbilities>();
            AbilityTracker = new Dictionary<CreatureSmartAbilities, long>();
            if (_unit is Creature)
                Proto = (_unit as Creature).Spawn.Proto;
            else
            {
                throw new Exception("_Unit is not Creature");
            }
        }

        public override void Think(long tick)
        {
            if (_unit.IsDead)
                return;

            base.Think(tick);

            // Only bother to seek targets if we're actually being observed by a player
            if (Combat.CurrentTarget == null && _unit.PlayersInRange.Count > 0)
            {
                if (_pet != null && (_pet.IsHeeling || ((CombatInterface_Pet)_pet.CbtInterface).IgnoreDamageEvents))
                    return;

                Unit target = _unit.AiInterface.GetAttackableUnit();
                if (target != null)
                    _unit.AiInterface.ProcessCombatStart(target);
            }

            if (_unit.IsCreature())
            {
                if (!Abilities.Any())
                {
                    Abilities = CreatureService.CreatureSmartAbilities
                        .Where(x => x.CreatureTypeId == Proto.CreatureType)
                        .Where(x => x.CreatureSubTypeId == Proto.CreatureSubType);
                }

                var target = Combat.GetCurrentTarget();
                if (target == null)
                    return;

                if (tick <= NextAbilityExecution)
                    return;

                lock (AbilityTracker)
                {
                    NextAbilityExecution = tick + ABILITY_COOLDOWN;
                }

                // Get abilities that can fire now.
                FilterAbilities(tick);

                ExecuteNextAbilityFromList(tick);
            }
        }

        private void FilterAbilities(long tick)
        {
            foreach (var ability in Abilities)
            {
                var t = ConditionManager.GetType();
                var method = t.GetMethod(ability.SpellCondition);
                _logger.Debug($"Checking condition: {ability.SpellCondition} ");
                if (method == null)
                {
                    _logger.Error($"Method is null: {ability.SpellCondition} ");
                    return;
                }
                var conditionTrue = (bool)method.Invoke(ConditionManager, null);
                if (conditionTrue)
                {
                    // If the ability is not in the ability tracker, add it
                    if (!AbilityTracker.ContainsKey(ability))
                    {
                        lock (AbilityTracker)
                        {
                            AbilityTracker.Add(ability, 0);
                        }

                        _logger.Debug($"Adding ability to the tracker : {AbilityTracker.Count} {ability.SpellCastName} 0");
                    }
                    else // If this ability is already in the abilitytracker  -- can probably remove this as it should be removed on execution.
                    {
                        long nextInvocation = 0;

                        // If it's next invocation > now, dont add.
                        AbilityTracker.TryGetValue(ability, out nextInvocation);
                        if (nextInvocation > tick)
                        {
                            // Do nothing
                        }
                    }
                }
                else
                {
                    // Condition is no longer true - remove it from the AbilityTracker
                    AbilityTracker.Remove(ability);
                }
            }
        }

        private void ExecuteNextAbilityFromList(long tick)
        {
            // This contains the list of abilities that can possibly be executed.
            var rand = StaticRandom.Instance.Next(1, 100);

            foreach (var keyValuePair in AbilityTracker)
            {
                _logger.Debug($"tick = {tick} ");
                if (keyValuePair.Value < tick)
                {
                    if (keyValuePair.Key.SpellExecuteChance >= rand)
                    {
                        var method = ExecutionManager.GetType().GetMethod(keyValuePair.Key.SpellCastExecution);
                        if (method == null)
                        {
                            _logger.Warn($"Could not locate method for execution : {keyValuePair.Key.SpellCastExecution}");
                            return;
                        }

                        _logger.Trace($"Executing  : {keyValuePair.Key.SpellCastName} => {keyValuePair.Value} ");

                        PerformSpeech(keyValuePair.Key);

                        PerformSound(keyValuePair.Key);

                        _logger.Debug($"Executing  : {keyValuePair.Key.SpellCastName} => {keyValuePair.Value} ");

                        lock (AbilityTracker)
                        {
                            // TODO : See if this is required, or can use ability cool down instead
                            AbilityTracker[keyValuePair.Key] = tick + keyValuePair.Key.SpellCastCoolDown * 1000;
                            _logger.Debug($"Next kv tick = {tick + keyValuePair.Key.SpellCastCoolDown * 1000} ");
                        }

                        try
                        {
                            method.Invoke(ExecutionManager, null);
                        }
                        catch (Exception e)
                        {
                            _logger.Error($"{e.Message} {e.StackTrace}");
                            throw;
                        }

                        _logger.Trace(
                            $"Updating the tracker : {keyValuePair.Key.SpellCastName} => {tick + keyValuePair.Key.SpellCastCoolDown * 1000} ");
                        _logger.Debug($"CoolDowns : {_unit.AbtInterface.Cooldowns.Count}");
                        break; // Leave the loop, come back on next tick
                    }

                    _logger.Debug($"Skipping : {keyValuePair.Key.SpellCastName} => {keyValuePair.Value} (random)");
                }
            }
        }

        public void PerformSound(CreatureSmartAbilities key)
        {
            if (!string.IsNullOrEmpty(key.SpellCastSound))
                foreach (var plr in GetClosePlayers(300))
                    plr.PlaySound(Convert.ToUInt16(key.SpellCastSound));
        }

        public void PerformSpeech(CreatureSmartAbilities key)
        {
            if (!string.IsNullOrEmpty(key.SpellCastSpeech))
                _unit.Say(key.SpellCastSpeech, ChatLogFilters.CHATLOGFILTERS_MONSTER_SAY);
        }

        public override void OnTaunt(Unit taunter, byte lvl)
        {
            if (_unit is Boss)
            {
                if ((_unit as Boss).CanBeTaunted)
                    base.OnTaunt(taunter, lvl);
            }
        }
    }
}
﻿using Common;
using WorldServer.World.Interfaces;

namespace WorldServer.World.Objects.Instances
{
    public class InstanceSpawn : Creature
    {
        private uint instanceGroupSpawnId = 0;
        private uint bossId = 0;
        private Instance Instance = null;

        public InstanceSpawn(Creature_spawn spawn, uint bossId, Instance instance) : base(spawn)
        {
            EvtInterface.AddEventNotify(EventName.OnEnterCombat, OnEnterCombat);
            EvtInterface.AddEventNotify(EventName.OnLeaveCombat, OnLeaveCombat);
        }

        public bool OnEnterCombat(Object mob, object args)
        {
            Unit Attacker = mob.GetCreature().CbtInterface.GetTarget(GameData.TargetTypes.TARGETTYPES_TARGET_ENEMY);
            if (Attacker == null)
                return false;
            if (instanceGroupSpawnId > 0)
            {
                Instance.AttackTarget(instanceGroupSpawnId, Attacker);
            }
            return false;
        }

        public bool OnLeaveCombat(Object mob, object args)
        {
            if (!mob.GetInstanceSpawn().IsDead && instanceGroupSpawnId > 0)
            {
                Instance.RespawnInstanceGroup(instanceGroupSpawnId);
            }
            return false;
        }

        protected override void SetRespawnTimer()
        {
        }

        public InstanceSpawn RezInstanceSpawn()
        {
            InstanceSpawn newCreature = new InstanceSpawn(Spawn, bossId, Instance);
            Region.AddObject(newCreature, Spawn.ZoneId);
            Destroy();
            return newCreature;
        }

        public override string ToString()
        {
            return "SpawnId=" + Spawn.Guid + ",Entry=" + Spawn.Entry + ",Spawngroup=" + instanceGroupSpawnId + ",LinkedBoss=" + bossId + ",Name=" + Name + ",Level=" + Level + ",Rank=" + Rank + ",Max Health=" + MaxHealth + ",Faction=" + Faction + ",Emote=" + Spawn.Emote + "AI:" + AiInterface.State + ",Position :" + base.ToString();
        }
    }
}
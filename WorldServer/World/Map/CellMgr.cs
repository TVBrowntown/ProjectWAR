﻿using Common;
using Common.Database.World.Maps;
using FrameWork;
using System.Collections.Generic;
using WorldServer.World.Objects;
using Object = WorldServer.World.Objects.Object;

namespace WorldServer.World.Map
{
    public class CellMgr
    {
        public RegionMgr Region { get; private set; }
        public ushort X { get; private set; }
        public ushort Y { get; private set; }
        public CellSpawns Spawns { get; private set; }
        public bool Active { get; private set; }
        public bool Loaded { get; private set; }

        public List<Object> Objects = new List<Object>();
        public List<Player> Players = new List<Player>();

        public CellMgr(RegionMgr mgr, ushort offX, ushort offY)
        {
            Region = mgr;
            X = offX;
            Y = offY;
            Spawns = mgr.GetCellSpawn(offX, offY);
            Active = false;
        }

        public void AddObject(Object obj)
        {
            if (obj is Player)
            {
                Players.Add((Player)obj);
                Region.LoadCells(X, Y, 1); // Load nearby cells when a player enters
                Active = true;
            }

            Objects.Add(obj);
            obj._Cell = this;
        }

        public void RemoveObject(Object obj)
        {
            //Log.Success("RemoveObject", "[" + X + "," + Y + "] Cell Remove " + Obj.Name);

            if (obj._Cell == this)
            {
                if (obj.IsPlayer())
                    Players.Remove(obj.GetPlayer());

                Objects.Remove(obj);
                obj._Cell = null;
                if (Players.Count == 0)
                    Active = false;
            }
        }

        public void Load()
        {
            lock (this)
            {
                if (Loaded)
                    return;

                Loaded = true;
            }

            Log.Debug(ToString(), "Loading... ");

            foreach (Creature_spawn spawn in Spawns.CreatureSpawns)
                Region.CreateCreature(spawn);

            foreach (GameObject_spawn spawn in Spawns.GameObjectSpawns)
                Region.CreateGameObject(spawn);

            foreach (Chapter_Info spawn in Spawns.ChapterSpawns)
                Region.CreateChapter(spawn);

            foreach (PQuest_Info quest in Spawns.PublicQuests)
                Region.CreatePQuest(quest);
        }

        public override string ToString()
        {
            return "CellMgr[" + X + "," + Y + "]";
        }
    }
}
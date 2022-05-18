using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sandbox;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using VRage;
using VRage.Game.Entity;

namespace BlockLimiter.Utility
{
    /// <summary>
    /// Thread safe wrapper to get entities
    /// </summary>
    public static class GridCache
    {
        private static readonly HashSet<MyCubeGrid> _gridCache = new HashSet<MyCubeGrid>();
        private static readonly HashSet<MySlimBlock> _blockCache = new HashSet<MySlimBlock>();
        private static readonly Dictionary<long, HashSet<long>> _bigBuilders = new Dictionary<long, HashSet<long>>();
        private static readonly Dictionary<long, HashSet<long>> _bigOwners = new Dictionary<long, HashSet<long>>();
        private static readonly HashSet<MyCubeGrid> _dirtyEntities = new HashSet<MyCubeGrid>();
        private static readonly FastResourceLock _gridLock = new FastResourceLock();
        private static readonly FastResourceLock _blockLock = new FastResourceLock();
        private static readonly FastResourceLock _builderLock = new FastResourceLock();
        private static readonly FastResourceLock _ownerLock = new FastResourceLock();


        static GridCache()
        {

        }

        public static void AddGrid(MyCubeGrid grid)
        {
            if (grid == null || _gridCache.Contains(grid) || grid.Projector != null) return;
            using (_gridLock.AcquireExclusiveUsing())
            {
                _gridCache.Add(grid);
               AddBlocks(grid.CubeBlocks);
            }
        }

        public static void RemoveGrid(MyCubeGrid grid)
        {
            using (_gridLock.AcquireExclusiveUsing())
            {
                _gridCache.Remove(grid);
                RemoveBlocks(grid.CubeBlocks);
            }
        }

        public static void AddBlock(MySlimBlock block)
        {
            if (block == null || _blockCache.Contains(block) || block.CubeGrid.Projector != null) return;
            using (_blockLock.AcquireExclusiveUsing())
            {
                _blockCache.Add(block);
            }
        }

        public static void RemoveBlock(MySlimBlock block)
        {
            if (block == null || !_blockCache.Contains(block)) return;
            using (_blockLock.AcquireExclusiveUsing())
            {
                _blockCache.Remove(block);
            }
        }

        private static void AddBlocks(HashSet<MySlimBlock> blocks)
        {
            if (blocks.Count == 0) return;
            using (_blockLock.AcquireExclusiveUsing())
            {
                _blockCache.UnionWith(blocks);
            }
        }

        public static void RemoveBlocks(HashSet<MySlimBlock> blocks)
        {
            using (_blockLock.AcquireExclusiveUsing())
            {
                foreach (var block in blocks)
                {
                    _blockCache.Remove(block);
                }
            }
        }
        
        public static bool TryGetBlockById(long entityId, out MySlimBlock entity)
        {
            using(_gridLock.AcquireSharedUsing())
            {
                entity = _blockCache.FirstOrDefault(e => e.FatBlock.EntityId == entityId);
                return entity != null;
            }
        }


        public static int Update()
        {
            if (Thread.CurrentThread != MySandboxGame.Static.UpdateThread)

            {
                throw new Exception("Update called from wrong thread");
            }

            using(_gridLock.AcquireExclusiveUsing())
            {
                var e = MyEntities.GetEntities();
                
                if (e.Count == 0) return 0;
                _gridCache.Clear();
                _blockCache.Clear();
                foreach (var g in e.OfType<MyCubeGrid>())
                {
                    if (g.Projector != null) continue;
                    _gridCache.Add(g);
                    AddBlocks(g.CubeBlocks);
                }
            }

            if (_gridCache.Count == 0) return _gridCache.Count;
            UpdateOwners();
            UpdateBuilders();
            return _gridCache.Count;
        }

        public static bool TryGetGridById(long entityId, out MyCubeGrid entity)
        {
            using(_gridLock.AcquireSharedUsing())
            {
                entity = _gridCache.FirstOrDefault(e => e.EntityId == entityId);
                return entity != null;
            }
        }


        public static void GetGrids(HashSet<MyCubeGrid> grids)
        {
            using(_gridLock.AcquireSharedUsing())
            {
                grids.UnionWith(_gridCache);
            }
        }

        public static void GetPlayerGrids(HashSet<MyCubeGrid> grids,long owner)
        {
            using (_gridLock.AcquireSharedUsing())
            {
                grids.UnionWith(_gridCache.Where(g=>g.BigOwners.Contains(owner)));
            }
        }

        public static void GetBlocks(HashSet<MySlimBlock> entities)
        {
            using(_blockLock.AcquireSharedUsing())
            {
                entities.UnionWith(_blockCache);
            }
        }

        public static int GetBlockCount()
        {
            using(_blockLock.AcquireSharedUsing())
            {
                return _blockCache.Count;
            }
        }

        public static void GetPlayerBlocks(HashSet<MySlimBlock> entities, long id)
        {
            using(_blockLock.AcquireSharedUsing())
            {
                entities.UnionWith(_blockCache.Where(x=>x?.OwnerId == id));
            }
        }

        public static void GetFactionBlocks(HashSet<MySlimBlock> entities, long id)
        {
            var faction = MySession.Static.Factions.TryGetFactionById(id);
            if (faction == null)return;
            using(_blockLock.AcquireSharedUsing())
            {
                entities.UnionWith(_blockCache.Where(x=>x?.FatBlock?.GetOwnerFactionTag() == faction.Tag));
            }
        }

        private static void UpdateOwners()
        {
            Parallel.ForEach(_dirtyEntities, g => UpdateGridOwners(g));
            _dirtyEntities.Clear();
            var rem = new HashSet<long>();

            using(_gridLock.AcquireSharedUsing())
            using (_ownerLock.AcquireSharedUsing())
            {
                rem.UnionWith(from e in _bigOwners where _gridCache.All(en => en?.EntityId != e.Key) select e.Key);
            }
            using (_ownerLock.AcquireExclusiveUsing())
            {
                foreach (var r in rem)
                    _bigOwners.Remove(r);
            }
        }

        private static HashSet<long> UpdateGridOwners(MyCubeGrid grid)
        {
            var owners = new Dictionary<long, int>();
            foreach (var block in grid.CubeBlocks)
            {
                owners.TryGetValue(block.OwnerId, out int c);
                owners[block.OwnerId] = c + 1;
            }

            int max = 0;
            var bigs = new HashSet<long>();

            foreach (var b in owners)
            {
                if (b.Key == 0) continue;
                if (b.Value > max)
                {
                    max = b.Value;
                    bigs.Clear();
                    bigs.Add(b.Key);
                }
                else if (b.Value == max)
                {
                    bigs.Add(b.Key);
                }
            }

            using(_ownerLock.AcquireExclusiveUsing())
                _bigOwners[grid.EntityId] = bigs;

            return bigs;
        } 
        
        //TODO: fix GetOwners.  Method returning nothing for block ownership.

        public static HashSet<long> GetOwners(MyCubeGrid grid)
        {
            HashSet<long> l;
            bool res;
            using(_ownerLock.AcquireSharedUsing())
                res = _bigOwners.TryGetValue(grid.EntityId, out l);

            if (res)
                return l;

            l = UpdateGridOwners(grid);
            grid.OnPhysicsChanged += Grid_OnPhysicsChanged;

            return l;
        }
        private static void UpdateBuilders()
        {
            Parallel.ForEach(_dirtyEntities, g => UpdateGridBuilders(g));
            _dirtyEntities.Clear();
            var rem = new HashSet<long>();

            using(_gridLock.AcquireSharedUsing())
            using (_builderLock.AcquireSharedUsing())
            {
                rem.UnionWith(from e in _bigBuilders where _gridCache.All(en => en?.EntityId != e.Key) select e.Key);
            }
            using (_builderLock.AcquireExclusiveUsing())
            {
                foreach (var r in rem)
                    _bigBuilders.Remove(r);
            }
        }
        
        public static HashSet<long> UpdateGridBuilders(MyCubeGrid grid)
        {
            var builders = new Dictionary<long, int>();
            foreach (var block in grid.CubeBlocks)
            {
                builders.TryGetValue(block.BuiltBy, out int c);
                builders[block.BuiltBy] = c + 1;
            }

            int max = 0;
            var bigs = new HashSet<long>();

            foreach (var b in builders)
            {
                if (b.Key == 0) continue;
                if (b.Value > max)
                {
                    max = b.Value;
                    bigs.Clear();
                    bigs.Add(b.Key);
                }
                else if (b.Value == max)
                {
                    bigs.Add(b.Key);
                }
            }

            using(_builderLock.AcquireExclusiveUsing())
                _bigBuilders[grid.EntityId] = bigs;

            return bigs;
        } 
        
        public static HashSet<long> GetBuilders(MyCubeGrid grid)
        {
            HashSet<long> l;
            bool res;
            using(_builderLock.AcquireSharedUsing())
                res = _bigBuilders.TryGetValue(grid.EntityId, out l);

            if (res)
                return l;

            l = UpdateGridBuilders(grid);
            grid.OnPhysicsChanged += Grid_OnPhysicsChanged;

            return l;
        }

        private static void Grid_OnPhysicsChanged(MyEntity obj)
        {
            _dirtyEntities.Add((MyCubeGrid)obj);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlockLimiter.Patch;
using Sandbox;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using VRage;
using VRage.Collections;
using VRage.Game.Entity;
using VRage.ModAPI;

namespace BlockLimiter.Utility
{
    /// <summary>
    /// Thread safe wrapper to get entities
    /// </summary>
    public static class GridCache
    {
        private static readonly HashSet<MyCubeGrid> _gridCache = new HashSet<MyCubeGrid>();
        private static readonly Dictionary<long, List<long>> _bigBuilders = new Dictionary<long, List<long>>();
        private static readonly HashSet<MyCubeGrid> _dirtyEntities = new HashSet<MyCubeGrid>();
        private static int _updateCounter;
        private static readonly FastResourceLock _entityLock = new FastResourceLock();
        private static readonly FastResourceLock _builderLock = new FastResourceLock();

       static GridCache()
        {
            BlockOwnershipTransfer.SlimOwnerChanged += SlimOwnerChanged;
        }

        private static void SlimOwnerChanged(MySlimBlock arg1, long arg2)
        {
            _dirtyEntities.Add(arg1.CubeGrid);
        }

        public static void Update()
        {
            if(Thread.CurrentThread != MySandboxGame.Static.UpdateThread)
                throw new Exception("Update called from wrong thread");

            using(_entityLock.AcquireExclusiveUsing())
            {
                var e = MyEntities.GetEntities();
                
                if (e.Count > 0)
                {
                    _gridCache.Clear();
                    _gridCache.UnionWith(e.OfType<MyCubeGrid>().Where(x=>x.Projector == null));
                }
            }

            if (++_updateCounter % 100 == 0)
            {
                UpdateBuilders();
            }
        }

        public static bool TryGetGridById(long entityId, out MyCubeGrid entity)
        {
            using(_entityLock.AcquireSharedUsing())
            {
                entity = _gridCache.FirstOrDefault(e => e.EntityId == entityId);
                return entity != null;
            }
        }

        public static void RemoveGrid(long entityId)
        {
            
            using(_entityLock.AcquireSharedUsing())
            {
                var entity = _gridCache.FirstOrDefault(e => e.EntityId == entityId);

                _gridCache.Remove(entity);
            }
        }

        public static void GetGrids(HashSet<MyCubeGrid> grids)
        {
            using(_entityLock.AcquireSharedUsing())
            {
                grids.UnionWith(_gridCache);
            }
        }
        
        public static void GetBlocks(HashSet<MySlimBlock> entities)
        {
            using(_entityLock.AcquireSharedUsing())
            {
                entities.UnionWith(_gridCache.SelectMany(g=>g.CubeBlocks));
            }
        }

        private static void UpdateBuilders()
        {
            Parallel.ForEach(_dirtyEntities, g => UpdateGridBuilders(g));
            _dirtyEntities.Clear();
                var rem = new HashSet<long>();

            using(_entityLock.AcquireSharedUsing())
            using (_builderLock.AcquireSharedUsing())
            {
                rem.UnionWith(from e in _bigBuilders where _gridCache.All(en => en.EntityId != e.Key) select e.Key);
            }
            using (_builderLock.AcquireExclusiveUsing())
            {
                foreach (var r in rem)
                    _bigBuilders.Remove(r);
            }
        }
        
        private static List<long> UpdateGridBuilders(MyCubeGrid grid)
        {
            var builders = new Dictionary<long, int>();
            foreach (var block in grid.CubeBlocks)
            {
                builders.TryGetValue(block.BuiltBy, out int c);
                builders[block.BuiltBy] = c + 1;
            }

            int max = 0;
            var bigs = new List<long>();

            foreach (var b in builders)
            {
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
        
        public static List<long> GetBuilders(MyCubeGrid grid)
        {
            List<long> l;
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

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlockLimiter.Settings;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;

namespace BlockLimiter.Utility
{
    public static class UpdateLimits
    {

        private static readonly Logger Log = BlockLimiter.Instance.Log;

        public static bool PlayerLimit(long id)
        {
            if (id == 0) return false;
            var playerBlocks = new HashSet<MySlimBlock>();

            GridCache.GetPlayerBlocks(playerBlocks,id);

            var limits = BlockLimiterConfig.Instance.AllLimits;
            if (limits?.Count == 0) return false;
            if (playerBlocks.Count == 0)
            {
                foreach (var limit in BlockLimiterConfig.Instance.AllLimits)
                {
                    limit.FoundEntities.Remove(id);
                }
                return false;
            }

            Parallel.ForEach(BlockLimiterConfig.Instance.AllLimits, limit =>
            {
                if (!limit.LimitPlayers)
                {
                    limit.FoundEntities.Remove(id);
                    return;
                }
                var limitedBlocks = playerBlocks.Count(x =>
                    limit.IsMatch(x.BlockDefinition));
                if (limitedBlocks == 0)
                {
                    limit.FoundEntities.Remove(id);
                    return;
                }

                limit.FoundEntities[id] = limitedBlocks;


            });

            return true;
        }

        
        public static void GridLimit(MyCubeGrid grid)
        {
            if (grid == null) return;
            
            var blocks = new HashSet<MySlimBlock>();
            blocks.UnionWith(grid.CubeBlocks);
            //adding all blocks from subGrids to count
            //blocks.UnionWith(Grid.GetGridsInGroup(grid).SelectMany(x=>x.CubeBlocks));

            if (blocks.Count == 0)
            {
                return;
            }

            Parallel.ForEach(BlockLimiterConfig.Instance.AllLimits, limit =>
            {
                if (!limit.LimitGrids || !limit.IsGridType(grid))
                {
                    limit.FoundEntities.Remove(grid.EntityId);
                    return;
                }
                
                var limitedBlocks = blocks.Count(x => limit.IsMatch(x.BlockDefinition));

                if (limitedBlocks == 0)
                {
                    limit.FoundEntities.Remove(grid.EntityId);
                    return;
                }
                limit.FoundEntities[grid.EntityId] = limitedBlocks;

            });

        }

        
        public static bool FactionLimit(long id)
        {
            if (id == 0) return false;
            var factionBlocks = new HashSet<MySlimBlock>();
            var limits = BlockLimiterConfig.Instance.AllLimits;

            GridCache.GetFactionBlocks(factionBlocks,id);

            if (factionBlocks.Count == 0)
            {
                foreach (var limit in limits)
                {
                    limit.FoundEntities.Remove(id);  
                }
                return false;
            }


            var faction = MySession.Static.Factions.TryGetFactionById(id);
            if (faction == null) return false;

            Parallel.ForEach(limits, limit =>
            {
                if (!limit.LimitFaction)
                {
                    limit.FoundEntities.Remove(id);
                    return;
                }

                var factionBlockCount = factionBlocks.Count(x => limit.IsMatch(x.BlockDefinition));

                if (factionBlockCount == 0)
                {
                    limit.FoundEntities.Remove(id);
                    return;
                }

                limit.FoundEntities[id] = factionBlockCount;

            });
            return true;
        }
        
    }
}
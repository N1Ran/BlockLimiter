using System.Collections.Generic;
using System.Linq;
using BlockLimiter.Settings;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using SpaceEngineers.Game.Entities.Blocks;

namespace BlockLimiter.Utility
{
    public static class UpdateLimits
    {
        
        
        public static void PlayerLimit(long id)
        {
            if (id == 0) return;
            var blockCache = new HashSet<MySlimBlock>();
            var playerBlocks = new HashSet<MySlimBlock>();
            
            var faction = MySession.Static.Factions.GetPlayerFaction(id);
            
            GridCache.GetBlocks(blockCache);
            if (blockCache.Count < 1)
                return;
            playerBlocks.UnionWith(blockCache.Where(x=>x.OwnerId == id || x.BuiltBy == id));
            
            if (playerBlocks.Count == 0) return;
            
            foreach (var limit in BlockLimiterConfig.Instance.AllLimits)
            {
                if (!limit.LimitPlayers 
                    || Utilities.IsExcepted(id, limit.Exceptions) 
                    || faction != null && Utilities.IsExcepted(faction.FactionId, limit.Exceptions)) continue;

                var limitedBlocks = playerBlocks.Count(x =>
                    Block.IsMatch(x.BlockDefinition, limit));
                if (limitedBlocks == 0) continue;
                limit.FoundEntities[id] = limitedBlocks;
                
            }
        }

        public static void PlayerLimit(MyPlayer player)
        {
            if (player?.Identity?.IdentityId == null)
            {
                BlockLimiter.Instance.Log.Warn("Attempt to update null player");
                return;
            }
            if (player.Identity != null) PlayerLimit(player.Identity.IdentityId);
        }
        
        
        
        public static void GridLimit(MyCubeGrid grid)
        {
            var blocks = grid.CubeBlocks;
            foreach (var limit in BlockLimiterConfig.Instance.AllLimits)
            {
                if (!limit.LimitGrids || !Grid.IsGridType(grid,limit))
                {
                    limit.FoundEntities.Remove(grid.EntityId);
                    continue;
                }

                var limitedBlocks = blocks.Count(x => Block.IsMatch(x.BlockDefinition, limit));
                
                if (limitedBlocks < 1)continue;
                limit.FoundEntities[grid.EntityId] = limitedBlocks;
            }
        }

        
        public static void FactionLimit(long id)
        {
            if (id == 0) return;
            var blockCache = new HashSet<MySlimBlock>();
            var factionBlocks = new HashSet<MySlimBlock>();
            
            var faction = MySession.Static.Factions.TryGetFactionById(id);
            
            if (faction == null) return;
            
            GridCache.GetBlocks(blockCache);
            if (blockCache.Count == 0)
                return;
            
            factionBlocks.UnionWith(blockCache.Where(x => x.FatBlock?.GetOwnerFactionTag() == faction.Tag));
            
            if (factionBlocks.Count == 0) return;

            foreach (var limit in BlockLimiterConfig.Instance.AllLimits)
            {
                if (!limit.LimitFaction || Utilities.IsExcepted(faction.FactionId, limit.Exceptions)) continue;
                var factionBlockCount = factionBlocks.Count(x => Block.IsMatch(x.BlockDefinition, limit));
                limit.FoundEntities[id] = factionBlockCount;
            }
        }

    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using BlockLimiter.Settings;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;

namespace BlockLimiter.Utility
{
    public static class Block
    {
        public static bool AllowBlock(MyCubeBlockDefinition block, long playerId, MyCubeGrid grid = null)
        {
            var nope = false;
            var blockCache = new HashSet<MySlimBlock>();
            GridCache.GetBlocks(blockCache);

            var faction = MySession.Static.Factions.GetPlayerFaction(playerId);

            foreach (var item in BlockLimiterConfig.Instance.AllLimits)
            {
                if (!item.BlockList.Any() || !IsMatch(block, item)) continue;

                if (item.Exceptions.Any())
                {
                    if (item.Exceptions.Contains(playerId.ToString()))
                    {
                        continue;
                    }
                    if (faction != null && item.Exceptions.Contains(faction.Tag))
                    {
                        continue;
                    }
                    var playerSteamId = MyAPIGateway.Multiplayer.Players.TryGetSteamId(playerId);
                    if (item.Exceptions.Contains(playerSteamId.ToString()))
                    {
                        continue;
                    }
                    if (item.Exceptions.Contains(MySession.Static.Players.TryGetIdentityNameFromSteamId(playerSteamId)))
                    {
                        continue;
                    }
                    if (grid != null && (item.Exceptions.Contains(grid.EntityId.ToString()) || item.Exceptions.Contains(grid.DisplayName)))
                    {
                        continue;
                    }
                }
                
                if (playerId > 0 && item.LimitPlayers)
                {
                    var filteredBlocksCount = blockCache.Count(x=> IsMatch(x.BlockDefinition,item) && IsOwner(item.BlockOwnerState, x, playerId));
                    if (filteredBlocksCount >= item.Limit)
                    {
                        nope = true;
                        break;
                    }
                }

                if (grid != null && item.LimitGrids)
                {
                    if (!Grid.IsGridType(grid, item)) continue;
                    var subGrids = MyAPIGateway.GridGroups.GetGroup(grid, GridLinkTypeEnum.Physical);

                    var filteredBlocksCount = blockCache.Count(x=> x.CubeGrid == grid && IsMatch(x.BlockDefinition,item) && IsOwner(item.BlockOwnerState, x, playerId));
                    if (filteredBlocksCount >= item.Limit)
                    {
                        nope = true;
                        break;
                    }

                    if (subGrids.Any())
                    {
                        foreach (var subGrid in subGrids)
                        {
                            var subGridBlockCount = blockCache.Count(x=> x.CubeGrid == subGrid && IsMatch(x.BlockDefinition,item) && IsOwner(item.BlockOwnerState, x, playerId));
                            if (subGridBlockCount >= item.Limit)
                            {
                                nope = true;
                                break;
                            }
                        }
                    }
                }

                else
                {
                    if (item.Limit == 0)
                    {
                        nope = true;
                        break;
                    }
                }

                if (faction != null && item.LimitFaction)
                {
                    var filteredBlocksCount = blockCache.Count(x =>
                        IsMatch(x.BlockDefinition,item) &&  x.FatBlock.GetOwnerFactionTag() == faction.Tag);
                    var overCount = filteredBlocksCount - item.Limit;
                    if (filteredBlocksCount >= item.Limit)
                    {
                        nope = true;
                        break;
                    }
                }
                
            }

            return !nope;
            
        }
        
        public static bool AllowBlock(MyCubeBlockDefinition block, long playerId, MyObjectBuilder_CubeGrid grid = null)
        {
            
            var nope = false;
            var blockCache = new HashSet<MySlimBlock>();
            GridCache.GetBlocks(blockCache);

            var faction = MySession.Static.Factions.GetPlayerFaction(playerId);

            foreach (var item in BlockLimiterConfig.Instance.AllLimits)
            {
                if (!item.BlockList.Any() || !IsMatch(block, item)) continue;
                
                if (grid != null && (faction != null && (Utilities.IsExcepted(playerId,item.Exceptions) || Utilities.IsExcepted(faction.FactionId,item.Exceptions) || Utilities.IsExcepted(grid.EntityId,item.Exceptions))))
                    continue;

                
                if (playerId != 0 && item.LimitPlayers)
                {
                    var filteredBlocksCount = blockCache.Count(x=> IsMatch(x.BlockDefinition,item) && IsOwner(item.BlockOwnerState, x, playerId));

                    if (filteredBlocksCount >= item.Limit)
                    {
                        nope = true;
                        break;
                    }
                }

                if (grid != null && item.LimitGrids)
                {
                    if (!Grid.IsGridType(grid, item)) continue;
                    var filteredBlocksCount = blockCache.Count(x=> x.CubeGrid.EntityId == grid.EntityId && IsMatch(x.BlockDefinition,item) && IsOwner(item.BlockOwnerState, x, playerId));
                    
                    if (filteredBlocksCount >= item.Limit)
                    {
                        nope = true;
                        break;
                    }
                    
                }

                else
                {
                    if (item.Limit == 0)
                    {
                        nope = true;
                        break;
                    }
                }

                if (faction != null && item.LimitFaction)
                {
                    var filteredBlocksCount = blockCache.Count(x =>
                        IsMatch(x.BlockDefinition,item) &&  x.FatBlock.GetOwnerFactionTag() == faction.Tag);
                    var overCount = filteredBlocksCount - item.Limit;

                    if (filteredBlocksCount >= item.Limit)
                    {
                        nope = true;
                        break;
                    }
                }
                
                
            }

            return !nope;
            
        }
        

        public static bool IsOwner(LimitItem.OwnerState state, MySlimBlock block, long playerId)
        {
            bool correctOwner;
            switch (state)
            {
                case LimitItem.OwnerState.BuiltbyId:
                    correctOwner = block.BuiltBy == playerId;
                    break;
                case LimitItem.OwnerState.OwnerId:
                    correctOwner = block.OwnerId == playerId;
                    break;
                case LimitItem.OwnerState.OwnerAndBuiltbyId:
                    correctOwner = block.OwnerId == playerId && block.BuiltBy == playerId;
                    break;
                case LimitItem.OwnerState.OwnerOrBuiltbyId:
                    correctOwner = block.OwnerId == playerId || block.BuiltBy == playerId;
                    break;
                default:
                    correctOwner = false;
                    break;
            }

            return correctOwner;
        }

        public static bool IsMatch(MyCubeBlockDefinition block, LimitItem item)
        {
            if (!item.BlockList.Any()) return false;
            return item.BlockList.Any(x => x.Equals(block.Id.SubtypeId.ToString(), StringComparison.OrdinalIgnoreCase)) || item.BlockList.Any(x =>
                x.Equals(block.Id.TypeId.ToString().Substring(16), StringComparison.OrdinalIgnoreCase)) || 
                   item.BlockList.Any(x=>x.Equals(block.BlockPairName,StringComparison.OrdinalIgnoreCase));
        }
        
        
        public static bool TryAdd(MyCubeBlockDefinition block, long id)
        {

            if (!GridCache.TryGetGridById(id, out var grid))
            {
                var identity = MySession.Static.Players.TryGetIdentity(id);

                if (identity == null) return false;
                
                var faction = MySession.Static.Factions.GetPlayerFaction(id);

                foreach (var limit in BlockLimiterConfig.Instance.AllLimits.Where(limit => IsMatch(block, limit)))
                {
                    if (limit.LimitFaction && faction != null)
                    {
                        limit.FoundEntities.AddOrUpdate(id, 1, (l, i) => i + 1);
                    }

                    if (limit.LimitPlayers)
                    {
                        limit.FoundEntities.AddOrUpdate(id, 1, (l, i) => i + 1);
                    }
                }

                return true;
            }


            foreach (var limit in BlockLimiterConfig.Instance.AllLimits.Where(limit => IsMatch(block, limit)))
            {
                if (!limit.LimitGrids) continue;
                
                limit.FoundEntities.AddOrUpdate(id, 1, (l, i) => i + 1);
            }

            return true;


        }

        public static bool TryAddBlock(MyCubeBlockDefinition definition, long id, int amount = 1)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits) return false;
            
            for (int i = 0; i < amount; i++)
            {
                TryAdd(definition, id);
            }

            return true;
        }

        public static void RemoveBlock(MySlimBlock block)
        {
            var blockDef = block?.FatBlock?.BlockDefinition;
            
            if (blockDef == null) return;
            
            var blockOwner = block.OwnerId;
            var blockBuilder = block.BuiltBy;
            var blockGrid = block.CubeGrid.EntityId;
            var faction = MySession.Static.Factions.TryGetFactionByTag(block.FatBlock.GetOwnerFactionTag())?.FactionId;

            foreach (var limit in BlockLimiterConfig.Instance.AllLimits.Where(x => IsMatch(blockDef, x)))
            {
                if (limit.LimitGrids && blockGrid > 0)
                {
                    limit.FoundEntities.AddOrUpdate(blockGrid, 0, (l, i) => Math.Max(0,i - 1));
                }

                if (limit.LimitPlayers)
                {
                    if (blockOwner > 0 && IsOwner(limit.BlockOwnerState,block, blockOwner))
                        limit.FoundEntities.AddOrUpdate(blockOwner, 0, (l, i) => Math.Max(0,i - 1));
                    if (blockBuilder > 0 && IsOwner(limit.BlockOwnerState,block, blockBuilder))
                        limit.FoundEntities.AddOrUpdate(blockBuilder, 0, (l, i) => Math.Max(0,i - 1));
                }

                if (limit.LimitFaction && faction != null)
                {
                    limit.FoundEntities.AddOrUpdate((long)faction, 0, (l, i) => Math.Max(0,i - 1));
                }
            }


        }

        public static void UpdateFactionLimits(long id)
        {
            var faction = MySession.Static.Factions.TryGetFactionById(id);
            if (faction == null) return;
            
            var blocks = new HashSet<MySlimBlock>();
            GridCache.GetBlocks(blocks);
            if (!blocks.Any()) return;

            foreach (var limit in BlockLimiterConfig.Instance.AllLimits)
            {
                if (!limit.LimitFaction) continue;
                var factionBlockCount = blocks.Count(x =>
                    x.FatBlock.GetOwnerFactionTag() == faction.Tag && Block.IsMatch(x.BlockDefinition, limit));
                limit.FoundEntities[id] = factionBlockCount;
            }
        }

        public static void UpdatePlayerLimits(long id)
        {
            if (id == 0) return;
            BlockLimiter.Instance.Log.Info($"Checking {id}");
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
                    IsMatch(x.BlockDefinition, limit) &&
                    IsOwner(limit.BlockOwnerState, x, id));
                if (limitedBlocks < 1) continue;
                limit.FoundEntities[id] = limitedBlocks;
                
            }
        }

        public static void UpdatePlayerLimits(MyPlayer player)
        {
            if (player?.Identity?.IdentityId == null)
            {
                BlockLimiter.Instance.Log.Warn("Attempt to update null player");
                return;
            }
            if (player.Identity != null) UpdatePlayerLimits(player.Identity.IdentityId);
        }

    }
}
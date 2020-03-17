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
                if (!item.BlockPairName.Any() || !IsMatch(block, item)) continue;

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
                if (!item.BlockPairName.Any() || !IsMatch(block, item)) continue;

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
            var correctOwner = false;
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
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }

            return correctOwner;
        }

        public static bool IsMatch(MyCubeBlockDefinition block, LimitItem item)
        {
            var typeMatch = false;
            
            if (item.UseBlockType)
                typeMatch = item.BlockPairName.Count > 0 && item.BlockPairName.Any(x =>
                                x.Equals(block.Id.TypeId.ToString().Substring(16), StringComparison.OrdinalIgnoreCase));
            return typeMatch || item.BlockPairName.Count> 0 && item.BlockPairName.Any(x=>x.Equals(block.BlockPairName,StringComparison.OrdinalIgnoreCase));
        }
        
        //Projector
        public static bool ProjectBlock(MyObjectBuilder_CubeBlock block, long playerId, MyCubeGrid grid = null)
        {
            
            var project = true;

            var faction = MySession.Static.Factions.GetPlayerFaction(playerId);
            var blockDef = Utilities.GetDefinition(block);
            foreach (var item in BlockLimiterConfig.Instance.LimitItems)
            {
                if (!item.RestrictProjection) continue;
                if (!item.BlockPairName.Any() || !IsMatch(blockDef, item)) continue;

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
                    if (item.FoundEntities.TryGetValue(playerId, out var count))
                    {
                        if (count >= 0 || item.Limit == 0)
                        {
                            project = false;
                            break;
                        }
                    }
                }

                if (grid != null && item.LimitGrids)
                {
                    if (Grid.IsGridType(grid, item))
                    {
                        if (item.Limit == 0)
                        {
                            project = false;
                            break;
                        }
                        var filteredBlocksCount =
                            grid.CubeBlocks.Count(x => x.BlockDefinition == blockDef);

                        if (filteredBlocksCount >= item.Limit || item.Limit == 0)
                        {
                            project = false;
                            break;
                        }
                    }
                    
                }

                if (faction != null && item.LimitFaction)
                {
                    if (item.FoundEntities.TryGetValue(faction.FactionId, out var count))
                    {
                        if (count >= 0|| item.Limit == 0)
                        {
                            project = false;
                            break;
                        }
                    }
                }
                
            }

            return project;
            
        }

        public static bool ProjectBlock(MyObjectBuilder_CubeBlock block, long playerId, MyObjectBuilder_CubeGrid grid)
        {
            
            var project = true;

            var faction = MySession.Static.Factions.GetPlayerFaction(playerId);
            var blockDef = Utilities.GetDefinition(block);

            foreach (var item in BlockLimiterConfig.Instance.LimitItems)
            {
                if (!item.RestrictProjection) continue;
                if (!item.BlockPairName.Any() || !IsMatch(blockDef, item)) continue;

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
                
                if (playerId != 0 && item.LimitPlayers)
                {
                    if (item.FoundEntities.TryGetValue(playerId, out var count))
                    {
                        if (count >= 0 || item.Limit == 0)
                        {
                            project = false;
                            break;
                        }
                    }
                }

                if (grid != null && item.LimitGrids)
                {
                    if (Grid.IsGridType(grid, item))
                    {
                        if (item.Limit == 0)
                        {
                            project = false;
                            break;
                        }
                        
                        var filteredBlocksCount =
                            grid.CubeBlocks.Count(x => x.SubtypeId == blockDef.Id.SubtypeId);

                        if (filteredBlocksCount >= item.Limit || item.Limit == 0)
                        {
                            project = false;
                            break;
                        }
                    }
                    
                }

                if (faction != null && item.LimitFaction)
                {
                    if (!item.FoundEntities.TryGetValue(faction.FactionId, out var count)) continue;
                    if (count >= 0|| item.Limit == 0)
                    {
                        project = false;
                        break;
                    }
                }
                
            }

            return project;
            
        }
        
        public static void Add(MyCubeBlockDefinition block, long id)
        {
            if (!Utilities.TryGetEntityByNameOrId(id.ToString(), out var entity))
            {
                var faction = MySession.Static.Factions.TryGetFactionById(id);
                if (!MySession.Static.Players.TryGetPlayerId(id, out var player) && faction == null)
                    return;

                foreach (var limit in BlockLimiterConfig.Instance.AllLimits.Where(limit => Block.IsMatch(block, limit)))
                {
                    if (limit.LimitFaction && faction != null)
                    {
                        limit.FoundEntities.AddOrUpdate(id, 1, (l, i) => i + 1);
                    }

                    if (limit.LimitPlayers && player.IsValid)
                    {
                        limit.FoundEntities.AddOrUpdate(id, 1, (l, i) => i + 1);
                        if (limit.LimitFaction)
                        {
                            var playerFaction = MySession.Static.Factions.GetPlayerFaction(id);
                            if (playerFaction == null) continue;
                            limit.FoundEntities.AddOrUpdate(playerFaction.FactionId, 1, (l, i) => i + 1);
                        }
                    }
                }
            }

            foreach (var limit in BlockLimiterConfig.Instance.AllLimits.Where(limit => Block.IsMatch(block, limit)))
            {
                if (limit.LimitGrids && entity is MyCubeGrid grid)
                {
                    limit.FoundEntities.AddOrUpdate(id, 1, (l, i) => i + 1);
                    continue;
                }
                
                if (!(entity is MyCharacter)) continue;

                var playerFaction = MySession.Static.Factions.GetPlayerFaction(id);

                if (limit.LimitPlayers)
                {
                    limit.FoundEntities.AddOrUpdate(id, 1, (l, i) => i + 1);
                }

                if (limit.LimitFaction && playerFaction != null)
                {
                    limit.FoundEntities.AddOrUpdate(playerFaction.FactionId, 1, (l, i) => i + 1);
                }

            }
        }

        public static void RemoveBlock(MySlimBlock block)
        {
            return;
            if (block == null) return;
            var blockDef = block.BlockDefinition;
            var blockOwner = block.OwnerId;
            var blockBuilder = block.BuiltBy;
            var blockGrid = block.CubeGrid.EntityId;
            var faction = MySession.Static.Factions.TryGetFactionByTag(block.FatBlock.GetOwnerFactionTag())?.FactionId;

            foreach (var limit in BlockLimiterConfig.Instance.AllLimits.Where(x => Block.IsMatch(blockDef, x)))
            {
                if (limit.LimitGrids)
                {
                    limit.FoundEntities.AddOrUpdate(blockGrid, 0, (l, i) => Math.Max(0,i - 1));
                }

                if (limit.LimitPlayers)
                {
                    if (IsOwner(limit.BlockOwnerState,block, blockOwner))
                        limit.FoundEntities.AddOrUpdate(blockOwner, 0, (l, i) => Math.Max(0,i - 1));
                    if (IsOwner(limit.BlockOwnerState,block, blockBuilder))
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

        public static void UpdatePlayerLimits(MyPlayer player)
        {
            var playerGrids = player.Grids;
            var playerBlocks = new HashSet<MySlimBlock>();
            if (playerGrids.Count < 1) return;
            
            foreach (var id in playerGrids)
            {
               if (!GridCache.TryGetGridById(id, out var grid))continue;
               Grid.UpdateLimit(grid);
               var blocks = grid.CubeBlocks;
               playerBlocks.UnionWith(blocks);
            }

            foreach (var limit in BlockLimiterConfig.Instance.AllLimits)
            {
                if (!limit.LimitPlayers) continue;

                var limitedBlocks = playerBlocks.Count(x =>
                    IsMatch(x.BlockDefinition, limit) &&
                    IsOwner(limit.BlockOwnerState, x, player.Identity.IdentityId));
                if (limitedBlocks <1) continue;
                limit.FoundEntities[player.Identity.IdentityId] = limitedBlocks;
                
            }
        }



    }
}
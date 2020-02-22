using System;
using System.Collections.Generic;
using System.Linq;
using BlockLimiter.Settings;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
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
                        item.FoundEntities.Remove(playerId);
                        continue;
                    }
                    if (faction != null && item.Exceptions.Contains(faction.Tag))
                    {
                        item.FoundEntities.Remove(faction.FactionId);
                        continue;
                    }
                    var playerSteamId = MyAPIGateway.Multiplayer.Players.TryGetSteamId(playerId);
                    if (item.Exceptions.Contains(playerSteamId.ToString()))
                    {
                        item.FoundEntities.Remove(playerId);
                        continue;
                    }
                    if (item.Exceptions.Contains(MySession.Static.Players.TryGetIdentityNameFromSteamId(playerSteamId)))
                    {
                        item.FoundEntities.Remove(playerId);
                        continue;
                    }
                    if (grid != null && (item.Exceptions.Contains(grid.EntityId.ToString()) || item.Exceptions.Contains(grid.DisplayName)))
                    {
                        item.FoundEntities.Remove(grid.EntityId);
                        continue;
                    }
                }
                
                if (playerId != 0 && item.LimitPlayers)
                {
                    var filteredBlocksCount = blockCache.Count(x=> x.BlockDefinition == block && IsOwner(item.BlockOwnerState, x, playerId));
                    var overCount = filteredBlocksCount - item.Limit;
                    item.FoundEntities[playerId] = overCount;
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

                    var filteredBlocksCount = blockCache.Count(x=> x.CubeGrid == grid && x.BlockDefinition == block && IsOwner(item.BlockOwnerState, x, playerId));
                    var overCount = filteredBlocksCount - item.Limit;
                    item.FoundEntities[grid.EntityId] = overCount;
                    if (filteredBlocksCount >= item.Limit)
                    {
                        nope = true;
                        break;
                    }

                    if (subGrids.Any())
                    {
                        foreach (var subGrid in subGrids)
                        {
                            var subGridBlockCount = blockCache.Count(x=> x.CubeGrid == subGrid && x.BlockDefinition == block && IsOwner(item.BlockOwnerState, x, playerId));
                            var subOverCount = filteredBlocksCount - item.Limit;
                            item.FoundEntities[subGrid.EntityId] = subOverCount;
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
                        x.BlockDefinition == block &&  x.FatBlock.GetOwnerFactionTag() == faction.Tag);
                    var overCount = filteredBlocksCount - item.Limit;
                    item.FoundEntities[faction.FactionId] = overCount;
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
                        item.FoundEntities.Remove(playerId);
                        continue;
                    }
                    if (faction != null && item.Exceptions.Contains(faction.Tag))
                    {
                        item.FoundEntities.Remove(faction.FactionId);
                        continue;
                    }
                    var playerSteamId = MyAPIGateway.Multiplayer.Players.TryGetSteamId(playerId);
                    if (item.Exceptions.Contains(playerSteamId.ToString()))
                    {
                        item.FoundEntities.Remove(playerId);
                        continue;
                    }
                    if (item.Exceptions.Contains(MySession.Static.Players.TryGetIdentityNameFromSteamId(playerSteamId)))
                    {
                        item.FoundEntities.Remove(playerId);
                        continue;
                    }
                    if (grid != null && (item.Exceptions.Contains(grid.EntityId.ToString()) || item.Exceptions.Contains(grid.DisplayName)))
                    {
                        item.FoundEntities.Remove(grid.EntityId);
                        continue;
                    }
                }
                
                if (playerId != 0 && item.LimitPlayers)
                {
                    var filteredBlocksCount = blockCache.Count(x=> x.BlockDefinition == block && IsOwner(item.BlockOwnerState, x, playerId));
                    var overCount = filteredBlocksCount - item.Limit;
                    item.FoundEntities[playerId] = overCount;

                    if (filteredBlocksCount >= item.Limit)
                    {
                        nope = true;
                        break;
                    }
                }

                if (grid != null && item.LimitGrids)
                {
                    if (!Grid.IsGridType(grid, item)) continue;
                    var filteredBlocksCount = blockCache.Count(x=> x.CubeGrid.EntityId == grid.EntityId && x.BlockDefinition == block && IsOwner(item.BlockOwnerState, x, playerId));
                    var overCount = filteredBlocksCount - item.Limit;
                    item.FoundEntities[grid.EntityId] = overCount;
                    
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
                        x.BlockDefinition == block &&  x.FatBlock.GetOwnerFactionTag() == faction.Tag);
                    var overCount = filteredBlocksCount - item.Limit;
                    item.FoundEntities[faction.FactionId] = overCount;

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
                        item.FoundEntities.Remove(playerId);
                        continue;
                    }
                    if (faction != null && item.Exceptions.Contains(faction.Tag))
                    {
                        item.FoundEntities.Remove(faction.FactionId);
                        continue;
                    }
                    var playerSteamId = MyAPIGateway.Multiplayer.Players.TryGetSteamId(playerId);
                    if (item.Exceptions.Contains(playerSteamId.ToString()))
                    {
                        item.FoundEntities.Remove(playerId);
                        continue;
                    }
                    if (item.Exceptions.Contains(MySession.Static.Players.TryGetIdentityNameFromSteamId(playerSteamId)))
                    {
                        item.FoundEntities.Remove(playerId);
                        continue;
                    }
                    if (grid != null && (item.Exceptions.Contains(grid.EntityId.ToString()) || item.Exceptions.Contains(grid.DisplayName)))
                    {
                        item.FoundEntities.Remove(grid.EntityId);
                        continue;
                    }
                }
                
                if (playerId > 0 && item.LimitPlayers)
                {
                    if (item.FoundEntities.TryGetValue(playerId, out var count))
                    {
                        if (count > 0 || item.Limit == 0)
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
                            item.FoundEntities[grid.EntityId] = filteredBlocksCount;
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
                        item.FoundEntities.Remove(playerId);
                        continue;
                    }
                    if (faction != null && item.Exceptions.Contains(faction.Tag))
                    {
                        item.FoundEntities.Remove(faction.FactionId);
                        continue;
                    }
                    var playerSteamId = MyAPIGateway.Multiplayer.Players.TryGetSteamId(playerId);
                    if (item.Exceptions.Contains(playerSteamId.ToString()))
                    {
                        item.FoundEntities.Remove(playerId);
                        continue;
                    }
                    if (item.Exceptions.Contains(MySession.Static.Players.TryGetIdentityNameFromSteamId(playerSteamId)))
                    {
                        item.FoundEntities.Remove(playerId);
                        continue;
                    }
                    if (grid != null && (item.Exceptions.Contains(grid.EntityId.ToString()) || item.Exceptions.Contains(grid.DisplayName)))
                    {
                        item.FoundEntities.Remove(grid.EntityId);
                        continue;
                    }
                }
                
                if (playerId != 0 && item.LimitPlayers)
                {
                    if (item.FoundEntities.TryGetValue(playerId, out var count))
                    {
                        if (count > 0 || item.Limit == 0)
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
                        if (filteredBlocksCount >= item.Limit)
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


    }
}
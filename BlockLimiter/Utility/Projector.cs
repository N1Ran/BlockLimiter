
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
    public static class Projector
    {
        public static bool ProjectBlock(MyCubeBlockDefinition block, long playerId, MyCubeGrid grid = null)
        {
            
            var nope = false;
            var blockCache = new HashSet<MySlimBlock>();
            GridCache.GetBlocks(blockCache);

            var faction = MySession.Static.Factions.GetPlayerFaction(playerId);

            foreach (var item in BlockLimiterConfig.Instance.AllLimits)
            {
                if (!item.RestrictProjection) continue;
                if (!item.BlockPairName.Any() || !Block.IsMatch(block, item)) continue;

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
                    var filteredBlocksCount = blockCache.Count(x=> x.BlockDefinition == block && Block.IsOwner(item.BlockOwnerState, x, playerId));
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

                    var filteredBlocksCount = blockCache.Count(x=> x.CubeGrid == grid && x.BlockDefinition == block && Block.IsOwner(item.BlockOwnerState, x, playerId));
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
                            var subGridBlockCount = blockCache.Count(x=> x.CubeGrid == subGrid && x.BlockDefinition == block && Block.IsOwner(item.BlockOwnerState, x, playerId));
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

        public static bool ProjectBlock(MyCubeBlockDefinition block, long playerId, MyObjectBuilder_CubeGrid grid = null)
        {
            
            var nope = false;

            var faction = MySession.Static.Factions.GetPlayerFaction(playerId);

            foreach (var item in BlockLimiterConfig.Instance.AllLimits)
            {
                if (!item.RestrictProjection) continue;
                if (!item.BlockPairName.Any() || !Block.IsMatch(block, item)) continue;

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
                        if (count <= 0) continue;
                        nope = true;
                        break;
                    }
                }

                if (grid != null && item.LimitGrids)
                {
                    if (!Grid.IsGridType(grid, item)) continue;
                    var filteredBlocksCount =
                        grid.CubeBlocks.Count(x => MyDefinitionManager.Static.GetCubeBlockDefinition(x) == block);
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
                    if (item.FoundEntities.TryGetValue(faction.FactionId, out var count))
                    {
                        if (count <= 0) continue;
                        nope = true;
                        break;
                    }
                }
                
                
            }

            return !nope;
            
        }

    }
}
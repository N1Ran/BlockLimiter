using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Timers;
using BlockLimiter.Settings;
using Sandbox.Definitions;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch.Managers;
using Torch.Mod;
using Torch.Mod.Messages;
using Torch.Utils;
using VRage.Dedicated.Configurator;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Network;

namespace BlockLimiter.Utility
{
    public static class Utilities
    {
        [ReflectedStaticMethod(Type = typeof(MyCubeBuilder), Name = "SpawnGridReply", OverrideTypes = new []{typeof(bool), typeof(ulong)})]
        private static Action<bool, ulong> _spawnGridReply;

       /* [ReflectedMethodInfo(typeof(MyMechanicalConnectionBlockBase), "NotifyTopPartFailed")]
        private static Action<MySession.LimitResult> _attachGridReply;*/

        public static string GetPlayerNameFromSteamId(ulong steamId)
        {
            var pid = MySession.Static.Players.TryGetIdentityId(steamId);
            if (pid == 0)
                return null;
            var id = MySession.Static.Players.TryGetIdentity(pid);
            return id?.DisplayName;
        }

        public static long GetPlayerIdFromSteamId(ulong steamId)
        {
            return MySession.Static.Players.TryGetIdentityId(steamId);
        }

        public static ulong GetSteamIdFromPlayerId(long playerId)
        {
            return MySession.Static.Players.TryGetSteamId(playerId);
        }

        public static void SendFailSound(ulong target)
        {
            _spawnGridReply(false, target);
        }

        public static bool TryGetEntityByNameOrId(string nameOrId, out IMyEntity entity)
        {
            if (long.TryParse(nameOrId, out long id))
                return MyAPIGateway.Entities.TryGetEntityById(id, out entity);

            foreach (var ent in MyEntities.GetEntities())
            {
                if (ent.DisplayName == nameOrId)
                {
                    entity = ent;
                    return true;
                }
            }

            entity = null;
            return false;
        }

        public static long NextInt64(Random rnd)
        {
            var buffer = new byte[sizeof(long)];
            rnd.NextBytes(buffer);
            return BitConverter.ToInt64(buffer, 0);
        }

        public static void ValidationFailed()
        {
            ((MyMultiplayerServerBase)MyMultiplayer.Static).ValidationFailed(MyEventContext.Current.Sender.Value);
        }

        public static bool IsMatch(MyCubeBlockDefinition block, LimitItem item)
        {
            var typeMatch = true;
            if (item.UseBlockType)
                typeMatch = item.BlockPairName.Count > 0 && item.BlockPairName.Any(x =>
                           x.Equals(block.Id.TypeId.ToString().Substring(16), StringComparison.OrdinalIgnoreCase));
            return typeMatch && item.BlockPairName.Count> 0 && item.BlockPairName.Any(x=>x.Equals(block.BlockPairName,StringComparison.OrdinalIgnoreCase));
        }

        #region GridChecks

        public static bool BlockGridSpawn(MyObjectBuilder_CubeGrid grid)
        {
            if (GridSizeViolation(grid))
                return true;
            

            return grid.CubeBlocks.Any(x => AllowBlock(MyDefinitionManager.Static.GetCubeBlockDefinition(x), 0,(MyObjectBuilder_CubeGrid) null));

        }



        public static bool GridSizeViolation(MyObjectBuilder_CubeGrid grid)
        {
            var gridSize = grid.CubeBlocks.Count;
            var gridType = grid.GridSizeEnum;
            var isStatic = grid.IsStatic;

            if (BlockLimiterConfig.Instance.MaxBlockSizeShips > 0 && !isStatic && gridSize >= BlockLimiterConfig.Instance.MaxBlockSizeShips)
            {
                return  true;
            }

            if (BlockLimiterConfig.Instance.MaxBlockSizeStations > 0 && isStatic && gridSize >= BlockLimiterConfig.Instance.MaxBlockSizeStations)
            {
                return  true;
            }

            if (BlockLimiterConfig.Instance.MaxBlocksLargeGrid > 0 && gridType == MyCubeSize.Large && gridSize >= BlockLimiterConfig.Instance.MaxBlocksLargeGrid)
            {
                return  true;
            }

            if (BlockLimiterConfig.Instance.MaxBlocksSmallGrid > 0 && gridType == MyCubeSize.Small && gridSize >= BlockLimiterConfig.Instance.MaxBlocksSmallGrid)
            {
                return  true;
            }

            return false;
        }
        
        public static bool BuildOnGrid(MyObjectBuilder_CubeGrid grid, out int blockCount)
        {
            blockCount = grid.CubeBlocks.Count;
            
            var gridType = grid.GridSizeEnum;
            var isStatic = grid.IsStatic;

            if (BlockLimiterConfig.Instance.MaxBlockSizeShips > 0 && !isStatic && blockCount >= BlockLimiterConfig.Instance.MaxBlockSizeShips)
            {
                return  false;
            }

            if (BlockLimiterConfig.Instance.MaxBlockSizeStations > 0 && isStatic && blockCount >= BlockLimiterConfig.Instance.MaxBlockSizeStations)
            {
                return  false;
            }

            if (BlockLimiterConfig.Instance.MaxBlocksLargeGrid > 0 && gridType == MyCubeSize.Large && blockCount >= BlockLimiterConfig.Instance.MaxBlocksLargeGrid)
            {
                return  false;
            }

            if (BlockLimiterConfig.Instance.MaxBlocksSmallGrid > 0 && gridType == MyCubeSize.Small && blockCount >= BlockLimiterConfig.Instance.MaxBlocksSmallGrid)
            {
                return  false;
            }

            return true;

        }


        #endregion

        #region BlockChecks
        

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

        

        #endregion

        #region Limits

        public static void UpdateLimits(bool useVanilla, out HashSet<LimitItem> items)
        {
            items = new HashSet<LimitItem>();
            if (useVanilla && BlockLimiter.Instance.VanillaLimits.Count > 0)
            {
                items.UnionWith(BlockLimiter.Instance.VanillaLimits);
            }

            items.UnionWith(BlockLimiterConfig.Instance.LimitItems);
        }

        public static StringBuilder GetLimit(long playerId)
        {
            
            var sb = new StringBuilder();
            if (playerId == 0)
            {
                sb.AppendLine("Player not found");
                return sb;
            }

            var limitItems = BlockLimiterConfig.Instance.AllLimits;
            
            if (limitItems.Count < 1)
            {
                sb.AppendLine("No limit found");
                return sb;
            }
            

            var playerFaction = MySession.Static.Factions.GetPlayerFaction(playerId);


            
            foreach (var item in limitItems)
            {
                if (item.BlockPairName.Count == 0 || item.FoundEntities.Count == 0) continue;
                
                sb.AppendLine();
                var itemName = string.IsNullOrEmpty(item.Name) ? item.BlockPairName.FirstOrDefault() : item.Name;

                sb.AppendLine($"----->{itemName}<-----");

                if (item.LimitPlayers && item.FoundEntities.TryGetValue(playerId, out var pCount))
                {
                    var count = pCount + item.Limit;
                    if (count < 1) continue;
                    sb.AppendLine($"Player Limit = {count}/{item.Limit}");
                }

                if (item.LimitFaction && playerFaction != null &&
                    item.FoundEntities.TryGetValue(playerFaction.FactionId, out var fCount))
                {
                    var count = fCount + item.Limit;
                    if (count < 1) continue;
                    sb.AppendLine($"Faction Limit = {count}/{item.Limit} ");
                }

                if (!item.LimitGrids || !item.FoundEntities.Any(x =>
                    GridCache.TryGetGridById(x.Key, out var grid) && grid.BigOwners.Contains(playerId))) continue;

                sb.AppendLine("Grid Limits");

                foreach (var (id,gCount) in item.FoundEntities)
                {
                    if (!GridCache.TryGetGridById(id, out var grid) || !grid.BigOwners.Contains(playerId)) continue;
                    var count = gCount + item.Limit;
                    if (count < 1) continue;
                    sb.AppendLine($"->{grid.DisplayName} = {count} / {item.Limit}");
                }

            }
            

            return sb;

        }

        #endregion


    }
}

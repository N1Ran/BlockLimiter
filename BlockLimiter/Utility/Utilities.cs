using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BlockLimiter.PluginApi;
using BlockLimiter.Settings;
using Sandbox.Definitions;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch.API.Managers;
using Torch.Managers.ChatManager;
using Torch.Utils;
using VRage.Collections;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Network;
using VRage.Utils;
using VRageMath;

namespace BlockLimiter.Utility
{
    public static class Utilities
    {
        [ReflectedStaticMethod(Type = typeof(MyCubeBuilder), Name = "SpawnGridReply", OverrideTypes = new []{typeof(bool), typeof(ulong)})]
        private static Action<bool, ulong> _spawnGridReply;


        public static string GetMessage(string msg, List<string> blockList, string limitName, int count = 1)
        {
            var returnMsg = "";


            returnMsg = msg.Replace("{BC}", count.ToString()).Replace("{L}",limitName).Replace("{BL}", string.Join("\n", blockList));


            return returnMsg;
        }

        public static void TrySendDenyMessage(List<string> blockList, string limitName, 
            ulong remoteUserId = 0, int count = 1)
        {
            if (remoteUserId == 0 || !MySession.Static.Players.IsPlayerOnline(GetPlayerIdFromSteamId(remoteUserId))) return;
            
            var msg = GetMessage(BlockLimiterConfig.Instance.DenyMessage,blockList,limitName, count);

            BlockLimiter.Instance.Torch.CurrentSession.Managers.GetManager<ChatManagerServer>()?
                .SendMessageAsOther(BlockLimiterConfig.Instance.ServerName, msg, Color.Red, remoteUserId);
            SendFailSound(remoteUserId);
            ValidationFailed(remoteUserId);
        }
        
        public static void TrySendProjectionDenyMessage(List<string> blockList, string limitName, 
            ulong remoteUserId = 0, int count = 1)
        {
            if (remoteUserId == 0 || !MySession.Static.Players.IsPlayerOnline(GetPlayerIdFromSteamId(remoteUserId))) return;
            
            var msg = GetMessage(BlockLimiterConfig.Instance.ProjectionDenyMessage,blockList,limitName, count);

            BlockLimiter.Instance.Torch.CurrentSession.Managers.GetManager<ChatManagerServer>()?
                .SendMessageAsOther(BlockLimiterConfig.Instance.ServerName, msg, Color.Red, remoteUserId);
            SendFailSound(remoteUserId);
            ValidationFailed(remoteUserId);
        }
        public static string GetPlayerNameFromId(long id)
        {
            var playerName = "";
            if (id == 0) return playerName;
            var identity = MySession.Static.Players.TryGetIdentity(id);
            playerName = identity?.DisplayName;
            return playerName;
        }
        public static string GetPlayerNameFromSteamId(ulong steamId)
        {
            var pid = MySession.Static.Players.TryGetIdentityId(steamId);
            if (pid == 0)
                return "";
            var id = MySession.Static.Players.TryGetIdentity(pid);
            return id?.DisplayName;
        }
        
        public static MyIdentity GetPlayerIdentityFromSteamId(ulong steamId)
        {
            MyIdentity identity = MySession.Static.Players.TryGetIdentity(MySession.Static.Players.TryGetIdentityId(steamId, 0));
            return identity;
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
            if (target == 0) return;
            _spawnGridReply(false, target);
        }

        /// <summary>
        /// Gets the entity with name or ID given
        /// </summary>
        /// <param name="nameOrId"></param>
        /// <param name="entity"></param>
        /// <returns></returns>
        public static bool TryGetEntityByNameOrId(string nameOrId, out IMyEntity entity)
        {
            
            if (long.TryParse(nameOrId, out var id))
                return MyAPIGateway.Entities.TryGetEntityById(id, out entity);


            foreach (var ent in MyEntities.GetEntities())
            {
                if (string.IsNullOrEmpty(ent.DisplayName)) continue;
                if (!ent.DisplayName.Equals(nameOrId)) continue;
                entity = ent;
                return true;
            }
            
            entity = null;
            return false;
        }

        public static bool TryGetPlayerByNameOrId(string nameOrId, out MyIdentity identity)
        {
            identity = null;
            if (ulong.TryParse(nameOrId, out var steamId))
            {
                var id0 = MySession.Static.Players.TryGetIdentityId(steamId);
                identity = MySession.Static.Players.TryGetIdentity(id0);
                
                return identity != null;
            }

            if (long.TryParse(nameOrId, out var id1))
            {
                identity = MySession.Static.Players.TryGetIdentity(id1);

                return identity != null;
            }

            foreach (var id3 in MySession.Static.Players.GetAllIdentities())
            {
                if (string.IsNullOrEmpty(id3.DisplayName) || !id3.DisplayName.Equals(nameOrId)) continue;
                identity = id3;
                return identity != null;
            }

            identity = null;
            return false;

        }



        public static void ValidationFailed(ulong id = 0)
        {
            var user = id > 0 ? id : MyEventContext.Current.Sender.Value;
            if (user == 0) return;
            ((MyMultiplayerServerBase)MyMultiplayer.Static).ValidationFailed(user);
        }
        
        private static MyConcurrentDictionary<MyStringHash, MyCubeBlockDefinition> _defCache = new MyConcurrentDictionary<MyStringHash, MyCubeBlockDefinition>();

        public static MyCubeBlockDefinition GetDefinition(MyObjectBuilder_CubeBlock block)
        {
            if (_defCache.TryGetValue(block.SubtypeId, out var def))
                return def;

            var blockDefinition = MyDefinitionManager.Static.GetCubeBlockDefinition(block);
            _defCache[block.SubtypeId] = blockDefinition;
            return blockDefinition;
        }


        public static bool IsExcepted(object target)
        {
            if (target == null) return false;
            
            var allExceptions =  new HashSet<string>(BlockLimiterConfig.Instance.GeneralException);

            if (allExceptions.Count == 0) return false;

            MyIdentity identity = null;
            MyFaction faction = null;
            long identityId = 0;
            ulong playerSteamId = 0;
            string displayName = "";
            HashSet<long> gridOwners = new HashSet<long>();

            switch (target)
            {
                case HashSet<long> owners:
                    gridOwners.UnionWith(owners);
                    break;
                case ulong steamId:
                    if (steamId == 0) return false;
                    playerSteamId = steamId;
                    identityId = GetPlayerIdFromSteamId(steamId);
                    identity = MySession.Static.Players.TryGetIdentity(identityId);
                    displayName = identity.DisplayName;
                    faction = MySession.Static.Factions.GetPlayerFaction(identityId);
                    break;
                case string name:
                    if (allExceptions.Contains(name)) return true;
                    if (TryGetPlayerByNameOrId(name, out identity))
                    {
                        identityId = identity.IdentityId;
                        faction = MySession.Static.Factions.GetPlayerFaction(identityId);
                        displayName = identity.DisplayName;
                        playerSteamId = GetSteamIdFromPlayerId(identityId);
                    }
                    break;
                case long id:
                    if (id == 0) return false;
                    identityId = id;
                    identity = MySession.Static.Players.TryGetIdentity(id);
                    if (identity != null)
                    {
                        faction = MySession.Static.Factions.GetPlayerFaction(id);
                        displayName = identity.DisplayName;
                        playerSteamId = GetSteamIdFromPlayerId(id);

                    }
                    else
                    {
                        faction = (MyFaction) MySession.Static.Factions.TryGetFactionById(id);
                    }
                    if (MyEntities.TryGetEntityById(id, out var entity))
                    {
                        if (allExceptions.Contains(entity.DisplayName)) return true;
                    }

                    if (GridCache.TryGetGridById(id, out var foundGrid))
                    {
                        gridOwners.UnionWith(GridCache.GetOwners(foundGrid));
                        gridOwners.UnionWith(GridCache.GetBuilders(foundGrid));
                        if (allExceptions.Contains(foundGrid.DisplayName)) return true;
                    }
                    break;
                case MyFaction targetFaction:
                    if (allExceptions.Contains(targetFaction.Tag) ||
                        allExceptions.Contains(targetFaction.FactionId.ToString()))
                        return true;
                    break;
                case MyPlayer player:
                    if (player.IsBot || player.IsWildlifeAgent) return true;
                    playerSteamId = player.Character.ControlSteamId;
                    if (playerSteamId == 0) return false;
                    if (allExceptions.Contains(playerSteamId.ToString())) return true;
                    identityId = GetPlayerIdFromSteamId(playerSteamId);
                    if (identityId > 0)
                    {
                        if (allExceptions.Contains(identityId.ToString())) return true;
                        identity = MySession.Static.Players.TryGetIdentity(identityId);
                        displayName = identity.DisplayName;
                    }
                    break;
                case MyCubeGrid grid:
                {
                    if (allExceptions.Contains(grid.DisplayName) || allExceptions.Contains(grid.EntityId.ToString()))
                        return true;
                    var owners = GridCache.GetOwners(grid);
                    owners.UnionWith(GridCache.GetBuilders(grid));
                    if (owners.Count == 0) break;
                    gridOwners.UnionWith(owners);
                    break;
                }
            }

            foreach (var owner in gridOwners)
            {
                if (owner == 0) continue;
                if (allExceptions.Contains(owner.ToString())) return true;
                identity = MySession.Static.Players.TryGetIdentity(owner);
                playerSteamId = GetSteamIdFromPlayerId(owner);
                if (playerSteamId > 0 && allExceptions.Contains(playerSteamId.ToString())) return true;
                if (identity != null)
                {
                    if (allExceptions.Contains(identity.DisplayName)) return true;
                }
                faction = MySession.Static.Factions.GetPlayerFaction(owner);
                if (faction != null && (allExceptions.Contains(faction.Tag) ||
                                        allExceptions.Contains(faction.FactionId.ToString()))) return true;
            }

            if (playerSteamId > 0 && allExceptions.Contains(playerSteamId.ToString())) return true;
            if (identityId > 0 && allExceptions.Contains(identityId.ToString())) return true;
            if (identity != null && allExceptions.Contains(identity.DisplayName)) return true;
            if (faction != null && (allExceptions.Contains(faction.Tag)|| allExceptions.Contains(faction.FactionId.ToString()))) return true;
            if (!string.IsNullOrEmpty(displayName) && allExceptions.Contains(displayName)) return true;
            return false;
        }


        public static bool TryGetAimedBlock(IMyPlayer player, out MyTextPanel panel)
        {
            panel = null;
            if (player == null) return false;

            var character = ((MyCharacter) player.Character);

            if (character == null) return false;

            if (!GridCache.TryGetGridById(character.AimedGrid, out var aimedGrid)) return false;

            var aimedBlock = aimedGrid.GetCubeBlock(character.AimedBlock);

            if (aimedBlock.FatBlock is MyTextPanel txtPanel && Block.IsOwner(aimedBlock, player.IdentityId))
            {
                panel = txtPanel;
            }

            return panel != null;
        }


        public static void SetClipboard(string text)
        {
            var blocks = new HashSet<MySlimBlock>();
            GridCache.GetBlocks(blocks);
            if (blocks.Count == 0) return;
            foreach (var block in blocks)
            {
                if (!(block.FatBlock is MyTerminalBlock tBlock))continue;

                if (!tBlock.CustomName.ToString().Contains("Blocklimiter Clipboard", StringComparison.OrdinalIgnoreCase))continue;
                tBlock.CustomData = text;

            }
            
        }

        #region Limits

        public static HashSet<LimitItem> UpdateLimits(bool useVanilla)
        {
            var items = new HashSet<LimitItem>(BlockLimiterConfig.Instance.LimitItems);
            if (useVanilla && BlockLimiter.Instance.VanillaLimits.Count > 0)
            {
                items.UnionWith(BlockLimiter.Instance.VanillaLimits);
            }

            return items;
        }

        public static StringBuilder GetLimit(long playerId)
        {
            
            var sb = new StringBuilder();
            sb.AppendLine("Ain't find shit");
            if (playerId == 0)
            {
                return sb;
            }

            var limitItems = BlockLimiterConfig.Instance.AllLimits;
            
            if (limitItems.Count < 1)
            {
                return sb;
            }

            sb.Clear();

            var playerFaction = MySession.Static.Factions.GetPlayerFaction(playerId);

            var playerBlocks = new HashSet<MySlimBlock>();

            if (playerId > 0)
                
            {
                GridCache.GetPlayerBlocks(playerBlocks,playerId);
                var grids = new HashSet<MyCubeGrid>();
                GridCache.GetPlayerGrids(grids,playerId);

                if (grids.Count > 0)
                {
                    if (BlockLimiterConfig.Instance.MaxBlockSizeShips > 0)
                    {
                        sb.AppendLine($"Ship Limits");
                        foreach (var grid in grids.Where(x=> x.BlocksCount > 0 && !x.IsStatic))
                        {
                            sb.AppendLine(
                                $"{grid.DisplayName}: {grid.BlocksCount}/{BlockLimiterConfig.Instance.MaxBlockSizeShips}");
                        }
                    }
                    
                    if (BlockLimiterConfig.Instance.MaxBlockSizeStations > 0)
                    {
                        sb.AppendLine($"Station Limits");
                        foreach (var grid in grids.Where(x=> x.BlocksCount > 0 && x.IsStatic))
                        {
                            sb.AppendLine(
                                $"{grid.DisplayName}: {grid.BlocksCount}/{BlockLimiterConfig.Instance.MaxBlockSizeStations}");
                        }
                    }
                    
                    if (BlockLimiterConfig.Instance.MaxBlocksLargeGrid > 0)
                    {
                        sb.AppendLine($"Large Grid Block Limits");
                        foreach (var grid in grids.Where(x=> x.BlocksCount > 0 && x.GridSizeEnum == MyCubeSize.Large))
                        {
                            sb.AppendLine(
                                $"{grid.DisplayName}: {grid.BlocksCount}/{BlockLimiterConfig.Instance.MaxBlocksLargeGrid}");
                        }
                    }
                    
                    if (BlockLimiterConfig.Instance.MaxBlocksSmallGrid > 0)
                    {
                        sb.AppendLine($"Small Grid Block Limits");
                        foreach (var grid in grids.Where(x=> x.BlocksCount > 0 && x.GridSizeEnum == MyCubeSize.Small))
                        {
                            sb.AppendLine(
                                $"{grid.DisplayName}: {grid.BlocksCount}/{BlockLimiterConfig.Instance.MaxBlocksSmallGrid}");
                        }
                    }
                    

                    
                    if (BlockLimiterConfig.Instance.MaxSmallGrids > 0)
                    {
                        sb.AppendLine($"Small Grids Limits: {grids.Count(x=>x.GridSizeEnum == MyCubeSize.Small)}/{BlockLimiterConfig.Instance.MaxSmallGrids}");
                    }
                    
                    if (BlockLimiterConfig.Instance.MaxLargeGrids > 0)
                    {
                        sb.AppendLine($"Large Grid Limits: {grids.Count(x=>x.GridSizeEnum == MyCubeSize.Large)}/{BlockLimiterConfig.Instance.MaxLargeGrids}");
                    }
                }
            }
            
            
            

            foreach (var item in limitItems)
            {
                if (item.BlockList.Count == 0 || item.FoundEntities.Count == 0) continue;
                
                var itemName = string.IsNullOrEmpty(item.Name) ? item.BlockList.FirstOrDefault() : item.Name;
                sb.AppendLine();
                sb.AppendLine($"----->{itemName}<-----");

                if (item.LimitPlayers && item.FoundEntities.TryGetValue(playerId, out var pCount))
                {
                    if (pCount > 0)

                    {
                        var dictionary = new ConcurrentDictionary<long, double>();

                        sb.AppendLine($"Player Limit = {pCount}/{item.Limit}");

                        if (playerBlocks.Count > 0)
                        {
                            foreach (var block in playerBlocks)
                            {
                                if (!item.IsMatch(block.BlockDefinition)) continue;
                                dictionary.AddOrUpdate(block.CubeGrid.EntityId, 1, (l, i) => i + 1);
                            }

                            foreach (var (gridId,amount) in dictionary)
                            {
                                if (!GridCache.TryGetGridById(gridId, out var grid) && Grid.IsOwner(grid, playerId))
                                {
                                    sb.AppendLine($"[UnknownGrid] = {amount}");
                                    continue;
                                }

                                sb.AppendLine($"{grid.DisplayName} = {amount}");
                            }


                        }
                    }
                    
                }

                if (item.LimitFaction && playerFaction != null &&
                    item.FoundEntities.TryGetValue(playerFaction.FactionId, out var fCount))
                {
                    if (fCount > 1) 
                        sb.AppendLine($"Faction Limit = {fCount}/{item.Limit} ");
                }

                if (!item.LimitGrids) continue;
                var gridDictionary = new Dictionary<MyCubeGrid,int>();
                foreach (var (id,gCount) in item.FoundEntities)
                {
                    if (!GridCache.TryGetGridById(id, out var grid) || !Grid.IsOwner(grid,playerId)) continue;
                    if (!item.IsGridType(grid))
                    {
                        item.FoundEntities.Remove(id);
                        continue;
                    }
                    if (gCount < 1) continue;
                    gridDictionary[grid] = gCount;
                }
                if (gridDictionary.Count == 0)continue;
                sb.AppendLine("Grid Limits:");

                foreach (var (grid,gCount) in gridDictionary)
                {
                    var gridName = Grid.IsBiggestGridInGroup(grid) ? grid.DisplayName : $"{grid.DisplayName}*{grid.DisplayName}";
                    sb.AppendLine($"->{gridName} = {gCount} / {item.Limit}");
                }
            }
            

            return sb;

        }

        #endregion


    }
}

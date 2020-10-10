using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BlockLimiter.Settings;
using Sandbox.Definitions;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch.Utils;
using VRage.Collections;
using VRage.Game;
using VRage.ModAPI;
using VRage.Network;
using VRage.Utils;

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



        public static void ValidationFailed()
        {
            var user = MyEventContext.Current.Sender.Value;
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


        public static bool IsExcepted(object target, LimitItem limit = null)
        {
            if (target == null) return false;
            
            HashSet<string> allExceptions = new HashSet<string>();
            if (limit != null) allExceptions = new HashSet<string>(limit.Exceptions);
            allExceptions.UnionWith(BlockLimiterConfig.Instance.GeneralException);

            if (allExceptions.Count == 0) return false;

            MyIdentity identity = null;
            MyFaction faction = null;
            long identityId = 0;
            string displayName = "";
            HashSet<long> gridOwners = new HashSet<long>();

            switch (target)
            {
                case HashSet<long> owners:
                    gridOwners.UnionWith(owners);
                    break;
                case ulong steamId:
                    if (steamId == 0) return false;
                    if (allExceptions.Contains(steamId.ToString())) return true;
                    identityId = GetPlayerIdFromSteamId(steamId);
                    if (identityId > 0)
                    {
                        if (allExceptions.Contains(identityId.ToString())) return true;
                        identity = MySession.Static.Players.TryGetIdentity(identityId);
                        displayName = identity.DisplayName;
                    }
                    break;
                case string name:
                    if (allExceptions.Contains(name)) return true;
                    if (TryGetPlayerByNameOrId(name, out identity) &&
                        (allExceptions.Contains(identity.DisplayName) 
                         || allExceptions.Contains(identity.IdentityId.ToString()))) return true;
                    break;
                case long id:
                    if (id == 0) return false;
                    identityId = id;
                    identity = MySession.Static.Players.TryGetIdentity(id);
                    if (identity != null)
                    {
                        faction = MySession.Static.Factions.GetPlayerFaction(id);
                        displayName = identity.DisplayName;
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
                        if (allExceptions.Contains(foundGrid.DisplayName)) return true;
                    }
                    break;
                case MyFaction targetFaction:
                    if (allExceptions.Contains(targetFaction.Tag) ||
                        allExceptions.Contains(targetFaction.FactionId.ToString()))
                        return true;
                    break;
                case MyPlayer player:
                    var playerSteamId = player.Character.ControlSteamId;
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
                if (identity != null && allExceptions.Contains(identity.DisplayName)) return true;
                faction = MySession.Static.Factions.GetPlayerFaction(owner);
                if (faction != null && (allExceptions.Contains(faction.Tag) ||
                                        allExceptions.Contains(faction.FactionId.ToString()))) return true;
            }

            if (identityId > 0 && allExceptions.Contains(identityId.ToString())) return true;
            if (identity != null && allExceptions.Contains(identity.DisplayName)) return true;
            if (faction != null && (allExceptions.Contains(faction.Tag)|| allExceptions.Contains(faction.FactionId.ToString()))) return true;
            if (!string.IsNullOrEmpty(displayName) && allExceptions.Contains(displayName)) return true;
            return false;
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
                if (item.BlockList.Count == 0 || item.FoundEntities.Count == 0) continue;
                
                var itemName = string.IsNullOrEmpty(item.Name) ? item.BlockList.FirstOrDefault() : item.Name;
                sb.AppendLine();
                sb.AppendLine($"----->{itemName}<-----");

                if (item.LimitPlayers && item.FoundEntities.TryGetValue(playerId, out var pCount))
                {
                    if (pCount < 1) continue;
                    sb.AppendLine($"Player Limit = {pCount}/{item.Limit}");
                }

                if (item.LimitFaction && playerFaction != null &&
                    item.FoundEntities.TryGetValue(playerFaction.FactionId, out var fCount))
                {
                    if (fCount < 1) continue;
                    sb.AppendLine($"Faction Limit = {fCount}/{item.Limit} ");
                }

                if (!item.LimitGrids || (!item.FoundEntities.Any(x =>
                    GridCache.TryGetGridById(x.Key, out var grid) && Grid.IsOwner(grid,playerId)))) continue;

                sb.AppendLine("Grid Limits:");

                foreach (var (id,gCount) in item.FoundEntities)
                {
                    if (!GridCache.TryGetGridById(id, out var grid) || !Grid.IsOwner(grid,playerId)) continue;
                    if (gCount < 1) continue;
                    sb.AppendLine($"->{grid.DisplayName} = {gCount} / {item.Limit}");
                }
            }
            

            return sb;

        }

        #endregion


    }
}

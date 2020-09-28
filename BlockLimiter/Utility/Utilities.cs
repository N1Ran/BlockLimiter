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


        public static bool IsExcepted(long id, List<string> exceptions)
        {

            var allExceptions = new HashSet<string>(exceptions);
            allExceptions.UnionWith(BlockLimiterConfig.Instance.GeneralException);

            if (allExceptions.Count == 0) return false;

            if (allExceptions.Contains(id.ToString()))
            {
                return true;
            }

            var faction = MySession.Static.Factions.TryGetFactionById(id);

            if (faction != null)
            {
                return allExceptions.Contains(faction.Tag) || allExceptions.Contains(faction.FactionId.ToString()) || allExceptions.Contains(faction.Name);
            }

            var identity = MySession.Static.Players.TryGetIdentity(id);

            if (identity != null)
            {
               if (allExceptions.Contains(identity.DisplayName)) return true;

               var identFaction = MySession.Static.Factions.GetPlayerFaction(id);

               if (identFaction!= null && (allExceptions.Contains(identFaction.Tag) || allExceptions.Contains(identFaction.Name) ||
                   allExceptions.Contains(identFaction.FactionId.ToString()))) return true;

               var x = MySession.Static.Players.TryGetSteamId(identity.IdentityId);

               if (x > 0 && allExceptions.Contains(x.ToString())) return true;
            } 

            if (!GridCache.TryGetGridById(id, out var grid)) return false;

            if (allExceptions.Contains(grid.DisplayName)) return true;

            var gridFac = grid.CubeBlocks.Select(x => x.FatBlock?.GetOwnerFactionTag()).FirstOrDefault();

            if (!string.IsNullOrEmpty(gridFac) && (allExceptions.Contains(gridFac) || allExceptions.Contains(MySession.Static.Factions.TryGetFactionByTag(gridFac)?.FactionId.ToString()))) return true;

            var owners = new HashSet<long>(GridCache.GetOwners(grid));
            
            if (owners.Count == 0) return false;

            foreach (var ownerId in owners)
            {
                if (allExceptions.Contains(ownerId.ToString())) return true;

                var ownerIdent = MySession.Static.Players.TryGetIdentity(ownerId);

                if (ownerIdent != null && allExceptions.Contains(ownerIdent.DisplayName)) return true;

                var ownerSteamId = MySession.Static.Players.TryGetSteamId(ownerId);

                if (ownerSteamId > 0 && allExceptions.Contains(ownerSteamId.ToString())) return true;
            }

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

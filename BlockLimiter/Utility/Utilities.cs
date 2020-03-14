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
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch.Managers;
using Torch.Mod;
using Torch.Mod.Messages;
using Torch.Utils;
using VRage.Collections;
using VRage.Dedicated.Configurator;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Network;
using VRage.Utils;

namespace BlockLimiter.Utility
{
    public static class Utilities
    {
        [ReflectedStaticMethod(Type = typeof(MyCubeBuilder), Name = "SpawnGridReply", OverrideTypes = new []{typeof(bool), typeof(ulong)})]
        private static Action<bool, ulong> _spawnGridReply;

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
        
        private static MyConcurrentDictionary<MyStringHash, MyCubeBlockDefinition> _defCache = new MyConcurrentDictionary<MyStringHash, MyCubeBlockDefinition>();

        public static MyCubeBlockDefinition GetDefinition(MyObjectBuilder_CubeBlock block)
        {
            if (_defCache.TryGetValue(block.SubtypeId, out var def))
                return def;

            var blockDefinition = MyDefinitionManager.Static.GetCubeBlockDefinition(block);
            _defCache[block.SubtypeId] = blockDefinition;
            return blockDefinition;
        }

        public static void AddFoundEntities(MyCubeBlockDefinition block, long id)
        {
            if (!TryGetEntityByNameOrId(id.ToString(), out var entity))
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
        public static void RemoveBlockFromEntity(MySlimBlock block)
        {
            var blockDef = block.BlockDefinition;
            var blockOwner = block.OwnerId;
            var blockBuilder = block.BuiltBy;
            var blockGrid = block.CubeGrid.EntityId;
            var faction = MySession.Static.Factions.TryGetFactionByTag(block.FatBlock.GetOwnerFactionTag())?.FactionId;

            foreach (var limit in BlockLimiterConfig.Instance.AllLimits.Where(x => Block.IsMatch(blockDef, x)))
            {
                if (limit.LimitGrids)
                {
                    limit.FoundEntities.AddOrUpdate(blockGrid, 0, (l, i) => i - 1);
                }

                if (limit.LimitPlayers)
                {
                    if (Block.IsOwner(limit.BlockOwnerState,block, blockOwner))
                        limit.FoundEntities.AddOrUpdate(blockOwner, 0, (l, i) => i - 1);
                    if (Block.IsOwner(limit.BlockOwnerState,block, blockBuilder))
                        limit.FoundEntities.AddOrUpdate(blockBuilder, 0, (l, i) => i - 1);
                }

                if (limit.LimitFaction && faction != null)
                {
                    limit.FoundEntities.AddOrUpdate((long)faction, 0, (l, i) => i - 1);
                }
            }


        }

        public static bool IsExcepted(object obj, List<string> exceptions)
        {
            var excepted = false;

            switch (obj)
            {
                case long id:
                    if (TryGetEntityByNameOrId(id.ToString(), out var xEntity))
                    {
                        if (exceptions.Contains(xEntity.EntityId.ToString()) ||
                               exceptions.Contains(xEntity.DisplayName)) excepted = true;
                    }

                    var playerId = GetSteamIdFromPlayerId(id);
                    if (playerId <= 0) return exceptions.Contains(id.ToString()) || excepted;
                    if(exceptions.Contains(playerId.ToString()))
                        excepted = true;
                    return exceptions.Contains(id.ToString()) || excepted;
                case MyPlayer player:
                    break;
                case MyFaction faction:
                    excepted = exceptions.Contains(faction.Tag);
                    break;
                case MyCubeGrid grid:
                    excepted = exceptions.Contains(grid.EntityId.ToString()) || exceptions.Contains(grid.DisplayName);
                    break;
                case string any:
                    if (!TryGetEntityByNameOrId(any, out var entity)) return exceptions.Contains(any);
                    if (exceptions.Contains(entity.EntityId.ToString()) ||
                        exceptions.Contains(entity.DisplayName)) excepted = true;
                    return excepted || exceptions.Contains(any);
            }

            return excepted;
        }



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

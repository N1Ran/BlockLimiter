using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Authentication.ExtendedProtection;
using System.Windows.Media;
using BlockLimiter.ProcessHandlers;
using BlockLimiter.Settings;
using BlockLimiter.Utility;
using NLog;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.World;
using Torch;
using Torch.Managers.PatchManager;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Game;
using VRage.Network;

namespace BlockLimiter.Handlers
{
    [PatchShim]
    public static class SpawnGridHandler
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public static void Patch(PatchContext ctx)
        {
            var t = typeof(MyCubeBuilder);
            var m = t.GetMethod("RequestGridSpawn", BindingFlags.NonPublic | BindingFlags.Static);
            ctx.GetPattern(m).Prefixes.Add(typeof(SpawnGridHandler).GetMethod(nameof(Prefix)));

        }

        public static bool Prefix(DefinitionIdBlit definition)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits) return true;
            var block = MyDefinitionManager.Static.GetCubeBlockDefinition(definition);
            if (block == null)
            {
                return true;
            }
            var limitItems = BlockLimiterConfig.Instance.AllLimits;

            if (!limitItems.Any()) return true;
            var found = false;
            var remoteUserId = MyEventContext.Current.Sender.Value;
            var player = MySession.Static.Players.TryGetPlayerBySteamId(remoteUserId);
            var playerId = player.Identity.IdentityId;
            var playerFaction = MySession.Static.Factions.GetPlayerFaction(playerId);

            foreach (var item in limitItems)
            {
                if (!item.BlockPairName.Any(x=>x.Equals(block.BlockPairName,StringComparison.OrdinalIgnoreCase)))continue;
                
                if (item.Exceptions.Any())
                {
                    var skip = false;
                    foreach (var id in item.Exceptions)
                    {
                        if (long.TryParse(id, out var someId) && (someId == playerId || someId == playerFaction?.FactionId))
                        {
                            skip = true;
                            break;
                        }

                        if (ulong.TryParse(id, out var steamId) && steamId == remoteUserId)
                        {
                            skip = true;
                            break;
                        }

                        if (Utilities.TryGetEntityByNameOrId(id, out var entity) && entity != null &&((MyCharacter) entity).ControlSteamId == remoteUserId)
                        {
                            skip = true;
                            break;
                        }

                        if (id.Length > 4 && playerFaction == null) continue;
                        if (id.Equals(playerFaction?.Tag,StringComparison.OrdinalIgnoreCase)) continue;
                        skip = true;
                        break;
                    }
                    
                    if (skip)continue;
                }
                
                var isGridType = false;
                switch (item.GridTypeBlock)
                {
                    case LimitItem.GridType.SmallGridsOnly:
                        isGridType = block.CubeSize == MyCubeSize.Small;
                        break;
                    case LimitItem.GridType.LargeGridsOnly:
                        isGridType = block.CubeSize == MyCubeSize.Large;
                        break;
                    case LimitItem.GridType.AllGrids:
                        isGridType = true;
                        break;
                    case LimitItem.GridType.StationsOnly:
                        break;
                    case LimitItem.GridType.ShipsOnly:
                        isGridType = true;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                if (!isGridType) continue;

                
                if (item.Limit == 0)
                {
                    found = true;
                    break;
                }

                if (item.FoundEntities.TryGetValue(playerId, out var pCount))
                {
                    if (pCount >= 0)
                    {
                        found = true;
                        break;
                    }
                }


                if (playerFaction==null)break;
                if (!item.FoundEntities.TryGetValue(playerFaction.FactionId, out var fCount) || fCount < 0) continue;
                found = true;
                break;

            }

            if (!found)
                    return true;
            var b = block.BlockPairName;
            var p = player.DisplayName;
            if (BlockLimiterConfig.Instance.EnableLog)
                Log.Info($"Blocked {p} from placing a {b}");
            
            //ModCommunication.SendMessageTo(new NotificationMessage($"You've reach your limit for {b}",5000,MyFontEnum.Red),remoteUserId );
            MyVisualScriptLogicProvider.SendChatMessage($"Limit reached", BlockLimiterConfig.Instance.ServerName, playerId, MyFontEnum.Red);

            Utilities.SendFailSound(remoteUserId);
            Utilities.ValidationFailed();

            return false;
        }


    }
}

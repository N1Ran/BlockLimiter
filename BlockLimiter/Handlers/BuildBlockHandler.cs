using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using BlockLimiter.ProcessHandlers;
using Sandbox.Game.Entities;
using BlockLimiter.Utility;
using Sandbox.Game.World;
using Torch.Managers.PatchManager;
using BlockLimiter.Settings;
using Torch;
using VRage.Network;
using NLog;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Entities.Character;
using Sandbox.ModAPI;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Scripting;
using VRageRender;

namespace BlockLimiter.Handlers
{
    [PatchShim]
    public static class BuildBlockHandler
    {
        private static readonly HashSet<string> PatchMethods = new HashSet<string>()
        {
           // "BuildBlockRequest",
            "BuildBlocksRequest",
           // "BuildBlocksAreaRequest"
        };

        private static Logger Log = LogManager.GetCurrentClassLogger();

        public static void Patch(PatchContext ctx)
        {
            var t = typeof(MyCubeGrid);
            var m = typeof(BuildBlockHandler).GetMethod(nameof(Prefix));
            foreach (var met in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {

                if (PatchMethods.Contains(met.Name))
                {
                    ctx.GetPattern(met).Prefixes.Add(m);
                }
            }
        }


        public static bool Prefix(MyCubeGrid __instance, HashSet<MyCubeGrid.MyBlockLocation> locations)
        {
            
            if (!BlockLimiterConfig.Instance.EnableLimits) return true;
            var grid = __instance;
            if (grid == null)
            {
                Log.Debug("Null grid in BuildBlockHandler");
                return true;
            }
            var block = MyDefinitionManager.Static.GetCubeBlockDefinition(locations.ToList().FirstOrDefault().BlockDefinition);
            
            if (block == null)
            {
                Log.Debug("Null block in BuildBlockHandler");
                return true;
            }
            var limitItems = BlockLimiterConfig.Instance.AllLimits;

            if (!limitItems.Any()) return true;

            var remoteUserId = MyEventContext.Current.Sender.Value;
            var playerId = Utilities.GetPlayerIdFromSteamId(remoteUserId);
            var subGrids = MyAPIGateway.GridGroups.GetGroup(grid, GridLinkTypeEnum.Physical);
            var playerFaction = MySession.Static.Factions.GetPlayerFaction(playerId);
            var found = false;
            foreach (var item in limitItems)
            {
                if (!Utilities.IsMatch(block,item))continue;

                if (item.Exceptions.Any())
                {
                    var skip = false;
                    foreach (var id in item.Exceptions)
                    {
                        if (long.TryParse(id, out var someId) && (someId == playerId || someId == playerFaction?.FactionId|| someId == grid.EntityId))
                        {
                            skip = true;
                            break;
                        }

                        if (ulong.TryParse(id, out var steamId) && steamId == remoteUserId)
                        {
                            skip = true;
                            break;
                        }

                        if (Utilities.TryGetEntityByNameOrId(id, out var entity) && entity != null &&( entity == grid ||
                            ((MyCharacter) entity).ControlSteamId == remoteUserId))
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
                        isGridType = grid.GridSizeEnum == MyCubeSize.Small;
                        break;
                    case LimitItem.GridType.LargeGridsOnly:
                        isGridType = grid.GridSizeEnum == MyCubeSize.Large;
                        break;
                    case LimitItem.GridType.StationsOnly:
                        isGridType = grid.IsStatic;
                        break;
                    case LimitItem.GridType.AllGrids:
                        isGridType = true;
                        break;
                    case LimitItem.GridType.ShipsOnly:
                        isGridType = !grid.IsStatic;
                        break;
                }

                if (!isGridType) continue;

                if (item.Limit == 0)
                {
                    found = true;
                    break;
                }
                if (item.DisabledEntities.Contains(playerId))
                {
                    found = true;
                    break;
                }

                if (item.DisabledEntities.Contains(grid.EntityId))
                {
                    found = true;
                    break;
                }

                if (subGrids.Any(sb => item.DisabledEntities.Contains(sb.EntityId)))
                {
                    found = true;
                    break;
                }


                if (playerFaction==null || !item.LimitFaction)continue;
                if (!item.DisabledEntities.Contains(playerFaction.FactionId)) continue;
                found = true;
                break;

            }
            if (!found)
                return true;
            var b = block.BlockPairName;
            if (BlockLimiterConfig.Instance.EnableLog)
                Log.Info($"Blocked {Utilities.GetPlayerNameFromSteamId(remoteUserId)} from placing a {b}");
            //ModCommunication.SendMessageTo(new NotificationMessage($"You've reach your limit for {b}",5000,MyFontEnum.Red),remoteUserId );
            MyVisualScriptLogicProvider.SendChatMessage($"You've reach your limit for {b}",BlockLimiterConfig.Instance.ServerName,playerId,MyFontEnum.Red);

            Utilities.SendFailSound(remoteUserId);
            Utilities.ValidationFailed();
            return false;
        }

    }
}

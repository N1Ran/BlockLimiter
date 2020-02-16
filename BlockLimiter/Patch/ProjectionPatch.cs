using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BlockLimiter.Settings;
using Sandbox;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Entities.Blocks;
using Sandbox.ModAPI;
using Torch.Managers.PatchManager;
using Torch.Mod;
using Torch.Mod.Messages;
using Torch.Utils;
using VRage.Game;
using VRage.Network;
using System.Linq;
using BlockLimiter.ProcessHandlers;
using BlockLimiter.Utility;
using NLog;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using Torch;
using VRage.Utils;

namespace BlockLimiter.Patch
{
    public static class ProjectionPatch
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private static readonly FieldInfo OriginalGridFields = typeof(MyProjectorBase).GetField("m_originalGridBuilders", BindingFlags.NonPublic | BindingFlags.Instance);
        private static  readonly MethodInfo RemoveProjectionMethod = typeof(MyProjectorBase).GetMethod("OnRemoveProjectionRequest", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo NewBlueprintMethod = typeof(MyProjectorBase).GetMethod("OnNewBlueprintSuccess", BindingFlags.NonPublic | BindingFlags.Instance);

        public static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(NewBlueprintMethod).Prefixes.Add(typeof(ProjectionPatch).GetMethod(nameof(PrefixNewBlueprint)));

        }

        public static void PrefixNewBlueprint(MyProjectorBase __instance, ref List<MyObjectBuilder_CubeGrid> projectedGrids)
        {

            if (!BlockLimiterConfig.Instance.EnableLimits)return;
            
            var proj = __instance;
            if (proj == null)
            {
                Log.Debug("Null projector in ProjectionHandler");
                return;
            }

            foreach (var projectedGrid in projectedGrids)
            {
                var grid = projectedGrid;
                var blocks = projectedGrid.CubeBlocks;
                if (!blocks.Any()) continue;
                var remoteUserId = MyEventContext.Current.Sender.Value;
                var playerId = MySession.Static.Players.TryGetPlayerBySteamId(remoteUserId).Identity.IdentityId;
                var count = 0;
                for (var i = blocks.Count - 1; i >= 0; i--)
                {
                    var block = blocks[i];
                    var def = MyDefinitionManager.Static.GetCubeBlockDefinition(block);
                    if (Block.ProjectBlock(def, playerId, grid)) continue;
                    blocks.RemoveAtFast(i);
                    count++;
                }

                if (count <= 0) continue;
                MyMultiplayer.RaiseEvent(__instance, x => (Action)Delegate.CreateDelegate(typeof(Action), x, RemoveProjectionMethod), new EndpointId(remoteUserId));
                var stopSpawn = Grid.GridSizeViolation(grid);
                Task.Run(() =>
                {
                    Thread.Sleep(100);
                    MySandboxGame.Static.Invoke(() =>
                        {
                            ((IMyProjector)__instance).SetProjectedGrid(null);
                            Thread.Sleep(500);
                            if (!stopSpawn)((IMyProjector)__instance).SetProjectedGrid(grid);
                        },
                        "BlockLimiter");
                });

                if (stopSpawn)
                {
                    ModCommunication.SendMessageTo(new NotificationMessage($"Gridsize block count is larger than permitted", 15000, MyFontEnum.Red), remoteUserId);
                    return;
                }
                ModCommunication.SendMessageTo(new NotificationMessage($"Blocklimiter removed {count} blocks blueprint!", 15000, MyFontEnum.Red), remoteUserId);
                //((IMyProjector)__instance).SetProjectedGrid(projectedGrid);
            }

        }

       
    }
}
using System;
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
using System.Reflection.Emit;
using BlockLimiter.Utility;
using NLog;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.World;
using Torch;
using Torch.Managers;

namespace BlockLimiter.Patch
{
    [PatchShim]
    public static class ProjectionPatch
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private static  readonly MethodInfo RemoveProjectionMethod = typeof(MyProjectorBase).GetMethod("OnRemoveProjectionRequest", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo NewBlueprintMethod = typeof(MyProjectorBase).GetMethod("OnNewBlueprintSuccess", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo OriginalGridField = typeof(MyProjectorBase).GetField("m_originalGridBuilder", BindingFlags.NonPublic | BindingFlags.Instance);
        public static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(typeof(MyProjectorBase).GetMethod("OnNewBlueprintSuccess")).Prefixes.Add(typeof(ProjectionPatch).GetMethod(nameof(PrefixNewBlueprint), BindingFlags.Static| BindingFlags.Instance| BindingFlags.NonPublic));

        }

        private static bool PrefixNewBlueprint(MyProjectorBase __instance, ref List<MyObjectBuilder_CubeGrid> projectedGrids)
        {

            if (!BlockLimiterConfig.Instance.EnableLimits)return true;
            var proj = __instance;
            if (proj == null)
            {
                Log.Debug("No projector?");
                return false;
            }
            
            var grid = projectedGrids[0];

            if (grid == null)
            {
                Log.Warn("Grid null in projectorPatch");
                return false;
            }

            var remoteUserId = MyEventContext.Current.Sender.Value;
            var player = MySession.Static.Players.TryGetPlayerBySteamId(remoteUserId);

            var stopSpawn = Grid.GridSizeViolation(grid);
            var target = new EndpointId(remoteUserId);
            
            
            if (stopSpawn)
            {
                proj.SendRemoveProjection();
                NetworkManager.RaiseEvent(proj, RemoveProjectionMethod, target);
                Utilities.SendFailSound(remoteUserId);
                Utilities.ValidationFailed();
                return false;
            }
            
            var count = 0;
            var blocks = projectedGrids[0].CubeBlocks;
            var playerId = player.Identity.IdentityId;
            for (var i = blocks.Count - 1; i >= 0; i--)
            {
                var block = blocks[i];
                if (Block.ProjectBlock(block, playerId, grid) &&
                    Block.ProjectBlock(block,playerId,proj.CubeGrid)) continue;
                blocks.RemoveAtFast(i);
                count++;
            }
            

            if (count < 1) return true;

            NetworkManager.RaiseEvent(proj, RemoveProjectionMethod, target);

            try
            {
                var projGrid = new List<MyObjectBuilder_CubeGrid>()
                    {grid};
                NetworkManager.RaiseEvent(proj, NewBlueprintMethod, proj, target);
            }
            catch (Exception e)
            {
                throw;
                //Log.Warn(e);
            }

           
            ModCommunication.SendMessageTo(new NotificationMessage($"Blocklimiter removed {count} blocks blueprint!", 15000,
                        MyFontEnum.Red), remoteUserId);

            return true;


        }

       
    }
}
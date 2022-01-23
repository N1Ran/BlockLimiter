using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sandbox.Game.Entities;
using BlockLimiter.Utility;
using Sandbox.Game.World;
using Torch.Managers.PatchManager;
using BlockLimiter.Settings;
using NLog;
using VRage.Network;
using Sandbox.Definitions;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Entities.Cube;
using Torch.API.Managers;
using Torch.Managers.ChatManager;
using VRageMath;


namespace BlockLimiter.Patch
{
    [PatchShim]
    public static class BuildBlockPatch
    {
     
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private static readonly Logger _blockLimitLogger = BlockLimiter.Instance.Log;


        public static void Patch(PatchContext ctx)
        {
            var t = typeof(MyCubeGrid);
            var aMethod = t.GetMethod("BuildBlocksRequest", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            ctx.GetPattern(aMethod).Prefixes.Add(typeof(BuildBlockPatch).GetMethod(nameof(BuildBlocksRequest),BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
            var bMethod = t.GetMethod("BuildBlocksAreaRequest", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            ctx.GetPattern(bMethod).Prefixes.Add(typeof(BuildBlockPatch).GetMethod(nameof(BuildBlocksArea),BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
            
            ctx.GetPattern(typeof(MyProjectorBase).GetMethod("BuildInternal", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Instance))
                .Prefixes.Add(typeof(BuildBlockPatch).GetMethod(nameof(Build),
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
            
        }
     

        /// <summary>
        /// Checks blocks being built in creative with multiblock placement.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="area"></param>
        /// <returns></returns>
        private static bool BuildBlocksArea(MyCubeGrid __instance, MyCubeGrid.MyBlockBuildArea area)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits) return true;

            var def = MyDefinitionManager.Static.GetCubeBlockDefinition(area.DefinitionId);
            var grid = __instance;

            int blocksToBuild = (int) area.BuildAreaSize.X * (int) area.BuildAreaSize.Y * (int) area.BuildAreaSize.Z;


            if (grid == null)
            {
                Log.Debug("Null grid in BuildBlockHandler");
                return true;
            }

            var remoteUserId = MyEventContext.Current.Sender.Value;
            var playerId = Utilities.GetPlayerIdFromSteamId(remoteUserId);

            if (Block.IsWithinLimits(def, playerId, grid.EntityId, blocksToBuild, out var limitName)) return true;

            if (remoteUserId == 0 || !MySession.Static.Players.IsPlayerOnline(playerId)) return false;
            
            _blockLimitLogger.Info($"Blocked {Utilities.GetPlayerNameFromSteamId(remoteUserId)} from placing {def.ToString().Substring(16)} due to limits");

            var msg = Utilities.GetMessage(BlockLimiterConfig.Instance.DenyMessage,new List<string>(){def.ToString().Substring(16)},limitName);

            BlockLimiter.Instance.Torch.CurrentSession.Managers.GetManager<ChatManagerServer>()?
                    .SendMessageAsOther(BlockLimiterConfig.Instance.ServerName, msg, Color.Red, remoteUserId);
            Utilities.SendFailSound(remoteUserId);
            Utilities.ValidationFailed();

            return false;


        }

        /// <summary>
        /// Checks blocks being placed on grids.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="locations"></param>
        /// <returns></returns>
        private static bool BuildBlocksRequest(MyCubeGrid __instance, HashSet<MyCubeGrid.MyBlockLocation> locations)
        {
            
            if (!BlockLimiterConfig.Instance.EnableLimits) return true;

            var grid = __instance;
            if (grid == null)
            {
                Log.Debug("Null grid in BuildBlockHandler");
                return true;
            }


            var def = MyDefinitionManager.Static.GetCubeBlockDefinition(locations.FirstOrDefault().BlockDefinition);

            if (def == null) return true;
            
           
            var remoteUserId = MyEventContext.Current.Sender.Value;
            var playerId = Utilities.GetPlayerIdFromSteamId(remoteUserId);

            if (Block.IsWithinLimits(def, playerId, grid.EntityId,1, out var limitName)) return true;

            if (remoteUserId == 0 || !MySession.Static.Players.IsPlayerOnline(playerId)) return false;

            _blockLimitLogger.Info($"Blocked {Utilities.GetPlayerNameFromSteamId(remoteUserId)} from placing {def.ToString().Substring(16)} due to limits");
            var msg = Utilities.GetMessage(BlockLimiterConfig.Instance.DenyMessage,new List<string> {def.ToString().Substring(16)},limitName);
            BlockLimiter.Instance.Torch.CurrentSession.Managers.GetManager<ChatManagerServer>()?
                    .SendMessageAsOther(BlockLimiterConfig.Instance.ServerName, msg, Color.Red, remoteUserId);

            Utilities.SendFailSound(remoteUserId);
            Utilities.ValidationFailed();
            return false;

        }


        /// <summary>
        /// Checks blocks about to be welded with limits and decide if weld is allowed.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="cubeBlockPosition"></param>
        /// <param name="owner"></param>
        /// <param name="builder"></param>
        /// <param name="requestInstant"></param>
        /// <param name="builtBy"></param>
        /// <returns></returns>
        private static bool Build(MyProjectorBase __instance, Vector3I cubeBlockPosition, long owner, long builder, bool requestInstant = true, long builtBy = 0)
        {
            var projector = __instance;
            if (projector == null) return false;

            if (!BlockLimiterConfig.Instance.EnableLimits) return true;

            if (owner + builder == 0) return false;

            long projectorId = projector.EntityId;
            int subgridIndex = (int)builtBy;

            MyCubeGrid builtGrid = (MyCubeGrid)BlockLimiter.Instance.MultigridProjectorApi.GetBuiltGrid(projectorId, subgridIndex) ?? __instance.CubeGrid;
            if (builtGrid == null) return false;

            MyCubeGrid previewGrid = (MyCubeGrid)BlockLimiter.Instance.MultigridProjectorApi.GetPreviewGrid(projectorId, subgridIndex) ?? __instance.ProjectedGrid;
            if (previewGrid == null) return false;

            MySlimBlock previewBlock = previewGrid?.GetCubeBlock(cubeBlockPosition);
            var blockDefinition = previewBlock?.BlockDefinition;
            if (blockDefinition == null) return false;

            var remoteUserId = MyEventContext.Current.Sender.Value;

            if (Block.IsWithinLimits(blockDefinition, owner, builtGrid.EntityId, 1, out var limitName) &&
                Block.IsWithinLimits(blockDefinition, builder, builtGrid.EntityId, 1, out limitName)) return true;

            if (remoteUserId == 0) return false;
            var grid = projector.CubeGrid;
            _blockLimitLogger.Info($"Blocked welding of {blockDefinition.ToString().Substring(16)} on {grid.DisplayName} ownedby {Utilities.GetPlayerNameFromId(grid.BigOwners[0])}");
            var playerId = Utilities.GetPlayerIdFromSteamId(remoteUserId);

            if (!MySession.Static.Players.IsPlayerOnline(playerId)) return false;

            var msg = Utilities.GetMessage(BlockLimiterConfig.Instance.DenyMessage,new List<string> {blockDefinition.ToString().Substring(16)},limitName);
            BlockLimiter.Instance.Torch.CurrentSession.Managers.GetManager<ChatManagerServer>()?
                .SendMessageAsOther(BlockLimiterConfig.Instance.ServerName, msg, Color.Red, remoteUserId);
            Utilities.SendFailSound(remoteUserId);
            Utilities.ValidationFailed(remoteUserId);
            return false;


        }
            

    }
}

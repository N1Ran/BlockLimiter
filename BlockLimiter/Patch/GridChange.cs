using System;
using System.Reflection;
using BlockLimiter.Settings;
using BlockLimiter.Utility;
using NLog;
using NLog.Fluent;
using Sandbox.Engine.Physics;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using Torch;
using Torch.Managers;
using Torch.Managers.PatchManager;
using Torch.Utils;
using VRage.Game;
using VRage.Network;

namespace BlockLimiter.Patch
{
    [PatchShim]
    public static class GridChange
    {
        private static  readonly MethodInfo ConvertToStationRequest = typeof(MyCubeGrid).GetMethod(nameof(MyCubeGrid.OnConvertedToStationRequest), BindingFlags.Public | BindingFlags.Instance);
        private static readonly MethodInfo ConvertToShipRequest = typeof(MyCubeGrid).GetMethod("OnConvertedToShipRequest", BindingFlags.NonPublic | BindingFlags.Instance);

        public static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(ConvertToStationRequest).Prefixes.Add(typeof(GridChange).GetMethod(nameof(ToStatic),BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
            ctx.GetPattern(ConvertToShipRequest).Prefixes.Add(typeof(GridChange).GetMethod(nameof(ToDynamic),BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
            ctx.GetPattern(typeof(MyCubeGrid).GetMethod("OnGridClosedRequest",  BindingFlags.NonPublic | BindingFlags.Static)).
                Prefixes.Add(typeof(GridChange).GetMethod(nameof(OnGridClosed), BindingFlags.Static|  BindingFlags.NonPublic));
        }

        /// <summary>
        /// Triggers when a grid is removed from the world and removes the grid and block from player/faction count
        /// </summary>
        /// <param name="__instance"></param>
        /// <returns></returns>
        private static bool OnGridClosed(ref long entityId)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits)
            {
                return true;
            }

            if (!GridCache.TryGetGridById(entityId, out var grid))
            {
                Log.Debug("Null closure");
                return true;
            }
            
            GridCache.RemoveGrid(entityId);
            
            foreach (var block in grid.CubeBlocks)
            {
                if (block == null) continue;
                Block.RemoveBlock(block);
            }
            
            return true;
        }
        
        /// <summary>
        ///
        /// </summary>
        /// <param name="__instance"></param>
        /// <returns></returns>
        private static bool ToStatic (MyCubeGrid __instance)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits || !BlockLimiterConfig.Instance.EnableConvertBlock)
            {
                return true;
            }
            var grid = __instance;

            if (grid == null)
            {
                if (BlockLimiterConfig.Instance.EnableLog) Log.Warn("Null grid in GridChange handler");
                return true;
            }
            var remoteUserId = MyEventContext.Current.Sender.Value;
            var playerId = Utilities.GetPlayerIdFromSteamId(remoteUserId);
            if (remoteUserId==0 ||playerId == 0) return false;
            MyVisualScriptLogicProvider.SendChatMessage($"Grid conversion blocked due to violation",BlockLimiterConfig.Instance.ServerName,playerId,MyFontEnum.Red);
            if (BlockLimiterConfig.Instance.EnableLog) Log.Info(
                $"Grid conversion blocked from {MySession.Static.Players.TryGetPlayerBySteamId(remoteUserId).DisplayName} due to violation");
            Utilities.SendFailSound(remoteUserId);
            Utilities.ValidationFailed();
            return false;

        }

        private static bool ToDynamic(MyCubeGrid __instance)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits || !BlockLimiterConfig.Instance.EnableConvertBlock)
            {
                return true;
            }
            
            var grid = __instance;
            if (grid == null)
            {
                if (BlockLimiterConfig.Instance.EnableLog) Log.Warn("Null grid in GridChange handler");
                return true;
            }
            if (Grid.AllowConversion(grid)) return true;
            var remoteUserId = MyEventContext.Current.Sender.Value;
            var playerId = Utilities.GetPlayerIdFromSteamId(remoteUserId);
            if (remoteUserId==0 ||playerId == 0) return false;
            MyVisualScriptLogicProvider.SendChatMessage($"Grid conversion blocked due to violation",BlockLimiterConfig.Instance.ServerName,playerId,MyFontEnum.Red);
            if (BlockLimiterConfig.Instance.EnableLog)Log.Info(
                $"Grid conversion blocked from {MySession.Static.Players.TryGetPlayerBySteamId(remoteUserId).DisplayName} due to violation");
            Utilities.SendFailSound(remoteUserId);
            Utilities.ValidationFailed();
            return false;
        }

    }
}
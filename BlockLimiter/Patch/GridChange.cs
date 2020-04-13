using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
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

        public static event Action<MyCubeGrid, long> SplitCreated;

        public static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(ConvertToStationRequest).Prefixes.Add(typeof(GridChange).GetMethod(nameof(ToStatic),BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
            ctx.GetPattern(ConvertToShipRequest).Prefixes.Add(typeof(GridChange).GetMethod(nameof(ToDynamic),BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
            ctx.GetPattern(typeof(MyCubeGrid).GetMethod("OnGridClosedRequest",  BindingFlags.NonPublic | BindingFlags.Static)).
                Prefixes.Add(typeof(GridChange).GetMethod(nameof(OnGridClosed), BindingFlags.Static|  BindingFlags.NonPublic));
            ctx.GetPattern(typeof(MyCubeGrid).GetMethod("CreateGridForSplit",  BindingFlags.NonPublic |  BindingFlags.Static)).
                Prefixes.Add(typeof(GridChange).GetMethod(nameof(OnCreateSplit), BindingFlags.Static| BindingFlags.Instance |  BindingFlags.NonPublic));
        }


        private static void OnCreateSplit(MyCubeGrid originalGrid)
        {
            if (originalGrid == null) return;
            Task.Run(() =>
            {
                Thread.Sleep(100);
                if (!GridCache.TryGetGridById(originalGrid.EntityId, out var newStateGrid)) return;
                UpdateLimits.GridLimit(newStateGrid);
            });
        }

        /// <summary>
        /// Triggers when a grid is removed from the world and removes the grid and block from player/faction count
        /// </summary>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private static bool OnGridClosed(ref long entityId)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits)
            {
                return true;
            }

            if (!GridCache.TryGetGridById(entityId, out var grid))
            {
                BlockLimiter.Instance.Log.Debug("Null closure");
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
                if (BlockLimiterConfig.Instance.EnableLog) BlockLimiter.Instance.Log.Warn("Null grid in GridChange handler");
                return true;
            }
            var remoteUserId = MyEventContext.Current.Sender.Value;
            var playerId = Utilities.GetPlayerIdFromSteamId(remoteUserId);
            if (Grid.AllowConversion(grid) || remoteUserId == 0 || playerId == 0)
            {
                var gridId = grid.EntityId;
                Task.Run(()=>
                {
                    Thread.Sleep(100);
                    GridCache.TryGetGridById(gridId, out var newStateGrid);
                    if (newStateGrid == null) return;
                    UpdateLimits.GridLimit(newStateGrid);
                });
                return true;
            }
            MyVisualScriptLogicProvider.SendChatMessage($"{BlockLimiterConfig.Instance.DenyMessage}",BlockLimiterConfig.Instance.ServerName,playerId,MyFontEnum.Red);
            if (BlockLimiterConfig.Instance.EnableLog) BlockLimiter.Instance.Log.Info(
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
                if (BlockLimiterConfig.Instance.EnableLog) BlockLimiter.Instance.Log.Warn("Null grid in GridChange handler");
                return true;
            }
            var remoteUserId = MyEventContext.Current.Sender.Value;
            var playerId = Utilities.GetPlayerIdFromSteamId(remoteUserId);
            if (Grid.AllowConversion(grid) || remoteUserId == 0 || playerId == 0)
            {
                var gridId = grid.EntityId;
                Task.Run(()=>
                {
                    Thread.Sleep(100);
                    GridCache.TryGetGridById(gridId, out var newStateGrid);
                    if (newStateGrid == null) return;
                    UpdateLimits.GridLimit(newStateGrid);
                });
                return true;
            }
            MyVisualScriptLogicProvider.SendChatMessage($"{BlockLimiterConfig.Instance.DenyMessage}",BlockLimiterConfig.Instance.ServerName,playerId,MyFontEnum.Red);
            if (BlockLimiterConfig.Instance.EnableLog)BlockLimiter.Instance.Log.Info(
                $"Grid conversion blocked from {MySession.Static.Players.TryGetPlayerBySteamId(remoteUserId).DisplayName} due to violation");
            Utilities.SendFailSound(remoteUserId);
            Utilities.ValidationFailed();
            return false;
        }

    }
}
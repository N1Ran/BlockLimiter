using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BlockLimiter.Punishment;
using BlockLimiter.Settings;
using BlockLimiter.Utility;
using NLog;
using Sandbox.Engine.Physics;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using Torch;
using Torch.API.Managers;
using Torch.Managers;
using Torch.Managers.ChatManager;
using Torch.Managers.PatchManager;
using Torch.Utils;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Network;
using VRageMath;

namespace BlockLimiter.Patch
{
    [PatchShim]
    public static class GridChange
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();


        private static  readonly MethodInfo ConvertToStationRequest = typeof(MyCubeGrid).GetMethod(nameof(MyCubeGrid.OnConvertedToStationRequest), BindingFlags.Public | BindingFlags.Instance);
        private static readonly MethodInfo ConvertToShipRequest = typeof(MyCubeGrid).GetMethod("OnConvertedToShipRequest", BindingFlags.NonPublic | BindingFlags.Instance);

        public static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(typeof(MyEntity).GetMethod("Close", BindingFlags.Public | BindingFlags.Instance)).
                Prefixes.Add(typeof(GridChange).GetMethod(nameof(OnClose),BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));

            ctx.GetPattern(ConvertToStationRequest).Prefixes.Add(typeof(GridChange).GetMethod(nameof(ToStatic),BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
            ctx.GetPattern(ConvertToShipRequest).Prefixes.Add(typeof(GridChange).GetMethod(nameof(ToDynamic),BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
            
            ctx.GetPattern(typeof(MyCubeGrid).GetMethod("MoveBlocks",  BindingFlags.Static|BindingFlags.NonPublic)).Suffixes
                .Add(typeof(GridChange).GetMethod(nameof(OnCreateSplit), BindingFlags.Static| BindingFlags.NonPublic));
        }


        private static HashSet<MySlimBlock> _justRemoved = new HashSet<MySlimBlock>();
        /// <summary>
        /// Removes blocks on closure
        /// </summary>
        /// <param name="__instance"></param>
        /// <returns></returns>
        private static void OnClose(MyEntity __instance)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits) return;
            if (__instance.MarkedForClose) return;

            if (__instance is MyCubeBlock cubeBlock)
            {
                if (_justRemoved.Contains(cubeBlock.SlimBlock))
                {
                    _justRemoved.Remove(cubeBlock.SlimBlock);
                    return;
                }
                _justRemoved.Add(cubeBlock.SlimBlock);
                Block.DecreaseCount(cubeBlock.BlockDefinition,
                    cubeBlock.BuiltBy == cubeBlock.OwnerId
                        ? new List<long> {cubeBlock.BuiltBy}
                        : new List<long> {cubeBlock.BuiltBy, cubeBlock.OwnerId}, 1, cubeBlock.CubeGrid.EntityId);
            }
            if (!(__instance is MyCubeGrid grid)) return;
            foreach (var block in grid.CubeBlocks)
            {
                if (_justRemoved.Contains(block))
                {
                    _justRemoved.Remove(block);
                    continue;
                }
                _justRemoved.Add(block);
                Block.DecreaseCount(block.BlockDefinition,
                    block.BuiltBy == block.OwnerId
                        ? new List<long> {block.BuiltBy}
                        : new List<long> {block.BuiltBy, block.OwnerId}, 1, grid.EntityId);
            }


        }

        /// <summary>
        /// Updates limits on grid split
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        private static void OnCreateSplit(ref MyCubeGrid from, ref MyCubeGrid to)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits) return;


            var toBlocks = new HashSet<MySlimBlock>(to.CubeBlocks);

            if (toBlocks.Count == 0)
            {
                Log.Warn("Could not update count after grid separation");
                return;
            }

            foreach (var block in toBlocks)
            {
                if (block.BuiltBy == block.OwnerId)
                    Block.DecreaseCount(block.BlockDefinition,new List<long>{block.BuiltBy},1,from.EntityId);
                else
                {
                    Block.DecreaseCount(block.BlockDefinition,new List<long>{block.BuiltBy,block.OwnerId},1,from.EntityId);
                }
            }


            var grid = from;
            if (grid == null) return;

            var removeSmallestGrid = false;

            var owners = new HashSet<long>(GridCache.GetOwners(from));
            owners.UnionWith(GridCache.GetBuilders(grid));

            if (owners.Count == 0) return;
            foreach (var owner in owners)
            {
                if (!Grid.CountViolation(grid, owner))continue;
                removeSmallestGrid = true;
                break;
            }

            if (!removeSmallestGrid || BlockLimiterConfig.Instance.BlockType != BlockLimiterConfig.BlockingType.Hard) return;
            var grid1 = from;
            var grid2 = to;
            BlockLimiter.Instance.Torch.InvokeAsync(() =>
            {
                Thread.Sleep(100);
                if (grid1.BlocksCount > grid2.BlocksCount)
                {

                    grid2.SendGridCloseRequest();
                    UpdateLimits.GridLimit(grid1);
                }
                else
                {
                    grid1.SendGridCloseRequest();
                    UpdateLimits.GridLimit(grid2);
                }
            });
        }

        
        /// <summary>
        ///Checks if grid will violate limit on conversion and updates limits after
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
                Log.Warn("Null grid in GridChange handler");
                return true;
            }

            if (grid.GridSizeEnum == MyCubeSize.Small) return true;

            var remoteUserId = MyEventContext.Current.Sender.Value;
            var playerId = Utilities.GetPlayerIdFromSteamId(remoteUserId);
            if (Grid.AllowConversion(grid,out var blocks, out var count, out var limitName) || remoteUserId == 0 || playerId == 0)
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
            var msg = Utilities.GetMessage(BlockLimiterConfig.Instance.DenyMessage,blocks,limitName,count);

            if (remoteUserId != 0 && MySession.Static.Players.IsPlayerOnline(playerId))
                BlockLimiter.Instance.Torch.CurrentSession.Managers.GetManager<ChatManagerServer>()?
                    .SendMessageAsOther(BlockLimiterConfig.Instance.ServerName, msg, Color.Red, remoteUserId);
            Log.Info(
                $"Grid conversion blocked from {MySession.Static.Players.TryGetPlayerBySteamId(remoteUserId).DisplayName} due to possible violation");
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
                Log.Warn("Null grid in GridChange handler");
                return true;
            }
            var remoteUserId = MyEventContext.Current.Sender.Value;
            var playerId = Utilities.GetPlayerIdFromSteamId(remoteUserId);
            if (Grid.AllowConversion(grid, out var blocks, out var count,out var limitName) || remoteUserId == 0 || playerId == 0)
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
            var msg = Utilities.GetMessage(BlockLimiterConfig.Instance.DenyMessage,blocks,limitName,count);

            if (remoteUserId != 0 && MySession.Static.Players.IsPlayerOnline(playerId))
                BlockLimiter.Instance.Torch.CurrentSession.Managers.GetManager<ChatManagerServer>()?
                    .SendMessageAsOther(BlockLimiterConfig.Instance.ServerName, msg, Color.Red, remoteUserId);
            Log.Info(
                $"Grid conversion blocked from {MySession.Static.Players.TryGetPlayerBySteamId(remoteUserId).DisplayName} due to possible violation");
            Utilities.SendFailSound(remoteUserId);
            Utilities.ValidationFailed();
            return false;
        }

    }
}
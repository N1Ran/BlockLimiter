using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BlockLimiter.Settings;
using BlockLimiter.Utility;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using Torch;
using Torch.Managers.PatchManager;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Network;

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
            try
            {
                ctx.GetPattern(typeof(MyEntity).GetMethod("Close", BindingFlags.Public | BindingFlags.Instance)).
                    Prefixes.Add(typeof(GridChange).GetMethod(nameof(OnClose),BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));

                ctx.GetPattern(ConvertToStationRequest).Prefixes.Add(typeof(GridChange).GetMethod(nameof(ToStatic),BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
                ctx.GetPattern(ConvertToShipRequest).Prefixes.Add(typeof(GridChange).GetMethod(nameof(ToDynamic),BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
            
                ctx.GetPattern(typeof(MyCubeGrid).GetMethod("MoveBlocks",  BindingFlags.Static|BindingFlags.NonPublic)).Suffixes
                    .Add(typeof(GridChange).GetMethod(nameof(OnCreateSplit), BindingFlags.Static| BindingFlags.NonPublic));
            }
            catch (Exception e)
            {
                Log.Error(e.StackTrace, "Patching Failed");
            }

            
        }


        private static readonly Dictionary<long, DateTime> _justRemoved = new Dictionary<long,DateTime>();

        public static void ClearRemoved()
        {
            lock (_justRemoved)
            {
                if (_justRemoved.Count == 0) return;

                for (var i = 0; i < Math.Min(10,_justRemoved.Count); i++)
                {
                    var (id, time) = _justRemoved.ElementAt(i);
                    if ((DateTime.Now - time).Ticks < 100) continue;
                    _justRemoved.Remove(id);
                }
            }
            
        }
        
        /// <summary>
        /// Removes blocks on closure
        /// </summary>
        /// <param name="__instance"></param>
        /// <returns></returns>
        private static void OnClose(MyEntity __instance)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits) return;
            if (__instance.MarkedForClose ) return;
            if (__instance is MyCubeBlock cubeBlock)
            {
                var id = cubeBlock.EntityId;
                lock (_justRemoved)
                {
                    if (_justRemoved.TryGetValue(id, out var time))
                    {
                        if ((DateTime.Now - time).Ticks > 100)
                            _justRemoved.Remove(cubeBlock.EntityId);
                        return;
                    }
                    _justRemoved[id] = DateTime.Now; 
                }
                //added filter for projector
                if (cubeBlock.CubeGrid.Projector == null)
                    GridCache.RemoveBlock(cubeBlock.SlimBlock);
                Block.DecreaseCount(cubeBlock.BlockDefinition,
                    cubeBlock.BuiltBy == cubeBlock.OwnerId
                        ? new List<long> {cubeBlock.BuiltBy}
                        : new List<long> {cubeBlock.BuiltBy, cubeBlock.OwnerId}, 1, cubeBlock.CubeGrid.EntityId);
            }
            else if ((__instance is MyCubeGrid grid))
            {
                var gridBlocks = new List<MySlimBlock>(grid.CubeBlocks);
                if (grid.Projector == null) 
                    GridCache.RemoveGrid(grid);
                if (gridBlocks?.Count == 0) return;
                lock (_justRemoved)
                {
                    foreach (var block in gridBlocks)
                    {
                        if (block.FatBlock == null) continue;
                        var id = block.FatBlock.EntityId;
                        if (_justRemoved.TryGetValue(id, out var _))
                        {
                            _justRemoved.Remove(block.FatBlock.EntityId);
                            continue;
                        }
                        _justRemoved[id] = DateTime.Now;
                        Block.DecreaseCount(block.BlockDefinition,
                            block.BuiltBy == block.OwnerId
                                ? new List<long> {block.BuiltBy}
                                : new List<long> {block.BuiltBy, block.OwnerId}, 1, grid.EntityId);
                    }
                }
                    
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
                Block.DecreaseCount(block.BlockDefinition,
                    block.BuiltBy == block.OwnerId
                        ? new List<long> {block.BuiltBy}
                        : new List<long> {block.BuiltBy, block.OwnerId}, 1, @from.EntityId);
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
                    UpdateLimits.Enqueue(grid1.EntityId);
                }
                else
                {
                    grid1.SendGridCloseRequest();
                    UpdateLimits.Enqueue(grid2.EntityId);
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
                    UpdateLimits.Enqueue(newStateGrid.EntityId);
                });
                return true;
            }
            Utilities.TrySendDenyMessage(blocks,limitName,remoteUserId,count);
            BlockLimiter.Instance.Log.Info(
                $"Grid conversion blocked from {MySession.Static.Players.TryGetPlayerBySteamId(remoteUserId).DisplayName} due to possible violation");
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
                    UpdateLimits.Enqueue(newStateGrid.EntityId);
                });
                return true;
            }
            Utilities.TrySendDenyMessage(blocks, limitName, remoteUserId, count);
            BlockLimiter.Instance.Log.Info(
                $"Grid conversion blocked from {MySession.Static.Players.TryGetPlayerBySteamId(remoteUserId).DisplayName} due to possible violation");
            return false;
        }

    }
}
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BlockLimiter.Settings;
using BlockLimiter.Utility;
using NLog;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using SpaceEngineers.Game.Entities.Blocks;
using SpaceEngineers.Game.ModAPI.Ingame;
using Torch.Managers.PatchManager;
using Torch.Mod;
using Torch.Mod.Messages;
using Torch.Utils;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Network;

namespace BlockLimiter.Patch
{
    [PatchShim]
    public static class MergeBlockPatch
    {
        private static readonly Logger Log = BlockLimiter.Instance.Log;

        public static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(typeof(MyShipMergeBlock).GetMethod(nameof(MyShipMergeBlock.UpdateBeforeSimulation10), BindingFlags.Public | BindingFlags.Instance )).
                Prefixes.Add(typeof(MergeBlockPatch).GetMethod(nameof(MergeCheck), BindingFlags.NonPublic | BindingFlags.Static));

            ctx.GetPattern(typeof(MyShipMergeBlock).GetMethod("AddConstraint",  BindingFlags.NonPublic|BindingFlags.Instance )).
                Suffixes.Add(typeof(MergeBlockPatch).GetMethod(nameof(AddBlocks), BindingFlags.NonPublic | BindingFlags.Static));

        }
        

        private static bool MergeCheck(MyShipMergeBlock __instance)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits) return true;

            var mergeBlock = __instance;

            if (mergeBlock?.Other == null )
                return true;
            
            if (mergeBlock.IsLocked || !mergeBlock.IsFunctional || !mergeBlock.Other.IsFunctional) return true;


            if (BlockLimiterConfig.Instance.MergerBlocking)
            {
                if (!Grid.CanMerge(mergeBlock.CubeGrid, mergeBlock.Other.CubeGrid))
                {
                    mergeBlock.Enabled = false;

                    mergeBlock.SlimBlock.DoDamage(mergeBlock.BlockDefinition.MaxIntegrity * mergeBlock.BlockDefinition.CriticalIntegrityRatio, MyDamageType.Explosion);

                    Log.Info($"Blocked merger between {mergeBlock.CubeGrid?.DisplayName} and {mergeBlock.Other?.CubeGrid?.DisplayName}");
                    return false;
                }
                
            }

            return true;
        }

        private static void AddBlocks(MyShipMergeBlock __instance)
        {
            var id = __instance.CubeGrid.EntityId;

            Task.Run((() =>
            {
                Thread.Sleep(10000);
                if (!GridCache.TryGetGridById(id, out var grid))
                {
                    return;
                }

                UpdateLimits.GridLimit(grid);

            }));
        }
    }
}
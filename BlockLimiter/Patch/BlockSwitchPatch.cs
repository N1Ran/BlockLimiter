using System.Collections.Generic;
using System.Reflection;
using BlockLimiter.Settings;
using NLog;
using Sandbox.Game.Entities.Cube;
using Sandbox.ModAPI;
using SpaceEngineers.Game.Entities.Blocks;
using Torch.Managers.PatchManager;

namespace BlockLimiter.Patch
{
    [PatchShim]
    public static class BlockSwitchPatch
    {
        public static readonly HashSet<MyFunctionalBlock> KeepOffBlocks = new HashSet<MyFunctionalBlock>();
        private static readonly Logger Log = BlockLimiter.Instance.Log;

        public static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(typeof(MyFunctionalBlock).GetMethod("UpdateBeforeSimulation10", BindingFlags.Instance | BindingFlags.Public)).
                Prefixes.Add(typeof(BlockSwitchPatch).GetMethod(nameof(KeepBlocksOff), BindingFlags.Static| BindingFlags.Instance| BindingFlags.NonPublic));

            ctx.GetPattern(typeof(MyFunctionalBlock).GetMethod("UpdateBeforeSimulation100", BindingFlags.Instance | BindingFlags.Public)).
                Prefixes.Add(typeof(BlockSwitchPatch).GetMethod(nameof(KeepBlocksOff), BindingFlags.Static| BindingFlags.Instance| BindingFlags.NonPublic));

        }

        private static void KeepBlocksOff (MyFunctionalBlock __instance)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits)return;
            var block = __instance;
            
            if (block.Enabled == false) return;
            if (KeepOffBlocks.Contains(block))
            {
                block.Enabled = false;
                Log.Info(
                    $"Turned off {block.BlockDefinition?.BlockPairName} from {block.CubeGrid?.DisplayName}");
                return;
            }
            if (block.Enabled == false || block is MyParachute || block is MyButtonPanel ||
                block is IMyPowerProducer || block.BlockDefinition?.ContainsComputer() == false)
            {
                return;
            }

            if (!BlockLimiterConfig.Instance.KillNoOwnerBlocks || block.OwnerId != 0) return;
            block.Enabled = false;
        }
    }
}
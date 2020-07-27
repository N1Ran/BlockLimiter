using System.Collections.Generic;
using System.Reflection;
using NLog;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Entities.Cube;
using Torch.Managers.PatchManager;
using VRage.Game.Entity;

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

        }

        private static void KeepBlocksOff (MyFunctionalBlock __instance)
        {
            var block = __instance;
            if (block.Enabled == false || !KeepOffBlocks.Contains(block)) return;
            block.Enabled = false;
            Log.Info(
                $"Turned off {block.BlockDefinition.BlockPairName} from {block.CubeGrid.DisplayName}");
        }
    }
}
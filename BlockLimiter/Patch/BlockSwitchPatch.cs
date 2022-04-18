using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BlockLimiter.Settings;
using NLog;
using Sandbox.Game.Entities.Cube;
using Sandbox.ModAPI;
using SpaceEngineers.Game.Entities.Blocks;
using SpaceEngineers.Game.ModAPI;
using Torch.Managers.PatchManager;
using VRage.Collections;

namespace BlockLimiter.Patch
{
    [PatchShim]
    public static class BlockSwitchPatch
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public static void Patch(PatchContext ctx)
        {
            
            try
            {
                ctx.GetPattern(typeof(MyFunctionalBlock).GetMethod("UpdateBeforeSimulation10", BindingFlags.Instance | BindingFlags.Public)).
                    Prefixes.Add(typeof(BlockSwitchPatch).GetMethod(nameof(KeepBlocksOff), BindingFlags.Static| BindingFlags.Instance| BindingFlags.NonPublic));

                ctx.GetPattern(typeof(MyFunctionalBlock).GetMethod("UpdateBeforeSimulation100", BindingFlags.Instance | BindingFlags.Public)).
                    Prefixes.Add(typeof(BlockSwitchPatch).GetMethod(nameof(KeepBlocksOff), BindingFlags.Static| BindingFlags.Instance| BindingFlags.NonPublic));
            }
            catch (Exception e)
            {
                Log.Error(e.StackTrace, "Patching Failed");
            }


        }

        private static void KeepBlocksOff (MyFunctionalBlock __instance)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits || !BlockLimiterConfig.Instance.KillNoOwnerBlocks || __instance.OwnerId != 0)return;
            var block = __instance;
            
            if (block.Enabled == false || block is MyParachute || block is MyButtonPanel ||
                block is IMyPowerProducer || block.BlockDefinition?.ContainsComputer() == false || block is IMyThrust || block is IMyGyro || block is IMyMedicalRoom 
                || block.IsPreview)
            {
                return;
            }
            
            BlockLimiter.Instance.Log.Info($"Keeping {block.BlockDefinition?.Id.ToString().Substring(16)} from {block.CubeGrid?.DisplayName} off due to no ownership");
            block.Enabled = false;
        }
    }
}
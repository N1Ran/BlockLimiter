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

            if (__instance.Enabled == false || __instance is MyParachute || __instance is MyButtonPanel ||
                __instance is IMyPowerProducer || __instance.BlockDefinition?.ContainsComputer() == false || __instance is IMyThrust || __instance is IMyGyro || __instance.CubeGrid.Projector != null)
            {
                return;
            }
            
            BlockLimiter.Instance.Log.Info($"Keeping {__instance.BlockDefinition?.Id.ToString().Substring(16)} from {__instance.CubeGrid?.DisplayName} off due to no ownership");
            __instance.Enabled = false;
        }
    }
}
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BlockLimiter.Settings;
using BlockLimiter.Utility;
using Sandbox;
using Sandbox.Game.Entities.Cube;
using Torch.Managers.PatchManager;

namespace BlockLimiter.Patch
{
    [PatchShim]
    public static class BlockOwnershipTransfer
    {
        private static void Patch(PatchContext ctx)
        {
            var t = typeof(MySlimBlock);

            //ctx.GetPattern(typeof(MySlimBlock).GetMethod(nameof(MySlimBlock.TransferAuthorship))).Prefixes.
              //  Add(typeof(BlockOwnershipTransfer).GetMethod(nameof(OnTransfer), BindingFlags.Static | BindingFlags.NonPublic));
            
            var m = t.GetMethod(nameof(MySlimBlock.TransferAuthorship), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            ctx.GetPattern(m).Prefixes.Add(typeof(BlockOwnershipTransfer).GetMethod(nameof(OnTransfer),BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));

            
        }

        public static event Action<MySlimBlock, long> SlimOwnerChanged;

        // ReSharper disable once InconsistentNaming
        private static bool OnTransfer(MySlimBlock __instance, long newOwner)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits || !BlockLimiterConfig.Instance.BlockOwnershipTransfer)
            {
                SlimOwnerChanged?.Invoke(__instance, newOwner);
                return true;
            }
            
            if (!Block.AllowBlock(__instance.BlockDefinition, newOwner, __instance.CubeGrid.EntityId))
            {
                Utilities.ValidationFailed();
                return false;
            }
            
            Block.RemoveBlock(__instance);

            if (!Block.TryAdd(__instance.BlockDefinition,newOwner, __instance.CubeGrid.EntityId))
            {
                Task.Run(() =>
                {
                    Thread.Sleep(100);
                    MySandboxGame.Static.Invoke(() =>
                    {
                        Block.UpdatePlayerLimits(newOwner);
                    }, "BlockLimiter");
                });
            }
            
            SlimOwnerChanged?.Invoke(__instance, newOwner);
            return true;
        }
    }
}
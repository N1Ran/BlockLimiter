using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BlockLimiter.Settings;
using BlockLimiter.Utility;
using Sandbox;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using Torch.Managers.PatchManager;

namespace BlockLimiter.Patch
{
    [PatchShim]
    public static class BlockOwnershipTransfer
    {
        private static void Patch(PatchContext ctx)
        {
            var t = typeof(MySlimBlock);
            var m = t.GetMethod(nameof(MySlimBlock.TransferAuthorship), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            ctx.GetPattern(m).Prefixes.Add(typeof(BlockOwnershipTransfer).GetMethod(nameof(OnTransfer),BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
        }

        public static event Action<MySlimBlock, long> SlimOwnerChanged;

        private static bool OnTransfer(MySlimBlock __instance, long newOwner)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits || !BlockLimiterConfig.Instance.BlockOwnershipTransfer)
            {
                SlimOwnerChanged?.Invoke(__instance, newOwner);
                return true;
            }

            var block = __instance;
            
            if (block == null)
                return false;
            
            if (!Block.AllowBlock(block.BlockDefinition, newOwner, block.CubeGrid.EntityId))
            {
                Utilities.ValidationFailed();
                return false;
            }
            
           
            Block.RemoveBlock(block);
            block.TransferAuthorship(newOwner);

            if (!Block.TryAdd(block.BlockDefinition,newOwner, block.CubeGrid.EntityId))
            {
                Task.Run(() =>
                {
                    Thread.Sleep(100);
                    MySandboxGame.Static.Invoke(() =>
                    {
                        UpdateLimits.PlayerLimit(newOwner);
                    }, "BlockLimiter");
                });
            }
            
            
            SlimOwnerChanged?.Invoke(__instance, newOwner);
            return true;
        }
    }
}
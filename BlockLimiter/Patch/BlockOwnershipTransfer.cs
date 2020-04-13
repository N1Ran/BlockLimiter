using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BlockLimiter.Settings;
using BlockLimiter.Utility;
using Sandbox;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using Torch.Managers;
using Torch.Managers.PatchManager;
using VRage.Game;
using VRage.Network;

namespace BlockLimiter.Patch
{
    [PatchShim]
    public static class BlockOwnershipTransfer
    {
        
        //private static  readonly MethodInfo TransferAuthor = typeof(MySlimBlock).GetMethod(nameof(MySlimBlock.TransferAuthorship), BindingFlags.NonPublic | BindingFlags.Instance);
        //private static readonly MethodInfo TransferOwner = typeof(MyCubeBlock).GetMethod(nameof(MyCubeBlock.ChangeBlockOwnerRequest), BindingFlags.NonPublic | BindingFlags.Instance);

        private static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(typeof(MySlimBlock).GetMethod(nameof(MySlimBlock.TransferAuthorship), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)).
                Prefixes.Add(typeof(BlockOwnershipTransfer).GetMethod(nameof(OnTransfer), BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));

            /*
            var t = typeof(MySlimBlock);
            var m = t.GetMethod(nameof(MySlimBlock.TransferAuthorship), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            ctx.GetPattern(m).Prefixes.Add(typeof(BlockOwnershipTransfer).GetMethod(nameof(OnTransfer),BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
            */
            
            var tMyCubeBlock = typeof(MyCubeBlock);
            var mMyCubeBlock = tMyCubeBlock.GetMethod(nameof(MyCubeBlock.ChangeOwner), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            ctx.GetPattern(mMyCubeBlock).Prefixes.Add(typeof(BlockOwnershipTransfer).GetMethod(nameof(ChangeOwner),BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
        }

        public static event Action<MySlimBlock, long> SlimOwnerChanged;

        private static bool OnTransfer(MySlimBlock __instance, long newOwner)
        {
            
            if (!BlockLimiterConfig.Instance.EnableLimits || !BlockLimiterConfig.Instance.BlockOwnershipTransfer)
            {
                return true;
            }

            var block = __instance;
            var oldId = __instance.BuiltBy;
            
            if (block == null)
                return false;
            if (!Block.AllowBlock(block.BlockDefinition, newOwner,0))
            {
                Utilities.ValidationFailed();
                return false;
            }
            
            Block.TryRemove(block.BlockDefinition,oldId);

            Block.TryAdd(block.BlockDefinition, newOwner);
            SlimOwnerChanged?.Invoke(__instance, newOwner);
            return true;
        }
        private static bool ChangeOwner(MyCubeBlock __instance, long owner, MyOwnershipShareModeEnum shareMode)
        {
            
            if (!BlockLimiterConfig.Instance.EnableLimits || !BlockLimiterConfig.Instance.BlockOwnershipTransfer)
            {
                return true;
            }

            var block = __instance;
            
            if (block == null || owner == 0)
                return false;

            
            if (!Block.AllowBlock(block.BlockDefinition, owner,0))
            {
                Utilities.ValidationFailed();
                return false;
            }
            
            __instance.SlimBlock.TransferAuthorship(owner);
            
            return true;
        }
    }
}
using System;
using System.Collections.Generic;
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
        private static  readonly MethodInfo Changed = typeof(MyCubeBlock).GetMethod("ChangeOwner", BindingFlags.Public | BindingFlags.Instance);


        private static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(typeof(MySlimBlock).GetMethod(nameof(MySlimBlock.TransferAuthorship), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)).
                Prefixes.Add(typeof(BlockOwnershipTransfer).GetMethod(nameof(OnTransfer), BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));

            ctx.GetPattern(typeof(MyCubeGrid).GetMethod(nameof(MyCubeGrid.ChangeOwnerRequest), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)).
                Prefixes.Add(typeof(BlockOwnershipTransfer).GetMethod(nameof(ChangeOwner), BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
            
            ctx.GetPattern(typeof(MyCubeGrid).GetMethod("OnChangeOwnersRequest", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)).
                Prefixes.Add(typeof(BlockOwnershipTransfer).GetMethod(nameof(ChangeOwnerRequest), BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
        }

        public static event Action<MySlimBlock, long> SlimOwnerChanged;

        private static bool ChangeOwnerRequest(List<MyCubeGrid.MySingleOwnershipRequest> requests, long requestingPlayer)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits) return true;
            var blocks = new List<MySlimBlock>();
            
            foreach (var request in requests)
            {
                if (!MyEntities.TryGetEntityById<MyCubeBlock>(request.BlockId, out var entity, false)) continue;
                blocks.Add(entity.SlimBlock);
            }

            if (blocks.Count == 0)
            {
                return true;
            }

            if (!Block.CanAdd(blocks, requestingPlayer, out _))
            {
                Utilities.ValidationFailed();
                Utilities.SendFailSound(Utilities.GetSteamIdFromPlayerId(requests[0].Owner));
                return false;
            }
            
            return true;
        }

        private static bool OnTransfer(MySlimBlock __instance, long newOwner)
        {
            
            if (!BlockLimiterConfig.Instance.EnableLimits)
            {
                return true;
            }

            var block = __instance;
            var oldId = __instance.BuiltBy;
            
            if (block == null)
                return false;

            if (BlockLimiterConfig.Instance.BlockOwnershipTransfer)
            {
                if (!Block.AllowBlock(block.BlockDefinition, newOwner, 0))
                {
                    Utilities.ValidationFailed();
                    return false;
                }
            }
            
            if (!Block.TryRemove(block.BlockDefinition,oldId))
            {

                Parallel.Invoke(() =>
                {
                    Thread.Sleep(500);
                    UpdateLimits.PlayerLimit(oldId);
                });
            }
            
            if (!Block.TryAdd(block.BlockDefinition,newOwner))
            {

                Parallel.Invoke(() =>
                {
                    Thread.Sleep(500);
                    UpdateLimits.PlayerLimit(oldId);
                });
            }
            
            SlimOwnerChanged?.Invoke(__instance, newOwner);
            return true;
        }
        private static bool ChangeOwner(MyCubeBlock block, long playerId)
        {
            
            if (!BlockLimiterConfig.Instance.EnableLimits)
            {
                return true;
            }


            
            if (block == null || playerId == 0)
                return false;
            
            if (BlockLimiterConfig.Instance.BlockOwnershipTransfer)
            {
                if (!Block.AllowBlock(block.BlockDefinition, playerId, 0))
                {
                    Utilities.ValidationFailed();
                    return false;
                }
            }
            
            block.ChangeOwner(playerId, MyOwnershipShareModeEnum.Faction);

            block.SlimBlock.TransferAuthorship(playerId);
            
            return true;
        }
    }
}
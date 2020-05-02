using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BlockLimiter.Settings;
using BlockLimiter.Utility;
using NLog;
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
        private static readonly Logger Log = BlockLimiter.Instance.Log;


        private static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(typeof(MySlimBlock).GetMethod(nameof(MySlimBlock.TransferAuthorship), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)).
                Prefixes.Add(typeof(BlockOwnershipTransfer).GetMethod(nameof(OnTransfer), BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));

            ctx.GetPattern(typeof(MyCubeGrid).GetMethod(nameof(MyCubeGrid.ChangeOwnerRequest), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)).
                Prefixes.Add(typeof(BlockOwnershipTransfer).GetMethod(nameof(ChangeOwner), BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
            
            ctx.GetPattern(typeof(MyCubeGrid).GetMethod("OnChangeOwnersRequest", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)).
                Prefixes.Add(typeof(BlockOwnershipTransfer).GetMethod(nameof(ChangeOwnersRequest), BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
        }

        public static event Action<MySlimBlock, long> SlimOwnerChanged;

        /// <summary>
        /// Checks if ownership can be changed
        /// </summary>
        /// <param name="requests"></param>
        /// <param name="requestingPlayer"></param>
        /// <returns></returns>
        private static bool ChangeOwnersRequest(List<MyCubeGrid.MySingleOwnershipRequest> requests, long requestingPlayer)
        {
            Log.Warn("ChangeOwnersRequest triggered");

            if (!BlockLimiterConfig.Instance.EnableLimits || !BlockLimiterConfig.Instance.BlockOwnershipTransfer) return true;
            var blocks = new List<MySlimBlock>();


            foreach (var request in requests)
            {
                if (!MyEntities.TryGetEntityById<MyCubeBlock>(request.BlockId, out var entity)) continue;
                blocks.Add(entity.SlimBlock);
            }


            if (blocks.Count == 0)
            {
                return true;
            }

            var newOwner = requests[0].Owner;


            if (newOwner == 0)
            {

                return true;
            }

            if (newOwner == requestingPlayer)
            {
                foreach (var block in blocks)
                {
                    block.TransferAuthorship(newOwner);
                }

                return true;
            }

            if (!Block.CanAdd(blocks, newOwner, out _))
            {
                if (BlockLimiterConfig.Instance.EnableLog)
                    Log.Info($"Ownership blocked {blocks.Count} blocks from {MySession.Static.Players.TryGetIdentity(requestingPlayer).DisplayName} to {MySession.Static.Players.TryGetIdentity(newOwner).DisplayName}");

                Utilities.ValidationFailed();
                Utilities.SendFailSound(Utilities.GetSteamIdFromPlayerId(requestingPlayer));
                return false;
            }

            foreach (var block in blocks)
            {
                block.TransferAuthorship(newOwner);
            }
            return true;

        }

        /// <summary>
        /// Check if block's authorship can be transferred
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="newOwner"></param>
        /// <returns></returns>
        private static bool OnTransfer(MySlimBlock __instance, long newOwner)
        {
            Log.Warn("OnTransfer triggered");
            if (!BlockLimiterConfig.Instance.EnableLimits)
            {
                return true;
            }

            if (newOwner == 0) return false;

            var block = __instance;
            if (block == null)
                return false;

            var oldId = block.BuiltBy;
            
            if (BlockLimiterConfig.Instance.BlockOwnershipTransfer)
            {
                if (!Block.IsWithinLimits(block.BlockDefinition, newOwner, 0))
                {
                    Utilities.ValidationFailed();
                    if (BlockLimiterConfig.Instance.EnableLog)
                        Log.Info($"Authorship transfer blocked for {block.BlockDefinition.ToString().Substring(16)} to {MySession.Static.Players.TryGetIdentity(newOwner)?.DisplayName}");
                    return false;
                }
            }


            Block.IncreaseCount(block.BlockDefinition,newOwner);
            Block.DecreaseCount(block.BlockDefinition,oldId);

            SlimOwnerChanged?.Invoke(__instance, newOwner);
            return true;
        }

        /// <summary>
        /// Makes sure authorship is transferred during ownership switch
        /// </summary>
        /// <param name="block"></param>
        /// <param name="playerId"></param>
        /// <returns></returns>
        private static bool ChangeOwner(MyCubeBlock block, long playerId)
        {
            Log.Warn("ChangOwner triggered");
            if (block == null) return false;
            
            if (!BlockLimiterConfig.Instance.EnableLimits)
            {
                return true;
            }

            
            if (BlockLimiterConfig.Instance.BlockOwnershipTransfer)
            {
                            
                if (playerId == 0)
                    return true;

                if (!Block.IsWithinLimits(block.BlockDefinition, playerId, 0))
                {
                    Utilities.ValidationFailed();
                    if (BlockLimiterConfig.Instance.EnableLog)
                        Log.Info($"Ownership blocked for {block.BlockDefinition.ToString().Substring(16)} to {MySession.Static.Players.TryGetIdentity(playerId)?.DisplayName}");

                    return false;
                }
            }

            
            block.ChangeOwner(playerId, MyOwnershipShareModeEnum.Faction);
            block.SlimBlock.TransferAuthorship(playerId);
            
            return true;
        }
    }
}
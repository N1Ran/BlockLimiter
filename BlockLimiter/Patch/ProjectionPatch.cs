using System;
using System.Collections.Generic;
using System.Reflection;
using BlockLimiter.Settings;
using Sandbox.Game.Entities.Blocks;
using Torch.Managers.PatchManager;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Game;
using VRage.Network;
using System.Linq;
using BlockLimiter.Utility;
using NLog;
using System.Threading;
using System.Threading.Tasks;
using Sandbox.Game;
using Sandbox.Game.World;
using Torch;
using Torch.Managers;

namespace BlockLimiter.Patch
{
    [PatchShim]
    public static class ProjectionPatch
    {
        private static readonly Logger Log = BlockLimiter.Instance.Log;

        private static readonly MethodInfo NewBlueprintMethod = typeof(MyProjectorBase).GetMethod("OnNewBlueprintSuccess", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo RemoveBlueprintMethod = typeof(MyProjectorBase).GetMethod("OnRemoveProjectionRequest", BindingFlags.NonPublic | BindingFlags.Instance);
        
        public static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(typeof(MyProjectorBase).GetMethod("OnNewBlueprintSuccess", BindingFlags.Instance | BindingFlags.NonPublic)).
                Prefixes.Add(typeof(ProjectionPatch).GetMethod(nameof(PrefixNewBlueprint), BindingFlags.Static| BindingFlags.Instance| BindingFlags.NonPublic));

            ctx.GetPattern(typeof(MyProjectorBase).GetMethod("RemoveProjection", BindingFlags.Instance | BindingFlags.NonPublic)).
                Prefixes.Add(typeof(ProjectionPatch).GetMethod(nameof(DecreaseProjectedCount), BindingFlags.Static| BindingFlags.Instance| BindingFlags.NonPublic));
        }


        private static void DecreaseProjectedCount(MyProjectorBase __instance)
        {
            if (!BlockLimiterConfig.Instance.CountProjections)return;
            MyIdentity myIdentity = MySession.Static.Players.TryGetIdentity(__instance.BuiltBy);

            var grid = __instance.ProjectedGrid;
            if (grid == null || myIdentity == null) return;
            foreach (var block in grid.CubeBlocks)
            {
                Block.DecreaseCount(block.BlockDefinition,myIdentity.IdentityId,1,__instance.CubeGrid.EntityId);
            }
        }


        /// <summary>
        /// Aim to block projections or remove blocks to match grid/player limits.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="projectedGrids"></param>
        /// <returns></returns>
        private static bool PrefixNewBlueprint(MyProjectorBase __instance, ref List<MyObjectBuilder_CubeGrid> projectedGrids)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits)return true;

            var proj = __instance;
            if (proj == null)
            {
                Log.Warn("No projector?");
                return false;
            }
            
            var grid = projectedGrids[0];

            if (grid == null)
            {
                Log.Warn("Grid null in projectorPatch");
                return false;
            }
            

            var remoteUserId = MyEventContext.Current.Sender.Value;
            var player = MySession.Static.Players.TryGetPlayerBySteamId(remoteUserId);
            
            var projectedBlocks = grid.CubeBlocks;

            if (player == null || projectedBlocks.Count == 0) return true;
            if (Utilities.IsExcepted(player.Identity.IdentityId, new List<string>())) return true;

            var playerId = player.Identity.IdentityId;
            if (Grid.IsSizeViolation(grid))
            {
                proj.SendRemoveProjection();
                NetworkManager.RaiseEvent(__instance,RemoveBlueprintMethod);
                Utilities.ValidationFailed();
                var msg1 = Utilities.GetMessage(BlockLimiterConfig.Instance.DenyMessage,new List<string>{"Null"},grid.CubeBlocks.Count);
                MyVisualScriptLogicProvider.SendChatMessage(msg1,BlockLimiterConfig.Instance.ServerName,playerId,MyFontEnum.Red);
                
                Log.Info($"Projection blocked from {player.DisplayName} due to size limit");
                
                return false;
            }

            
            var limits = new HashSet<LimitItem>();
            
            limits.UnionWith(BlockLimiterConfig.Instance.AllLimits.Where(x=>x.RestrictProjection));

            if (limits.Count == 0) return true;
            
            var count = 0;

            var playerFaction = MySession.Static.Factions.GetPlayerFaction(playerId);
            var removedList = new List<string>();
            foreach (var limit in limits)
            {
                if (Utilities.IsExcepted(player.Identity.IdentityId, limit.Exceptions)|| Utilities.IsExcepted(proj.CubeGrid.EntityId,limit.Exceptions)) continue;

                var pBlocks = new HashSet<MyObjectBuilder_CubeBlock>(projectedBlocks.Where(x => limit.IsMatch(Utilities.GetDefinition(x))));
                
                if (pBlocks.Count == 0) continue;

                var removalCount = 0;
                var fCount = 0;
                limit.FoundEntities.TryGetValue(grid.EntityId, out var gCount);
                limit.FoundEntities.TryGetValue(playerId, out var pCount);
                if (playerFaction != null)
                    limit.FoundEntities.TryGetValue(playerFaction.FactionId, out fCount);

                foreach (var block in pBlocks)
                {
                    if (Math.Abs(pBlocks.Count + pCount - removalCount) <= limit.Limit && Math.Abs(gCount + pBlocks.Count - removalCount) <= limit.Limit && Math.Abs(fCount + pBlocks.Count - removalCount) <= limit.Limit) break;
                    removalCount++;
                    count++;
                    projectedBlocks.Remove(block);
                    var blockDef = Utilities.GetDefinition(block).ToString().Substring(16);
                    if (removedList.Contains(blockDef))continue;
                    removedList.Add(blockDef);
                }

            }

            if ( count < 1) return true;
            
            Log.Info($"Removed {count} blocks from projector set by {player.DisplayName} ");

            NetworkManager.RaiseEvent(__instance,RemoveBlueprintMethod);

            try
            {
                
                NetworkManager.RaiseEvent(__instance, NewBlueprintMethod,
                    new List<MyObjectBuilder_CubeGrid> {grid});
            }
            catch (Exception e)
            {
                Log.Error(e);
            }

            var msg = Utilities.GetMessage(BlockLimiterConfig.Instance.ProjectionDenyMessage,removedList, count);

            MyVisualScriptLogicProvider.SendChatMessage(msg, BlockLimiterConfig.Instance.ServerName,playerId,MyFontEnum.Red);

            return true;


        }

       
    }
}
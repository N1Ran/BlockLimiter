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
        private static  readonly MethodInfo RemoveProjectionMethod = typeof(MyProjectorBase).GetMethod("OnRemoveProjectionRequest", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo NewBlueprintMethod = typeof(MyProjectorBase).GetMethod("OnNewBlueprintSuccess", BindingFlags.NonPublic | BindingFlags.Instance);
       
        
        public static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(typeof(MyProjectorBase).GetMethod("OnNewBlueprintSuccess", BindingFlags.Instance | BindingFlags.NonPublic)).
                Prefixes.Add(typeof(ProjectionPatch).GetMethod(nameof(PrefixNewBlueprint), BindingFlags.Static| BindingFlags.Instance| BindingFlags.NonPublic));
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
            
            var blocks = grid.CubeBlocks;

            if (player == null || blocks.Count == 0) return true;
            if (Utilities.IsExcepted(player.Identity.IdentityId, new List<string>())) return true;

            var target = new EndpointId(remoteUserId);
            var playerId = player.Identity.IdentityId;
            if (Grid.IsSizeViolation(grid))
            {
                Task.Run(() =>
                {
                    Thread.Sleep(100);
                    NetworkManager.RaiseEvent(__instance, RemoveProjectionMethod, target);
                });
                Utilities.SendFailSound(remoteUserId);
                Utilities.ValidationFailed();
                MyVisualScriptLogicProvider.SendChatMessage($"{BlockLimiterConfig.Instance.DenyMessage}",BlockLimiterConfig.Instance.ServerName,playerId,MyFontEnum.Red);
                if (BlockLimiterConfig.Instance.EnableLog)
                    Log.Info($"Projection blocked from {player.DisplayName}");
                return false;
            }

            
            var limits = new HashSet<LimitItem>();
            
            limits.UnionWith(BlockLimiterConfig.Instance.AllLimits.Where(x=>x.RestrictProjection && !Utilities.IsExcepted(grid.EntityId, x.Exceptions) &&
                                                                            !Utilities.IsExcepted(player.Identity.IdentityId, x.Exceptions)));

            if (limits.Count == 0) return true;
            
            var count = 0;

            var playerFaction = MySession.Static.Factions.GetPlayerFaction(playerId);

            foreach (var limit in limits)
            {

                var pBlocks = blocks.Where(x => Block.IsMatch(Utilities.GetDefinition(x), limit)).ToList();
                
                if (pBlocks.Count < 1) continue;

                var removalCount = 0;
                var fCount = 0;
                limit.FoundEntities.TryGetValue(grid.EntityId, out var gCount);
                limit.FoundEntities.TryGetValue(playerId, out var oCount);
                if (playerFaction != null)
                    limit.FoundEntities.TryGetValue(playerFaction.FactionId, out fCount);

                foreach (var block in pBlocks)
                {
                    if (Math.Abs(pBlocks.Count - removalCount) <= limit.Limit || Math.Abs(fCount+pBlocks.Count-removalCount)<=limit.Limit && (Math.Abs((oCount + pBlocks.Count) - removalCount) <= limit.Limit && Math.Abs((gCount + pBlocks.Count) - removalCount) <= limit.Limit))
                        break;
                    
                    removalCount++;
                    count++;
                    blocks.Remove(block);
                }

            }

            if ( count < 1) return true;
            

            NetworkManager.RaiseEvent(__instance, RemoveProjectionMethod, target);

            try
            {
                NetworkManager.RaiseEvent(__instance, NewBlueprintMethod,
                    new List<MyObjectBuilder_CubeGrid> {grid});
            }
            catch (Exception e)
            {
                //NullException thrown here but seems to work for some reason.  Don't Touch any further
            }

            var msg = BlockLimiterConfig.Instance.ProjectionDenyMessage.Replace("{BC}", $"{count}");

            MyVisualScriptLogicProvider.SendChatMessage($"{msg}",BlockLimiterConfig.Instance.ServerName,playerId,MyFontEnum.Red);

            return true;


        }

       
    }
}
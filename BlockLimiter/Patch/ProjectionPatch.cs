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
using Sandbox.ModAPI;
using Torch;
using Torch.API.Managers;
using Torch.Managers;
using Torch.Managers.ChatManager;
using VRage.GameServices;
using VRage.Serialization;
using VRageMath;

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

           /* ctx.GetPattern(typeof(MyProjectorBase).GetMethod("OnNewBlueprintSuccess", BindingFlags.Instance | BindingFlags.NonPublic)).
                Prefixes.Add(typeof(ProjectionPatch).GetMethod(nameof(PrefixNewBlueprint), BindingFlags.Static| BindingFlags.Instance| BindingFlags.NonPublic));
           */
            ctx.GetPattern(typeof(MyProjectorBase).GetMethod("InitializeClipboard", BindingFlags.Instance | BindingFlags.NonPublic)).
                Prefixes.Add(typeof(ProjectionPatch).GetMethod(nameof(PrefixInitializeClipboard), BindingFlags.Static| BindingFlags.Instance| BindingFlags.NonPublic));

        }


        /// <summary>
        /// Checks projections before showing and remove any nonAllowed blocks.
        /// </summary>
        /// <param name="__instance"></param>
        /// <returns></returns>
        private static bool PrefixInitializeClipboard(MyProjectorBase __instance)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits) return true;

            var remoteUserId = MyEventContext.Current.Sender.Value;
            var grid = __instance.CubeGrid;
            var copiedGrid = __instance.Clipboard.CopiedGrids[0];
            
            if (copiedGrid == null || grid == null) return true;

            var player = MySession.Static.Players.TryGetPlayerBySteamId(remoteUserId);
            
            var projectedBlocks = copiedGrid.CubeBlocks;


            if (player == null || projectedBlocks.Count == 0)
            {
                return true;
            }


            if (Utilities.IsExcepted(player))
            {
                return true;
            }

            var playerId = player.Identity.IdentityId;
            if (Grid.IsSizeViolation(copiedGrid) || BlockLimiterConfig.Instance.MaxBlockSizeProjections < 0 || (projectedBlocks.Count > BlockLimiterConfig.Instance.MaxBlockSizeProjections && BlockLimiterConfig.Instance.MaxBlockSizeProjections > 0) || Grid.CountViolation(copiedGrid,playerId))
            {
                NetworkManager.RaiseEvent(__instance,RemoveBlueprintMethod, new EndpointId(remoteUserId));
                Utilities.ValidationFailed();
                var msg1 = Utilities.GetMessage(BlockLimiterConfig.Instance.DenyMessage,new List<string>{"Null"},"SizeViolation",grid.CubeBlocks.Count);
                if (remoteUserId != 0 && MySession.Static.Players.IsPlayerOnline(player.Identity.IdentityId))
                    BlockLimiter.Instance.Torch.CurrentSession.Managers.GetManager<ChatManagerServer>()?
                        .SendMessageAsOther(BlockLimiterConfig.Instance.ServerName, msg1, Color.Red, remoteUserId);
                
                Log.Info($"Projection blocked from {player.DisplayName} due to size limit");
                
                return false;
            }

            
            var limits = new HashSet<LimitItem>();
            
            limits.UnionWith(BlockLimiterConfig.Instance.AllLimits.Where(x=>x.RestrictProjection));

            if (limits.Count == 0)
            {
                return true;
            }
            
            var count = 0;

            var playerFaction = MySession.Static.Factions.GetPlayerFaction(playerId);
            string limitName = null;
            var removedList = new List<string>();
            foreach (var limit in limits)
            {
                if (limit.IsExcepted(player)|| limit.IsExcepted(__instance.CubeGrid)) continue;

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
                    limitName = limit.Name;
                }

            }

            if ( count == 0) return true;
            
            Log.Info($"Removed {count} blocks from projector set by {player.DisplayName} ");

            NetworkManager.RaiseEvent(__instance,RemoveBlueprintMethod);

            try
            {
                ((IMyProjector)__instance).SetProjectedGrid(copiedGrid);
            }
            catch (Exception e)
            {
                Log.Error(e);
            }

            var msg = Utilities.GetMessage(BlockLimiterConfig.Instance.ProjectionDenyMessage,removedList, limitName,count);

            if (remoteUserId != 0 && MySession.Static.Players.IsPlayerOnline(player.Identity.IdentityId))
                BlockLimiter.Instance.Torch.CurrentSession.Managers.GetManager<ChatManagerServer>()?
                    .SendMessageAsOther(BlockLimiterConfig.Instance.ServerName, msg, Color.Red, remoteUserId);

            return true;
        }

        /*
        /// <summary>
        /// Aim to block projections or remove blocks to match grid/player limits.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="projectedGrids"></param>
        /// <returns></returns>
        private static bool PrefixNewBlueprint(MyProjectorBase __instance, ref List<MyObjectBuilder_CubeGrid> projectedGrids)
        {
            Log.Warn("Checking  Projected Grid");
            var remoteUserId = MyEventContext.Current.Sender.Value;
            var proj = __instance;
            if (proj == null)
            {
                Log.Warn("No projector? Fuck this game");
                return false;
            }

            bool changesMade = false;
            if (BlockLimiter.DPBInstalled)
            {
                try
                {
                    object[] parameters = {projectedGrids, remoteUserId,null};
                    BlockLimiter.DPBCanAdd?.Invoke(null, parameters);
                    changesMade = (bool) parameters[2];
                }
                catch (Exception e)
                {
                  Log.Warn(e,"DPB was unable to check projection");
                }
            }

            if (changesMade) NetworkManager.RaiseEvent(__instance,RemoveBlueprintMethod, new EndpointId(remoteUserId));

            var grid = projectedGrids[0];

            if (grid == null)
            {
                Log.Warn("Grid null in projectorPatch");
                return false;
            }


            if (!BlockLimiterConfig.Instance.EnableLimits)
            {
                if (changesMade)NetworkManager.RaiseEvent(__instance, NewBlueprintMethod,new List<MyObjectBuilder_CubeGrid> {grid});
                return true;
            }

           

            var player = MySession.Static.Players.TryGetPlayerBySteamId(remoteUserId);
            
            var projectedBlocks = grid.CubeBlocks;

            if (player == null || projectedBlocks.Count == 0)
            {
                if (changesMade)NetworkManager.RaiseEvent(__instance, NewBlueprintMethod,new List<MyObjectBuilder_CubeGrid> {grid});
                return true;
            }

            if (Utilities.IsExcepted(player))
            {
                if (changesMade)NetworkManager.RaiseEvent(__instance, NewBlueprintMethod,new List<MyObjectBuilder_CubeGrid> {grid});
                return true;
            }

            var playerId = player.Identity.IdentityId;
            if (Grid.IsSizeViolation(grid) || BlockLimiterConfig.Instance.MaxBlockSizeProjections < 0 || (projectedBlocks.Count > BlockLimiterConfig.Instance.MaxBlockSizeProjections && BlockLimiterConfig.Instance.MaxBlockSizeProjections > 0) || Grid.CountViolation(grid,playerId))
            {
                NetworkManager.RaiseEvent(__instance,RemoveBlueprintMethod, new EndpointId(remoteUserId));
                Utilities.ValidationFailed();
                var msg1 = Utilities.GetMessage(BlockLimiterConfig.Instance.DenyMessage,new List<string>{"Null"},"SizeViolation",grid.CubeBlocks.Count);
                if (remoteUserId != 0 && MySession.Static.Players.IsPlayerOnline(player.Identity.IdentityId))
                    BlockLimiter.Instance.Torch.CurrentSession.Managers.GetManager<ChatManagerServer>()?
                        .SendMessageAsOther(BlockLimiterConfig.Instance.ServerName, msg1, Color.Red, remoteUserId);
                
                Log.Info($"Projection blocked from {player.DisplayName} due to size limit");
                
                return false;
            }

            
            var limits = new HashSet<LimitItem>();
            
            limits.UnionWith(BlockLimiterConfig.Instance.AllLimits.Where(x=>x.RestrictProjection));

            if (limits.Count == 0)
            {
                if (changesMade)NetworkManager.RaiseEvent(__instance, NewBlueprintMethod,new List<MyObjectBuilder_CubeGrid> {grid});
                return true;
            }
            
            var count = 0;

            var playerFaction = MySession.Static.Factions.GetPlayerFaction(playerId);
            string limitName = null;
            var removedList = new List<string>();
            foreach (var limit in limits)
            {
                if (limit.IsExcepted(player)|| limit.IsExcepted(proj.CubeGrid)) continue;

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
                    limitName = limit.Name;
                }

            }

            if ( count == 0) return true;
            
            Log.Info($"Removed {count} blocks from projector set by {player.DisplayName} ");

            NetworkManager.RaiseEvent(__instance,RemoveBlueprintMethod);

            try
            {
                
                NetworkManager.RaiseEvent(__instance, NewBlueprintMethod,
                    new List<MyObjectBuilder_CubeGrid> {grid}, new EndpointId(remoteUserId));
            }
            catch (Exception e)
            {
                Log.Error(e);
            }

            var msg = Utilities.GetMessage(BlockLimiterConfig.Instance.ProjectionDenyMessage,removedList, limitName,count);

            if (remoteUserId != 0 && MySession.Static.Players.IsPlayerOnline(player.Identity.IdentityId))
                BlockLimiter.Instance.Torch.CurrentSession.Managers.GetManager<ChatManagerServer>()?
                    .SendMessageAsOther(BlockLimiterConfig.Instance.ServerName, msg, Color.Red, remoteUserId);

            return true;


        }
        */

       
    }
}
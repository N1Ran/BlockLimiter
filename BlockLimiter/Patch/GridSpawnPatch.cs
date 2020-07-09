using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BlockLimiter.Settings;
using BlockLimiter.Utility;
using NLog;
using ParallelTasks;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.SessionComponents;
using Sandbox.Game.World;
using Torch;
using Torch.Managers;
using Torch.Managers.PatchManager;
using VRage.Game;
using VRage.Network;

namespace BlockLimiter.Patch
{
    [PatchShim]
    public static class GridSpawnPatch
    {
        private static readonly Logger Log = BlockLimiter.Instance.Log;

        private static readonly MethodInfo ShowPasteFailed =
            typeof(MyCubeGrid).GetMethod("SendHudNotificationAfterPaste", BindingFlags.Static | BindingFlags.Public);
        
        private static readonly MethodInfo SpawnGrid =
            typeof(MyCubeGrid).GetMethod("TryPasteGrid_Implementation", BindingFlags.Static | BindingFlags.Public);


        public static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(typeof(MyCubeBuilder).GetMethod("RequestGridSpawn", BindingFlags.NonPublic | BindingFlags.Static))
                .Prefixes.Add(typeof(GridSpawnPatch).GetMethod(nameof(Prefix),BindingFlags.NonPublic|BindingFlags.Static));
            
            ctx.GetPattern(typeof(MyCubeGrid).GetMethod("TryPasteGrid_Implementation",  BindingFlags.Public  |  BindingFlags.Static)).
                Prefixes.Add(typeof(GridSpawnPatch).GetMethod(nameof(AttemptSpawn), BindingFlags.Static |  BindingFlags.NonPublic));
            

        }

        /// <summary>
        /// Decides if grid being spawned is permitted
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        private static bool AttemptSpawn(MyCubeGrid.MyPasteGridParameters parameters)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits) return true;
            var grids = parameters.Entities;

            if (grids.Count == 0) return false;

            var remoteUserId = MyEventContext.Current.Sender.Value;

            if (remoteUserId == 0) return true;

            var playerId = Utilities.GetPlayerIdFromSteamId(remoteUserId);
            var playerName = MySession.Static.Players.TryGetIdentity(playerId)?.DisplayName;

            grids.RemoveAll(x => Grid.IsSizeViolation(x));

            if (grids.Count == 0)
            {
                Log.Info($"Blocked {playerName} from spawning a grid");

                if (remoteUserId > 0)
                {
                    Thread.Sleep(100);
                    Utilities.ValidationFailed();
                    Utilities.SendFailSound(remoteUserId);
                    NetworkManager.RaiseStaticEvent(ShowPasteFailed, new EndpointId(remoteUserId), null);
                }

                return false;
            }

            var playerFaction = MySession.Static.Factions.GetPlayerFaction(playerId);

            var removalCount = 0;
            var removedList = new List<string>();
            foreach (var grid in grids)
            {
                foreach (var limit in BlockLimiterConfig.Instance.AllLimits)
                {
                    var fCount = 0;

                    if (Utilities.IsExcepted(playerId, limit.Exceptions)) continue;
                    var matchBlocks = new HashSet<MyObjectBuilder_CubeBlock>(grid.CubeBlocks.Where(x => Block.IsMatch(Utilities.GetDefinition(x), limit)));
                    limit.FoundEntities.TryGetValue(playerId, out var pCount);
                    if (playerFaction != null)
                        limit.FoundEntities.TryGetValue(playerFaction.FactionId, out fCount);

                    foreach (var block in matchBlocks)
                    {
                        if (Math.Abs(matchBlocks.Count + pCount - removalCount) <= limit.Limit && Math.Abs(fCount + matchBlocks.Count - removalCount) <= limit.Limit) break;
                        removalCount++;
                        var blockDef = Utilities.GetDefinition(block).ToString().Substring(16);
                        grid.CubeBlocks.Remove(block);
                        if (removedList.Contains(blockDef))
                            continue;
                        removedList.Add(blockDef);
                    }

                }
            }

            if (removalCount == 0) return true;
            
            
            parameters.Entities = grids;


            var msg = Utilities.GetMessage(BlockLimiterConfig.Instance.DenyMessage,removedList,removalCount);

            MyVisualScriptLogicProvider.SendChatMessage($"{msg}",BlockLimiterConfig.Instance.ServerName,playerId,MyFontEnum.Red);


            Log.Info($"Removed {removalCount} blocks from grid spawned by {MySession.Static.Players.TryGetIdentity(playerId)?.DisplayName}");
            return true;
            /*
            if (grids.All(x => Grid.CanSpawn(x, playerId)))
            {
                return true;
            }

            Log.Info($"Blocked {MySession.Static.Players.TryGetIdentity(playerId)?.DisplayName} from spawning a grid");
            
            MyVisualScriptLogicProvider.SendChatMessage($"{BlockLimiterConfig.Instance.DenyMessage}", BlockLimiterConfig.Instance.ServerName, playerId, MyFontEnum.Red);

            //This is needed to keep the wheel of misery away (staticEvent specifically)

            if (remoteUserId > 0)
            {
                Task.Run(() =>
                {
                    Thread.Sleep(100);
                    Utilities.ValidationFailed();
                    Utilities.SendFailSound(remoteUserId);
                    NetworkManager.RaiseStaticEvent(ShowPasteFailed, new EndpointId(remoteUserId), null);
                });
            }

            return false;
            */
        }

        /// <summary>
        /// Decides if a block about to be spawned is permitted by the player
        /// </summary>
        /// <param name="definition"></param>
        /// <returns></returns>
        private static bool Prefix(DefinitionIdBlit definition)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits) return true;
            
            var block = MyDefinitionManager.Static.GetCubeBlockDefinition(definition);
            
            if (block == null)
            {
                return true;
            }
            
            var remoteUserId = MyEventContext.Current.Sender.Value;
            var player = MySession.Static.Players.TryGetPlayerBySteamId(remoteUserId);
            var playerId = player.Identity.IdentityId;

            if (Block.IsWithinLimits(block, playerId, null))
            {
                return true;
            }


            var p = player.DisplayName;

            Log.Info($"Blocked {p} from placing {block}");

            //ModCommunication.SendMessageTo(new NotificationMessage($"You've reach your limit for {b}",5000,MyFontEnum.Red),remoteUserId );
            var msg = Utilities.GetMessage(BlockLimiterConfig.Instance.DenyMessage,new List<string>{block.ToString().Substring(16)});
            MyVisualScriptLogicProvider.SendChatMessage($"{msg}", BlockLimiterConfig.Instance.ServerName, playerId, MyFontEnum.Red);

            Utilities.SendFailSound(remoteUserId);
            Utilities.ValidationFailed();

            return false;
        }


    }
}

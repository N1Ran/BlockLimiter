using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using BlockLimiter.Settings;
using BlockLimiter.Utility;
using NLog;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Torch;
using Torch.API.Managers;
using Torch.Managers;
using Torch.Managers.ChatManager;
using Torch.Managers.PatchManager;
using VRage.Game;
using VRage.Network;
using VRageMath;

namespace BlockLimiter.Patch
{
    [PatchShim]
    public static class GridSpawnPatch
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private static readonly MethodInfo ShowPasteFailed =
            typeof(MyCubeGrid).GetMethod("SendHudNotificationAfterPaste", BindingFlags.Static | BindingFlags.Public);
        
        private static readonly MethodInfo SpawnGrid =
            typeof(MyCubeGrid).GetMethod("TryPasteGrid_Implementation", BindingFlags.Static | BindingFlags.Public);


        public static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(typeof(MyCubeBuilder).GetMethod("RequestGridSpawn", BindingFlags.NonPublic | BindingFlags.Static))
                .Prefixes.Add(typeof(GridSpawnPatch).GetMethod(nameof(OnSpawn),BindingFlags.NonPublic|BindingFlags.Static));
            
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
            if (!BlockLimiterConfig.Instance.EnableLimits  || !BlockLimiterConfig.Instance.EnableGridSpawnBlocking) return true;
            var grids = parameters.Entities;

            if (grids.Count == 0) return false;

            var remoteUserId = MyEventContext.Current.Sender.Value;

            if (remoteUserId == 0) return true;

            var playerId = Utilities.GetPlayerIdFromSteamId(remoteUserId);
            var playerName = MySession.Static.Players.TryGetIdentity(playerId)?.DisplayName;

            grids.RemoveAll(g=> Grid.IsSizeViolation(g) || Grid.CountViolation(g,playerId));
            
            if (grids.Count == 0  && BlockLimiterConfig.Instance.BlockType > BlockLimiterConfig.BlockingType.Warn)
            {
                BlockLimiter.Instance.Log.Info($"Blocked {playerName} from spawning a grid");

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
            string limitName = null;
            var removalCount = 0;
            var removedList = new List<string>();
            foreach (var grid in grids)
            {
                foreach (var limit in BlockLimiterConfig.Instance.AllLimits)
                {
                    var fCount = 0;

                    if (limit.IsExcepted(playerId)) continue;
                    var matchBlocks = new HashSet<MyObjectBuilder_CubeBlock>(grid.CubeBlocks.Where(x => limit.IsMatch(Utilities.GetDefinition(x))));
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
                        limitName = limit.Name;
                    }

                }
            }

            if (removalCount == 0) return true;
            
            
            parameters.Entities = grids;


            var msg = Utilities.GetMessage(BlockLimiterConfig.Instance.DenyMessage,removedList,limitName,removalCount);

            if (MySession.Static.Players.IsPlayerOnline(playerId))
                BlockLimiter.Instance.Torch.CurrentSession.Managers.GetManager<ChatManagerServer>()?
                    .SendMessageAsOther(BlockLimiterConfig.Instance.ServerName, msg, Color.Red, remoteUserId);


            BlockLimiter.Instance.Log.Info($"Removed {removalCount} blocks from grid spawned by {MySession.Static.Players.TryGetIdentity(playerId)?.DisplayName}");
            return true;
        }

        /// <summary>
        /// Decides if a block about to be spawned is permitted by the player
        /// </summary>
        /// <param name="definition"></param>
        /// <returns></returns>
        private static bool OnSpawn(DefinitionIdBlit definition)
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

            if (Block.IsWithinLimits(block, playerId, null,out var limitName) && !Grid.CountViolation(block,playerId))
            {
                return true;
            }


            var p = player.DisplayName;

            BlockLimiter.Instance.Log.Info($"Blocked {p} from placing {block}");

            //ModCommunication.SendMessageTo(new NotificationMessage($"You've reach your limit for {b}",5000,MyFontEnum.Red),remoteUserId );
            var msg = Utilities.GetMessage(BlockLimiterConfig.Instance.DenyMessage,new List<string>{block.ToString().Substring(16)},limitName);
            if (remoteUserId != 0 && MySession.Static.Players.IsPlayerOnline(playerId))
                BlockLimiter.Instance.Torch.CurrentSession.Managers.GetManager<ChatManagerServer>()?
                    .SendMessageAsOther(BlockLimiterConfig.Instance.ServerName, msg, Color.Red, remoteUserId);

            Utilities.SendFailSound(remoteUserId);
            Utilities.ValidationFailed();

            return false;
        }


    }
}

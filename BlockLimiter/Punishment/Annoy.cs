using System;
using System.Collections.Generic;
using System.Linq;
using BlockLimiter.ProcessHandlers;
using BlockLimiter.Settings;
using BlockLimiter.Utility;
using NLog;
using Sandbox.Game.World;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Game;

namespace BlockLimiter.Punishment
{
    public class Annoy : ProcessHandlerBase
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        public static readonly Dictionary<ulong, DateTime> AnnoyQueue = new Dictionary<ulong, DateTime>();
        public override int GetUpdateResolution()
        {
            //return 800;
            return Math.Max(BlockLimiterConfig.Instance.AnnoyInterval,1) * 1000;
        }

        public override void Handle()
        {
            //if (BlockLimiterConfig.Instance.AnnoyInterval < 1) return;
            if (!BlockLimiterConfig.Instance.Annoy || !BlockLimiterConfig.Instance.EnableLimits)return;

            RunAnnoyance();
        }

        public static void RunAnnoyance()
        {
            if (!BlockLimiterConfig.Instance.EnableLimits)return;
            var limitItems = BlockLimiterConfig.Instance.AllLimits;


            if (!limitItems.Any())
            {
                return;
            }


            var onlinePlayers = MySession.Static.Players.GetOnlinePlayers();

            if (onlinePlayers.Count < 1) return;


            foreach (var player in onlinePlayers)
            {
                var steamId = MySession.Static.Players.TryGetSteamId(player.Identity.IdentityId);
                var playerGridIds = new HashSet<long>(player.Grids);

                var playerFaction = MySession.Static.Factions.GetPlayerFaction(player.Identity.IdentityId);


                bool AnnoyPlayer()
                {
                    var annoy = false;
                    foreach (var item in limitItems)
                    {
                        if (item.IsExcepted(player)) continue;

                        foreach (var (id,count) in item.FoundEntities)
                        {

                            if (count <= item.Limit) continue;

                            if (id == player.Identity.IdentityId)
                            {
                                annoy = true;
                                break;
                            }

                            if (playerGridIds.Contains(id))
                            {
                                annoy = true;
                                break;
                            }
                        
                            if (playerFaction == null || id != playerFaction.FactionId) continue;
                            annoy = true;
                            break;
                        }
                    }

                    return annoy;
                }

                if (!AnnoyQueue.TryGetValue(steamId, out var time))
                {
                    if (AnnoyPlayer())
                    {
                        AnnoyQueue[steamId] = DateTime.Now.AddSeconds(BlockLimiterConfig.Instance.AnnoyInterval);
                    }
                    continue;
                }

                if (AnnoyPlayer()) continue;
                AnnoyQueue.Remove(steamId);

            }

            if (AnnoyQueue.Count == 0) return;

            var reset = new List<ulong>();
            lock (AnnoyQueue)
            {
                foreach (var (id, time) in AnnoyQueue)
                {
                    if (Math.Abs((time - DateTime.Now).TotalSeconds) >= 1) continue;
                    reset.Add(id);

                    try
                    {
                        ModCommunication.SendMessageTo(new NotificationMessage($"{BlockLimiterConfig.Instance.AnnoyMessage}",BlockLimiterConfig.Instance.AnnoyDuration,MyFontEnum.White),id);
                    }
                    catch (Exception exception)
                    {
                        Log.Error(exception);
                    }
                    BlockLimiter.Instance.Log.Info($"Annoy message sent to {Utilities.GetPlayerNameFromSteamId(id)}");

                }
            }

            foreach (var id in reset)
            {
                AnnoyQueue[id] = DateTime.Now.AddSeconds(BlockLimiterConfig.Instance.AnnoyInterval);
            }

        }





    }
}
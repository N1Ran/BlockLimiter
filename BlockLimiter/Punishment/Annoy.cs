using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using BlockLimiter.ProcessHandlers;
using BlockLimiter.Settings;
using BlockLimiter.Utility;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.World;
using Torch;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Game;

namespace BlockLimiter.Punishment
{
    public class Annoy : ProcessHandlerBase
    {
        private static readonly Logger Log = BlockLimiter.Instance.Log;
        public static readonly Dictionary<ulong, DateTime> AnnoyQueue = new Dictionary<ulong, DateTime>();
        public static readonly List<ulong> AnnoyList = new List<ulong>();
        public override int GetUpdateResolution()
        {
            return Math.Max(BlockLimiterConfig.Instance.AnnoyInterval,1) * 1000;
        }

        public override void Handle()
        {
            if (BlockLimiterConfig.Instance.AnnoyInterval < 1) return;
            if (!BlockLimiterConfig.Instance.Annoy || !BlockLimiterConfig.Instance.EnableLimits)return;

            RunAnnoyance();
        }

        public static void RunAnnoyance()
        {
            if (!BlockLimiterConfig.Instance.EnableLimits)return;
            var limitItems = BlockLimiterConfig.Instance.AllLimits;

            AnnoyList.Clear();

            if (!limitItems.Any())
            {
                return;
            }


            var onlinePlayers = MySession.Static.Players.GetOnlinePlayers();

            if (onlinePlayers.Count < 1) return;


            foreach (var player in onlinePlayers)
            {
                var steamId = MySession.Static.Players.TryGetSteamId(player.Identity.IdentityId);

                if (AnnoyList.Contains(steamId))
                {
                    continue;
                }

                var playerGridIds = new HashSet<long>(player.Grids);

                var playerFaction = MySession.Static.Factions.GetPlayerFaction(player.Identity.IdentityId);


                foreach (var item in limitItems)
                {
                    if (item.IsExcepted(player)) continue;

                    foreach (var (id,count) in item.FoundEntities)
                    {
                        if (AnnoyList.Contains(steamId)) break;

                        if (count <= item.Limit) continue;

                        if (id == player.Identity.IdentityId)
                        {
                            AnnoyList.Add(steamId);
                            break;
                        }

                        if (playerGridIds.Contains(id))
                        {
                            AnnoyList.Add(steamId);
                            break;
                        }
                        
                        if (playerFaction == null || id != playerFaction.FactionId) continue;
                        AnnoyList.Add(steamId);
                        break;
                    }
                }

            }

            if (AnnoyList.Count == 0) return;

            foreach (var (id, time) in AnnoyQueue)
            {
                if (Math.Abs(time.Second - DateTime.Now.Second) > BlockLimiterConfig.Instance.AnnoyInterval) continue;
                AnnoyList.Add(id);
            }

            foreach (var id in AnnoyList)
            {
                try
                {
                    ModCommunication.SendMessageTo(new NotificationMessage($"{BlockLimiterConfig.Instance.AnnoyMessage}",BlockLimiterConfig.Instance.AnnoyDuration,MyFontEnum.White),id);
                }
                catch (Exception exception)
                {
                    Log.Debug(exception);
                }
                Log.Info($"Annoy message sent to {id}");
            }

            Log.Info($"Blocklimiter annoyed {AnnoyList.Count} players");

        }





    }
}
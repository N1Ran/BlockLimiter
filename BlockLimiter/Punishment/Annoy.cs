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

            if (!limitItems.Any())
            {
                return;
            }


            var onlinePlayers = MySession.Static.Players.GetOnlinePlayers();

            if (onlinePlayers.Count < 1) return;
            var annoyList = new List<ulong>();

            foreach (var player in onlinePlayers)
            {
                var steamId = MySession.Static.Players.TryGetSteamId(player.Identity.IdentityId);
               
                if (annoyList.Contains(steamId)) continue;

                var playerGridIds = new HashSet<long>(player.Grids);

                var playerFaction = MySession.Static.Factions.GetPlayerFaction(player.Identity.IdentityId);


                foreach (var item in limitItems)
                {
                    if (item.IsExcepted(player)) continue;

                    foreach (var (id,count) in item.FoundEntities)
                    {
                        if (annoyList.Contains(steamId)) break;

                        if (count <= item.Limit) continue;

                        if (id == player.Identity.IdentityId)
                        {
                            annoyList.Add(steamId);
                            break;
                        }

                        if (playerGridIds.Contains(id))
                        {
                            annoyList.Add(steamId);
                            break;
                        }
                        
                        if (playerFaction == null || id != playerFaction.FactionId) continue;
                        annoyList.Add(steamId);
                        break;
                    }
                }

            }

            if (annoyList.Count < 1) return;

            

            foreach (var id in annoyList)
            {
                try
                {
                    ModCommunication.SendMessageTo(new NotificationMessage($"{BlockLimiterConfig.Instance.AnnoyMessage}",BlockLimiterConfig.Instance.AnnoyDuration,MyFontEnum.White),id);
                }
                catch (Exception exception)
                {
                    Log.Debug(exception);
                }
            }

            Log.Info($"Blocklimiter annoyed {annoyList.Count} players");

        }





    }
}
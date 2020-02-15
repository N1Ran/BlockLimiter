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
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        
        public override int GetUpdateResolution()
        {
            return BlockLimiterConfig.Instance.AnnoyInterval*1000;
        }

        public override void Handle()
        {
            if (!BlockLimiterConfig.Instance.Annoy || !BlockLimiterConfig.Instance.EnableLimits)return;
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

                foreach (var (id,count) in limitItems.SelectMany(x=>x.FoundEntities))
                {
                    if (id == player.Identity.IdentityId && count > 0)
                    {
                        annoyList.Add(steamId);
                        break;
                    }

                    if (player.Grids.Any(x => x == id))
                    {
                        annoyList.Add(steamId);
                        break;
                    }
                        

                    var playerFaction = MySession.Static.Factions.GetPlayerFaction(player.Identity.IdentityId);
                    if (playerFaction == null || id != playerFaction.FactionId) continue;
                    annoyList.Add(steamId);
                    break;
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

            if (BlockLimiterConfig.Instance.EnableLog)
                Log.Info($"Blocklimiter annoyed {annoyList.Count} players");

        }





    }
}
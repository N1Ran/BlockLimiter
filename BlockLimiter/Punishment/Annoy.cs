using System;
using System.Collections.Generic;
using System.Linq;
using BlockLimiter.ProcessHandlers;
using BlockLimiter.Settings;
using BlockLimiter.Utility;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
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


            var onlinePlayers = MySession.Static.Players.GetOnlinePlayers().ToList();
            var annoyList = new List<ulong>();
            var violatingEntities = limitItems.SelectMany(x => x.ViolatingEntities.Keys).ToList();
            if (!onlinePlayers.Any() || !violatingEntities.Any())return;


            foreach (var player in onlinePlayers)
            {
                var playerId = player.Id.SteamId;
                
                if (annoyList.Contains(playerId))continue;

                var playerFaction = MySession.Static.Factions.GetPlayerFaction(player.Identity.IdentityId);
                foreach (var identity in violatingEntities)
                {
                    

                    if (identity == player.Identity.IdentityId)
                    {
                        annoyList.Add(playerId);
                        break;
                    }

                    if (EntityCache.TryGetEntityById(identity, out var grid) && ((MyCubeGrid)grid).BigOwners.Contains(player.Identity.IdentityId))
                    {
                        annoyList.Add(playerId);
                        break;
                    }

                    if (playerFaction == null || identity != MySession.Static.Factions.GetPlayerFaction(player.Identity.IdentityId).FactionId) continue;
                    if (identity != MySession.Static.Factions.GetPlayerFaction(player.Identity.IdentityId).FactionId)
                        continue;
                    annoyList.Add(playerId);
                    break;


                }
            }
            if (!annoyList.Any())return;

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
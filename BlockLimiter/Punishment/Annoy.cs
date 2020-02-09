using System;
using System.Collections.Generic;
using System.Linq;
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


            var onlinePlayers = MySession.Static.Players.GetOnlinePlayers().ToList();
            var annoyList = new List<ulong>();

            foreach (var (x,y) in limitItems.SelectMany(x=>x.FoundEntities))
            {
                if (y <= 0) continue;

                if (Utilities.TryGetEntityByNameOrId(x.ToString(), out var entity))
                {
                    if (entity is MyCharacter character)
                    {
                        if (character.IsBot || character.IsDead) continue;
                        var steamId = MySession.Static.Players.TryGetSteamId(character.GetPlayerIdentityId());
                        if(!annoyList.Contains(steamId))
                            annoyList.Add(steamId);
                        continue;
                    }
                    if (entity is MyCubeGrid grid)
                    {
                        foreach (var ownerId in grid.BigOwners)
                        {
                            if (ownerId == 0) continue;
                            var steamId = MySession.Static.Players.TryGetSteamId(ownerId);
                            if(!annoyList.Contains(steamId))
                                annoyList.Add(steamId);
                        }
                        continue;
                    }
                }
                
                //player
                var playerSteamId = MySession.Static.Players.TryGetSteamId(x);
                if (playerSteamId > 0)
                {
                    if(!annoyList.Contains(playerSteamId))
                        annoyList.Add(playerSteamId);
                    continue;
                }

                //faction
                var faction = MySession.Static.Factions.TryGetFactionById(x);
                if (faction == null)continue;
                foreach (var member in faction.Members.Keys)
                {
                    var memberId = MySession.Static.Players.TryGetSteamId(member);
                    if(!annoyList.Contains(playerSteamId))
                        annoyList.Add(playerSteamId);
                }

            }
            
            if (onlinePlayers?.Any()==false || annoyList?.Any()==false)return;

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
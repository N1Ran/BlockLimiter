using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BlockLimiter.Limits;
using BlockLimiter.Settings;
using BlockLimiter.Utility;
using NLog;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch.Managers.PatchManager;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Network;
/*
namespace BlockLimiter.Handlers
{
    [PatchShim]
    public static class BlockOwnershipHandle
    {
        private static readonly HashSet<string> PatchMethods = new HashSet<string>()
        {
           "OnChangeOwnerRequest"
        };

        private static Logger Log = LogManager.GetCurrentClassLogger();

        public static void Patch(PatchContext ctx)
        {
            var t = typeof(MyCubeGrid);
            var m = typeof(BuildBlockHandler).GetMethod(nameof(Prefix));
            foreach (var met in t.GetMethods(BindingFlags.Public | BindingFlags.Public | BindingFlags.Instance))
            {

                if (PatchMethods.Contains(met.Name))
                {
                    ctx.GetPattern(met).Prefixes.Add(m);
                }
            }
        }


        public static bool Prefix(MyCubeGrid __instance, long blockId)
        {
            
            if (!BlockLimiterConfig.Instance.EnableLimits) return true;
            var grid = __instance;
            if (grid == null)
            {
                Log.Info("Null grid in BuildBlockHandler");
                return true;
            }

            if (!MyEntities.TryGetEntityById(blockId, out MyCubeBlock block))
            {
                Log.Info("Null block in BuildBlockHandler");
                return true;
            }


            var limitItems = new MtObservableCollection<LimitItem>();
            BlockLimiterConfig.Instance.LimitItems.Where(x=>x.BlockPairName.Any()).ForEach(x=>limitItems.Add(x));

            if  (BlockLimiterConfig.Instance.UseVanillaLimits && BlockLimiter.Instance.VanillaLimits.Any()) 
                BlockLimiter.Instance.VanillaLimits.Where(x=>x.BlockPairName.Any()).ForEach(x=>limitItems.Add(x));

            if (!limitItems.Any()) return true;

            var remoteUserId = MyEventContext.Current.Sender.Value;
            var playerId = Utilities.GetPlayerIdFromSteamId(remoteUserId);
            var subGrids = MyAPIGateway.GridGroups.GetGroup(grid, GridLinkTypeEnum.Physical);

            var playerFaction = MySession.Static.Factions.GetPlayerFaction(playerId);
            var found = false;
            
            foreach (var item in limitItems)
            {
                if (item.Limit == 0)
                {
                    if (item.BlockPairName.Any(x=>x.Equals(block.BlockDefinition.BlockPairName,StringComparison.OrdinalIgnoreCase)))
                    {
                        found = true;
                        continue;
                    }
                }

                if (item.LimitGrids)
                {
                    if (!Grid.IsAllowed(grid.EntityId, block.BlockDefinition))
                    {
                        found = true;
                        break;
                    }
                    if (subGrids.Any(x => !Grid.IsAllowed(x.EntityId,  block.BlockDefinition)))
                    {
                        found = true;
                        break;
                    }
                }

                if (item.LimitPlayers && !Player.IsAllowed(playerId,  block.BlockDefinition))
                {
                    found = true;
                    break;
                }

                if (playerFaction == null || !item.LimitFaction)continue;

                if(!Faction.IsAllowed(playerFaction.FactionId,  block.BlockDefinition))
                {
                    found = true;
                    break;
                }


            }
            if (!found)
                return true;
            var b = block.BlockDefinition.BlockPairName;
            Log.Info($"Blocked {Utilities.GetPlayerNameFromSteamId(remoteUserId)} from receiving a {b}");
            ModCommunication.SendMessageTo(new NotificationMessage($"You've reach your limit for {b}",5000,MyFontEnum.Red),remoteUserId );

            Utilities.SendFailSound(remoteUserId);
            Utilities.ValidationFailed();
            return false;
        }


    }
}*/
using System;
using System.Linq;
using System.Reflection;
using BlockLimiter.Limits;
using BlockLimiter.Settings;
using BlockLimiter.Utility;
using NLog;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch.Managers.PatchManager;
using Torch.Mod;
using Torch.Mod.Messages;
using Torch.Utils;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Network;
/*
namespace BlockLimiter.Handlers
{
    [PatchShim]
    public static class MergeBlockHandler
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public static void Patch(PatchContext ctx)
        {
            try
            {
                var detect =
                    typeof(MyMechanicalConnectionBlockBase).GetMethod("RaiseAttachedEntityChanged", BindingFlags.NonPublic |BindingFlags.Instance);
                ctx.GetPattern(detect).Prefixes.Add(typeof(MergeBlockHandler).GetMethod(nameof(MergeDetection)));
                Log.Info("Patched MergeBlock!");
            }
            catch (Exception e)
            {
               Log.Error(e,"Unabe to patch MyMechanicalBlockBase");
            }
        }



        public static bool MergeDetection(MyMechanicalConnectionBlockBase __instance)
        {
            if (__instance == null) return true;

            if (!BlockLimiterConfig.Instance.EnableLimits) return true;
            Log.Error("This is it");
            return true;
            var remoteUserId = MyEventContext.Current.Sender.Value;
            var grid = __instance.CubeGrid;
            if (grid == null)
            {
                Log.Info("Null grid in BuildBlockHandler");
                return true;
            }
            var playerId = Utilities.GetPlayerIdFromSteamId(remoteUserId);
            var blocks = __instance.TopGrid.GetBlocks();
            var subGrids = MyAPIGateway.GridGroups.GetGroup(grid, GridLinkTypeEnum.Physical);
            if (!blocks.Any())
            {
                return true;
            }

            var limitItems = new MtObservableCollection<LimitItem>();
            BlockLimiterConfig.Instance.LimitItems.Where(x=> x.LimitGrids && x.BlockPairName.Any()).ForEach(x=>limitItems.Add(x));

            if  (BlockLimiterConfig.Instance.UseVanillaLimits) 
                BlockLimiter.Instance.VanillaLimits.Where(x=>x.LimitGrids&&x.BlockPairName.Any()).ForEach(x=>limitItems.Add(x));


            if (!limitItems.Any()) return true;

            var playerFaction = MySession.Static.Factions.GetPlayerFaction(playerId);
            var found = false;
            var blockName = "";
            foreach (var item in limitItems)
            {
                foreach (var block in blocks.Select(x=>x.BlockDefinition))
                {
                    blockName = block.BlockPairName;

                    if (!Grid.IsAllowed(grid.EntityId, block))
                    {

                        found = true;
                        break;
                    }
                    if (subGrids.Any(x => !Grid.IsAllowed(x.EntityId, block)))
                    {

                        found = true;
                        break;
                    }

                }

            }
            if (!found)
                return true;
            Log.Info($"Blocked {Utilities.GetPlayerNameFromSteamId(remoteUserId)} from placing a {blockName}");
            ModCommunication.SendMessageTo(new NotificationMessage($"You've reach your limit for {blockName}",5000,MyFontEnum.Red),remoteUserId );
            Log.Info("Attach Fucker");
           // Utilities.AttachFail();
            Utilities.ValidationFailed();
            return false;

        }

    }
}*/
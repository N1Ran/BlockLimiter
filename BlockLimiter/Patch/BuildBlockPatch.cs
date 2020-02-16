using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using BlockLimiter.ProcessHandlers;
using Sandbox.Game.Entities;
using BlockLimiter.Utility;
using Sandbox.Game.World;
using Torch.Managers.PatchManager;
using BlockLimiter.Settings;
using Torch;
using VRage.Network;
using NLog;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Entities.Character;
using Sandbox.ModAPI;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Scripting;
using VRageRender;


namespace BlockLimiter.Patch
{
    public static class BuildBlockPatch
    {

        private static Logger Log = LogManager.GetCurrentClassLogger();

        
        public static void Patch(PatchContext ctx)
        {
            var t = typeof(MyCubeGrid);
            var bBr = t.GetMethod("BuildBlocksRequest", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            ctx.GetPattern(bBr).Prefixes.Add(typeof(BuildBlockPatch).GetMethod(nameof(BuildBlocksRequest),BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static));
            
            foreach (var met in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {

                if (met.Name.Contains("BuildBlockRequest"))
                {
                    ctx.GetPattern(met).Prefixes.Add(typeof(BuildBlockPatch).GetMethod(nameof(BuildBlockRequest),BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static));
                }
            }

        }

        public static bool BuildBlockRequest(MyCubeGrid __instance, MyCubeGrid.MyBlockLocation location)
        {
            
            if (!BlockLimiterConfig.Instance.EnableLimits) return true;
            var grid = __instance;
            if (grid == null)
            {
                Log.Debug("Null grid in BuildBlockHandler");
                return true;
            }
            var block = MyDefinitionManager.Static.GetCubeBlockDefinition(location.BlockDefinition);
            
            if (block == null)
            {
                Log.Debug("Null block in BuildBlockHandler");
                return true;
            }
            var remoteUserId = MyEventContext.Current.Sender.Value;
            var playerId = Utilities.GetPlayerIdFromSteamId(remoteUserId);

            if (Block.AllowBlock(block, playerId, grid))
            {
                return true;
            }
            
            var b = block.BlockPairName;
            if (BlockLimiterConfig.Instance.EnableLog)
                Log.Info($"Blocked {Utilities.GetPlayerNameFromSteamId(remoteUserId)} from placing a {b}");
            //ModCommunication.SendMessageTo(new NotificationMessage($"You've reach your limit for {b}",5000,MyFontEnum.Red),remoteUserId );
            MyVisualScriptLogicProvider.SendChatMessage($"Limit reached",BlockLimiterConfig.Instance.ServerName,playerId,MyFontEnum.Red);

            Utilities.SendFailSound(remoteUserId);
            Utilities.ValidationFailed();
            return false;
        }

        public static bool BuildBlocksRequest(MyCubeGrid __instance, HashSet<MyCubeGrid.MyBlockLocation> locations)
        {
            
            if (!BlockLimiterConfig.Instance.EnableLimits) return true;
            var grid = __instance;
            if (grid == null)
            {
                Log.Debug("Null grid in BuildBlockHandler");
                return true;
            }
            var block = MyDefinitionManager.Static.GetCubeBlockDefinition(locations.FirstOrDefault().BlockDefinition);
            
            if (block == null)
            {
                Log.Debug("Null block in BuildBlockHandler");
                return true;
            }
            var remoteUserId = MyEventContext.Current.Sender.Value;
            var playerId = Utilities.GetPlayerIdFromSteamId(remoteUserId);

            if (Block.AllowBlock(block, playerId, grid))
                return true;
            
            var b = block.BlockPairName;
            if (BlockLimiterConfig.Instance.EnableLog)
                Log.Info($"Blocked {Utilities.GetPlayerNameFromSteamId(remoteUserId)} from placing a {b}");
            //ModCommunication.SendMessageTo(new NotificationMessage($"You've reach your limit for {b}",5000,MyFontEnum.Red),remoteUserId );
            MyVisualScriptLogicProvider.SendChatMessage($"Limit reached",BlockLimiterConfig.Instance.ServerName,playerId,MyFontEnum.Red);

            Utilities.SendFailSound(remoteUserId);
            Utilities.ValidationFailed();
            return false;
        }

    }
}

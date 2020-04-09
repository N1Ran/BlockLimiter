using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Authentication.ExtendedProtection;
using System.Windows.Media;
using BlockLimiter.ProcessHandlers;
using BlockLimiter.Settings;
using BlockLimiter.Utility;
using NLog;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.World;
using Torch;
using Torch.Managers.PatchManager;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Game;
using VRage.Game.Components;
using VRage.Network;

namespace BlockLimiter.Patch
{
    [PatchShim]
    public static class GridSpawnPatch
    {
        public static void Patch(PatchContext ctx)
        {
            var t = typeof(MyCubeBuilder);
            var m = t.GetMethod("RequestGridSpawn", BindingFlags.NonPublic | BindingFlags.Static);
            ctx.GetPattern(m).Prefixes.Add(typeof(GridSpawnPatch).GetMethod(nameof(Prefix),BindingFlags.NonPublic|BindingFlags.Static));

        }

        /// <summary>
        /// Decides if a block about to be spawned is permitted by the player
        /// </summary>
        /// <param name="definition"></param>
        /// <returns></returns>
        private static bool Prefix(DefinitionIdBlit definition)
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

            if (Block.AllowBlock(block, playerId, (MyObjectBuilder_CubeGrid) null))
            {
                return true;
            }

            var b = block.BlockPairName;
            var p = player.DisplayName;
            if (BlockLimiterConfig.Instance.EnableLog)
                BlockLimiter.Instance.Log.Info($"Blocked {p} from placing a {b}");
            
            //ModCommunication.SendMessageTo(new NotificationMessage($"You've reach your limit for {b}",5000,MyFontEnum.Red),remoteUserId );
            MyVisualScriptLogicProvider.SendChatMessage($"{BlockLimiterConfig.Instance.DenyMessage}", BlockLimiterConfig.Instance.ServerName, playerId, MyFontEnum.Red);

            Utilities.SendFailSound(remoteUserId);
            Utilities.ValidationFailed();

            return false;
        }


    }
}

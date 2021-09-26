using System.Collections.Generic;
using System.Reflection;
using BlockLimiter.Settings;
using BlockLimiter.Utility;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Torch.API.Managers;
using Torch.Managers.ChatManager;
using Torch.Managers.PatchManager;
using VRage.Network;
using VRageMath;

namespace BlockLimiter.Patch
{
    [PatchShim]
    public static class MechanicalConnection
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public static void Patch(PatchContext ctx)
        {
          
            var t = typeof(MyMechanicalConnectionBlockBase);
            var a = typeof(MechanicalConnection).GetMethod(nameof(OnAttach), BindingFlags.NonPublic | BindingFlags.Static);
            foreach (var met in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {

                if (met.Name == "TryAttach")
                {
                    ctx.GetPattern(met).Prefixes.Add(a);
                }
            }

        }

        private static bool OnAttach(MyMechanicalConnectionBlockBase __instance, MyAttachableTopBlockBase top)
        {
            var topGrid = top.CubeGrid;
            var baseGrid = __instance.CubeGrid;
            var result = Grid.CanMerge(topGrid, baseGrid, out var blocks, out var count, out var limitName);
            if (!BlockLimiterConfig.Instance.MergerBlocking || result)
            {

                return true;
            }


            Log.Info($"Blocked attachement between {baseGrid.DisplayName} and {topGrid.DisplayName}");

            var remoteUserId = MyEventContext.Current.Sender.Value;
            if (remoteUserId <= 0) return false;
            Utilities.SendFailSound(remoteUserId);
            Utilities.ValidationFailed(remoteUserId);
            var msg = Utilities.GetMessage(BlockLimiterConfig.Instance.DenyMessage,blocks,limitName,count);
            BlockLimiter.Instance.Torch.CurrentSession.Managers.GetManager<ChatManagerServer>()?
                .SendMessageAsOther(BlockLimiterConfig.Instance.ServerName, msg, Color.Red, remoteUserId);

            return false;
        }

    }
}
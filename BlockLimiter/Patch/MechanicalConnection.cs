using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using BlockLimiter.Settings;
using BlockLimiter.Utility;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Torch.API.Managers;
using Torch.Managers.ChatManager;
using Torch.Managers.PatchManager;
using VRage.Collections;
using VRage.Network;
using VRageMath;

namespace BlockLimiter.Patch
{
    [PatchShim]
    public static class MechanicalConnection
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private static MyConcurrentDictionary<long, DateTime> _lastChecked =
            new MyConcurrentDictionary<long, DateTime>();

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
            if (!BlockLimiterConfig.Instance.MergerBlocking)
            {
                return true;
            }
            var topGrid = top.CubeGrid;
            var baseGrid = __instance.CubeGrid;

            var remoteUserId = MyEventContext.Current.Sender.Value;
            DateTime topDateTime = default;
            if (_lastChecked.TryGetValue(__instance.EntityId, out var inDateTime) || _lastChecked.TryGetValue(top.EntityId, out topDateTime))
            {
                if ( Math.Abs((DateTime.Now - inDateTime).Seconds) > 10)
                {
                    _lastChecked.Remove(__instance.EntityId);
                }
                if (Math.Abs((DateTime.Now - topDateTime).Seconds) > 10)
                {
                    _lastChecked.Remove(top.EntityId);
                }

                if (remoteUserId <= 0) return false;
                Utilities.SendFailSound(remoteUserId);
                Utilities.ValidationFailed(remoteUserId);
                return false;
            }
            var result = Grid.CanMerge(topGrid, baseGrid, out var blocks, out var count, out var limitName);
            if (result)
            {

                return true;
            }

            _lastChecked[__instance.EntityId] = DateTime.Now;
            _lastChecked[top.EntityId] = DateTime.Now;

            Log.Info($"Blocked attachement between {baseGrid.DisplayName} and {topGrid.DisplayName}");

            if (remoteUserId <= 0) return false;
            Utilities.SendFailSound(remoteUserId);
            Utilities.ValidationFailed(remoteUserId);
            var msg = Utilities.GetMessage(BlockLimiterConfig.Instance.DenyMessage,blocks,limitName,count);
            BlockLimiter.Instance.Torch.CurrentSession.Managers.GetManager<ChatManagerServer>()?
                .SendMessageAsOther(BlockLimiterConfig.Instance.ServerName, msg, Color.Red, remoteUserId);
            topGrid.RemoveBlock(top.SlimBlock);
            baseGrid.RemoveBlock(__instance.SlimBlock);
            return false;
        }

    }
}
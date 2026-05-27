using BlockLimiter.ProcessHandlers;
using BlockLimiter.Settings;
using BlockLimiter.Utility;
using NLog;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.Linq;
using Torch.API.Managers;
using Torch.Managers.ChatManager;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Game;
using VRageMath;

namespace BlockLimiter.Punishment
{
    public class Annoy : ProcessHandlerBase
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        public static readonly Dictionary<ulong, DateTime> AnnoyQueue = new Dictionary<ulong, DateTime>();
        private static readonly Dictionary<ulong, string> AnnoyMessageQueue = new Dictionary<ulong, string>();

        public override int GetUpdateResolution()
        {
            //return 800;
            return Math.Max(BlockLimiterConfig.Instance.AnnoyInterval, 1) * 1000;
        }

        public override void Handle()
        {
            //if (BlockLimiterConfig.Instance.AnnoyInterval < 1) return;
            if (!BlockLimiterConfig.Instance.Annoy || !BlockLimiterConfig.Instance.EnableLimits) return;

            RunAnnoyance();
        }

        private static string BuildAnnoyMessage(LimitItem limit, int currentCount)
        {
            var message = BlockLimiterConfig.Instance.AnnoyMessage ?? string.Empty;
            var limitName = limit?.Name ?? string.Empty;
            var limitValue = limit?.Limit.ToString() ?? string.Empty;
            var currentValue = currentCount.ToString();

            return message
                .Replace("{limit.Name}", limitName)
                .Replace("{limit.name}", limitName)
                .Replace("{Limit.Name}", limitName)
                .Replace("{LimitName}", limitName)
                .Replace("{limitName}", limitName)
                .Replace("{limit}", limitValue)
                .Replace("{Limit}", limitValue)
                .Replace("{current}", currentValue)
                .Replace("{Current}", currentValue);
        }

        public static void RunAnnoyance()
        {
            if (!BlockLimiterConfig.Instance.EnableLimits) return;
            var limitItems = BlockLimiterConfig.Instance.AllLimits;

            if (!limitItems.Any())
            {
                return;
            }

            var onlinePlayers = MySession.Static.Players.GetOnlinePlayers();

            if (onlinePlayers.Count < 1) return;

            foreach (var player in onlinePlayers)
            {
                var steamId = MySession.Static.Players.TryGetSteamId(player.Identity.IdentityId);
                var playerGridIds = new HashSet<long>(player.Grids);

                var playerFaction = MySession.Static.Factions.GetPlayerFaction(player.Identity.IdentityId);

                bool AnnoyPlayer(out string annoyMessage)
                {
                    annoyMessage = string.Empty;

                    foreach (var item in limitItems)
                    {
                        if (item.IsExcepted(player) || !item.Annoy) continue;

                        foreach (var (id, count) in item.FoundEntities)
                        {
                            if (count <= item.Limit) continue;

                            if (id == player.Identity.IdentityId)
                            {
                                annoyMessage = BuildAnnoyMessage(item, count);
                                return true;
                            }

                            if (playerGridIds.Contains(id))
                            {
                                annoyMessage = BuildAnnoyMessage(item, count);
                                return true;
                            }

                            if (playerFaction == null || id != playerFaction.FactionId) continue;
                            annoyMessage = BuildAnnoyMessage(item, count);
                            return true;
                        }
                    }

                    return false;
                }

                if (!AnnoyQueue.TryGetValue(steamId, out var time))
                {
                    if (AnnoyPlayer(out var message))
                    {
                        AnnoyQueue[steamId] = DateTime.Now.AddSeconds(BlockLimiterConfig.Instance.AnnoyInterval);
                        AnnoyMessageQueue[steamId] = message;
                    }
                    continue;
                }

                if (AnnoyPlayer(out var updatedMessage))
                {
                    AnnoyMessageQueue[steamId] = updatedMessage;
                    continue;
                }

                AnnoyQueue.Remove(steamId);
                AnnoyMessageQueue.Remove(steamId);
            }

            if (AnnoyQueue.Count == 0) return;

            var reset = new List<ulong>();
            lock (AnnoyQueue)
            {
                foreach (var (id, time) in AnnoyQueue)
                {
                    if (Math.Abs((time - DateTime.Now).TotalSeconds) >= 1) continue;
                    reset.Add(id);

                    try
                    {
                        if (!AnnoyMessageQueue.TryGetValue(id, out var message) || string.IsNullOrWhiteSpace(message))
                            message = BlockLimiterConfig.Instance.AnnoyMessage ?? string.Empty;

                        ModCommunication.SendMessageTo(new NotificationMessage(message, BlockLimiterConfig.Instance.AnnoyDuration, MyFontEnum.White), id);
                        var chatManager = BlockLimiter.Instance.Torch.CurrentSession.Managers.GetManager<ChatManagerServer>();
                        chatManager?.SendMessageAsOther(BlockLimiterConfig.Instance.ServerName, message, Color.Red, id);

                        if (BlockLimiterConfig.Instance.AnnoySound)
                            Utilities.SendFailSound(id);
                    }
                    catch (Exception exception)
                    {
                        Log.Error(exception);
                    }
                    BlockLimiter.Instance.Log.Info($"Annoy message sent to {Utilities.GetPlayerNameFromSteamId(id)}");
                }
            }

            foreach (var id in reset)
            {
                AnnoyQueue[id] = DateTime.Now.AddSeconds(BlockLimiterConfig.Instance.AnnoyInterval);
            }
        }
    }
}

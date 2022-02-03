using System;
using System.Collections.Generic;
using System.IO;
using BlockLimiter.Utility;
using Newtonsoft.Json;
using NLog;
using Sandbox.Game.World;

namespace BlockLimiter.Settings
{
    public class PlayerTimeModule
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public static List<PlayerTimeData> PlayerTimes = new List<PlayerTimeData>();

        public class PlayerTimeData 
        {
            [JsonProperty(Order = 1)]
            public string Player { get; set; }
            [JsonProperty(Order = 2)]
            public ulong SteamId { get; set; }
            [JsonProperty(Order = 3)]
            public DateTime FirstLogTime { get; set; }
        }


        private static void SaveTimeData()
        {
            File.WriteAllText(BlockLimiter.Instance.timeDataPath,JsonConvert.SerializeObject(PlayerTimes, Formatting.Indented));
        }

        public static void LogTime(Torch.API.IPlayer player)
        {
            if (player == null) return;
            ulong steamId = player.SteamId;
            PlayerTimeData data = new PlayerTimeData();
            bool found = false;
            if (PlayerTimes == null) PlayerTimes = new List<PlayerTimeData>();
            foreach (var time in PlayerTimes)
            {
                if (time.SteamId != steamId) continue;
                found = true;
                break;
            }

            if (found) return;
            Log.Info($"Logging time for player {player.Name}");
            data.SteamId = steamId;
            data.Player = player.Name;
            var lastLogout = MySession.Static.Players.TryGetIdentity(Utilities.GetPlayerIdFromSteamId(steamId))?.LastLoginTime;
            if (lastLogout != null && DateTime.Now > lastLogout)
                data.FirstLogTime = (DateTime) lastLogout;
            else
            {
                data.FirstLogTime = DateTime.Now;
            }
            PlayerTimes.Add(data);
            SaveTimeData();
        }

        public static DateTime GetTime(ulong steamId)
        {
            var time = DateTime.Now;

            foreach (var data in PlayerTimes)
            {
                if (data.SteamId != steamId) continue;
                time = data.FirstLogTime;
                break;
            }

            return time;
        }
    }
}
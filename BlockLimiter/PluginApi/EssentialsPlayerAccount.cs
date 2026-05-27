using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NLog;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.Managers;

namespace BlockLimiter.PluginApi
{
    public class EssentialsPlayerAccount
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private static readonly string EssentialGuid = "cbfdd6ab-4cda-4544-a201-f73efa3d46c0";
        private static ITorchPlugin EssentialsPlugin;

        private static Type PlayerAccountModuleType;
        private static FieldInfo PlayersAccountsField;
        private static MethodInfo GetAccountMethod;
        private static Type RanksAndPermissionsModuleType;
        private static FieldInfo RanksField;

        public static bool EssentialsInstalled => EssentialsPlugin != null;

        public static bool InitializeCommunication()
        {
            var pluginId = new Guid(EssentialGuid);
            var pluginManager = BlockLimiter.Instance.Torch.Managers.GetManager<PluginManager>();
            var result = false;

            try
            {
                if (!pluginManager.Plugins.TryGetValue(pluginId, out EssentialsPlugin) || EssentialsPlugin == null)
                {
                    Log.Warn("Communication with Essentials failed");
                }
                else
                {
                    PlayerAccountModuleType = null;
                    PlayersAccountsField = null;
                    GetAccountMethod = null;
                    RanksAndPermissionsModuleType = null;
                    RanksField = null;
                    Log.Info("BlockLimiter communication with Essentials successful");
                    result = true;
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Communication with Essentials failed");
            }

            return result;
        }

        private static bool EnsurePlayerAccountAccess()
        {
            if (!EssentialsInstalled)
                return false;

            if (PlayerAccountModuleType != null && (PlayersAccountsField != null || GetAccountMethod != null))
                return true;

            try
            {
                PlayerAccountModuleType = EssentialsPlugin.GetType().Assembly.GetType("Essentials.PlayerAccountModule");
                RanksAndPermissionsModuleType = EssentialsPlugin.GetType().Assembly.GetType("Essentials.RanksAndPermissionsModule");
                if (PlayerAccountModuleType == null)
                {
                    Log.Warn("Failed to locate Essentials.PlayerAccountModule type");
                    return false;
                }

                // Prefer the live static account list. This avoids Essentials.GetRank's old behavior of
                // returning a new PlayerAccountData with Rank = Default when the account is not found.
                PlayersAccountsField = PlayerAccountModuleType.GetField("PlayersAccounts", BindingFlags.Public | BindingFlags.Static);
                GetAccountMethod = PlayerAccountModuleType.GetMethod("GetAccount", BindingFlags.Public | BindingFlags.Instance);
                RanksField = RanksAndPermissionsModuleType?.GetField("Ranks", BindingFlags.Public | BindingFlags.Static);

                if (PlayersAccountsField == null && GetAccountMethod == null)
                {
                    Log.Warn("Failed to link Essentials player account accessors");
                    return false;
                }

                Log.Info("Successfully linked Essentials player account accessors");
                return true;
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to link Essentials player account accessors");
                return false;
            }
        }

        public static bool TryGetPrimaryRank(ulong steamId, out string rank)
        {
            rank = string.Empty;

            if (steamId == 0 || !EnsurePlayerAccountAccess())
                return false;

            try
            {
                if (TryGetRankFromStaticAccountList(steamId, out rank))
                    return true;

                if (TryGetRankFromGetAccount(steamId, out rank))
                    return true;
            }
            catch (Exception e)
            {
                Log.Warn(e, $"Failed to get player rank from Essentials for SteamID {steamId}");
            }

            rank = string.Empty;
            return false;
        }

        public static string GetPrimaryRank(ulong steamId)
        {
            return TryGetPrimaryRank(steamId, out var rank) ? rank : string.Empty;
        }


        public static bool TryCompareRankOrder(string playerRank, string filterRank, out int comparison)
        {
            comparison = 0;

            if (string.IsNullOrWhiteSpace(playerRank) || string.IsNullOrWhiteSpace(filterRank) || !EnsurePlayerAccountAccess())
                return false;

            try
            {
                var playerIndex = GetRankIndex(playerRank);
                var filterIndex = GetRankIndex(filterRank);

                if (playerIndex < 0 || filterIndex < 0)
                    return false;

                comparison = playerIndex.CompareTo(filterIndex);
                return true;
            }
            catch (Exception e)
            {
                Log.Warn(e, $"Failed to compare Essentials rank order '{playerRank}' with '{filterRank}'");
                return false;
            }
        }

        private static int GetRankIndex(string rankName)
        {
            if (RanksField == null || string.IsNullOrWhiteSpace(rankName))
                return -1;

            var ranks = RanksField.GetValue(null) as IEnumerable;
            if (ranks == null)
                return -1;

            var index = 0;
            foreach (var rank in ranks)
            {
                var currentRankName = ReadRankName(rank);
                if (string.Equals(currentRankName, rankName, StringComparison.OrdinalIgnoreCase))
                    return index;
                index++;
            }

            return -1;
        }

        private static string ReadRankName(object rank)
        {
            var rankNameProperty = rank?.GetType().GetProperty("RankName", BindingFlags.Public | BindingFlags.Instance);
            return rankNameProperty?.GetValue(rank)?.ToString() ?? string.Empty;
        }

        private static bool TryGetRankFromStaticAccountList(ulong steamId, out string rank)
        {
            rank = string.Empty;

            if (PlayersAccountsField == null)
                return false;

            var accounts = PlayersAccountsField.GetValue(null) as IEnumerable;
            if (accounts == null)
                return false;

            foreach (var account in accounts)
            {
                if (account == null)
                    continue;

                if (!TryReadSteamId(account, out var accountSteamId) || accountSteamId != steamId)
                    continue;

                rank = ReadRank(account);
                return !string.IsNullOrWhiteSpace(rank);
            }

            return false;
        }

        private static bool TryGetRankFromGetAccount(ulong steamId, out string rank)
        {
            rank = string.Empty;

            if (GetAccountMethod == null || PlayerAccountModuleType == null)
                return false;

            var instance = Activator.CreateInstance(PlayerAccountModuleType);
            var account = GetAccountMethod.Invoke(instance, new object[] { steamId });
            if (account == null)
                return false;

            rank = ReadRank(account);
            return !string.IsNullOrWhiteSpace(rank);
        }

        private static bool TryReadSteamId(object account, out ulong steamId)
        {
            steamId = 0;
            var steamIdProperty = account.GetType().GetProperty("SteamID", BindingFlags.Public | BindingFlags.Instance);
            if (steamIdProperty == null)
                return false;

            var value = steamIdProperty.GetValue(account);
            if (value is ulong ulongValue)
            {
                steamId = ulongValue;
                return true;
            }

            return ulong.TryParse(value?.ToString(), out steamId);
        }

        private static string ReadRank(object account)
        {
            var rankProperty = account.GetType().GetProperty("Rank", BindingFlags.Public | BindingFlags.Instance);
            return rankProperty?.GetValue(account)?.ToString() ?? string.Empty;
        }
    }
}

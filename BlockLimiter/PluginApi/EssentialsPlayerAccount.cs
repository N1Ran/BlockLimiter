using System;
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

        public static bool EssentialsInstalled => EssentialsPlugin != null;

        private static string EssentialGuid = "cbfdd6ab-4cda-4544-a201-f73efa3d46c0";

        private static ITorchPlugin EssentialsPlugin;

        private static MethodInfo GetRankList;


        public static bool InitializeCommunication()
        {
            var pluginId = new Guid(EssentialGuid);
            var pluginManager = BlockLimiter.Instance.Torch.Managers.GetManager<PluginManager>();
            var result = false;
            try
            {
                if (!pluginManager.Plugins.TryGetValue(pluginId, out EssentialsPlugin) || EssentialsPlugin == null)
                {
                    Log.Warn("Communication with essentials failed");
                }
                else
                {
                    Log.Info("Blocklimiter communication with essentials successful");
                    result = true;
                }
            }
            catch (Exception e)
            {
                Log.Error(e.StackTrace, "Communication with essentials failed");
            }


            return result;
        }
        




        private static bool GetRankListMethod()
        {
            if (GetRankList != null && EssentialsInstalled) return true;
            try
            {
                GetRankList = EssentialsPlugin.GetType().GetMethod("GetRankList");
                if (GetRankList != null) return true;
                Log.Warn("Failed to get rank method");

            }
            catch (Exception e)
            {
                Log.Error(e.StackTrace, "Failed to get rank method");
            }
            return false;

        }


        public static List<string> GetInheritPermList(ulong steamId)
        {
            var permList = new List<string>();

            if (steamId == 0) return permList;
            

            try
            {
                if (!GetRankListMethod())
                {
                    Log.Warn("Failed to get Rank List Method From Essentials");
                    return permList;
                }
                GetRankList.Invoke(null,new object[]{steamId,permList});

            }
            catch (Exception e)
            {
                Log.Warn(e.StackTrace, "Failed to get Inherited Permission List");
            }

            return permList;
        }
        
        
        
        
        
    }
}
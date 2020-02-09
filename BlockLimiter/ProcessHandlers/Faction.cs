/*
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Timers;
using System.Windows.Media.Effects;
using BlockLimiter.Settings;
using BlockLimiter.Utility;
using EmptyKeys.UserInterface.Generated.ContractsBlockView_Bindings;
using NLog;
using NLog.LayoutRenderers;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Game;
using VRage.Game.Entity;
using VRageRender;


namespace BlockLimiter.ProcessHandlers
{
    public class Faction : ProcessHandlerBase
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private readonly HashSet<MySlimBlock> _blockCache = new HashSet<MySlimBlock>();

        public override int GetUpdateResolution()
        {
            return 900;
        }


        public override void Handle()
        {

            if (!BlockLimiterConfig.Instance.EnableLimits && !BlockLimiterConfig.Instance.Annoy)
            {
                return;
            }
            
            var allFactions = MySession.Static.Factions.Factions;
            
            if (allFactions.Keys.Count < 1)
            {
                Log.Debug("No faction found");
                return;
            }

            var limitItems = BlockLimiterConfig.Instance.AllLimits;

            if (limitItems.Count < 1)
            {
                Log.Debug("No Faction limit found");
                return;
            }
            
            _blockCache.Clear();
            
            GridCache.GetBlocks(_blockCache);

            if (_blockCache.Count < 1)
            {
                Log.Debug("No blocks found");
                return;
            }
            
            foreach (var (factionId,faction) in allFactions)
            {
                if (faction.Members.All(x => !MySession.Static.Players.IsPlayerOnline(x.Key))) continue;

                foreach (var item in limitItems)
                {
                    if (item.BlockPairName.Count < 1 || !item.LimitFaction) continue;
                    if (item.IgnoreNpcs && faction.IsEveryoneNpc())
                    {
                        item.FoundEntities.Remove(factionId);
                        continue;
                    }

                    if (item.Exceptions.Contains(factionId.ToString()) || item.Exceptions.Contains(faction.Tag))
                    {
                        item.FoundEntities.Remove(factionId);
                        continue;
                    }

                    if (!faction.Members.Values.Any(x => MySession.Static.Players.IsPlayerOnline(x.PlayerId)))
                    {
                        item.FoundEntities.Remove(factionId);
                        continue;
                    }

                    var filteredBlocksCount = _blockCache.Count(x =>
                        Utilities.IsMatch(x.BlockDefinition, item) &&
                        x.FatBlock.GetOwnerFactionTag() == faction.Tag);

                    if (filteredBlocksCount < 1)
                    {
                        item.FoundEntities.Remove(factionId);
                        continue;
                    }
                    
                    double overCount = filteredBlocksCount - item.Limit;

                    item.FoundEntities.AddOrUpdate(factionId,overCount, (key, oldValue) => overCount);
                    
                }
                    
            }

            _blockCache.Clear();

        }
    }
}
*/
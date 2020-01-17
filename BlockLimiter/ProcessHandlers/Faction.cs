using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
            return 200;
        }


        public override void Handle()
        {

            if (!BlockLimiterConfig.Instance.EnableLimits && !BlockLimiterConfig.Instance.Annoy)
            {
                return;
            }
            
            _blockCache.Clear();
            
            EntityCache.GetBlocks(_blockCache);

            if (!_blockCache.Any())
            {
                Log.Debug("No blocks found");
                return;
            }

            var limitItems = BlockLimiterConfig.Instance.AllLimits;

            if (!limitItems.Any(x=>x.LimitFaction))
            {
                Log.Debug("No Faction limit found");
                return;
            }

            var blocks = _blockCache;
            
            if (!blocks.Any())return;

            var allFactions = MySession.Static.Factions.Factions;
            
            if (!allFactions.Any())
            {
                Log.Debug("No eligible faction found");
                return;
            }

            
            foreach (var item in limitItems)
            {
                if (!item.LimitFaction || !item.BlockPairName.Any()) continue;

                foreach (var (factionId,faction) in allFactions)
                {
                    if (item.IgnoreNpcs && faction.IsEveryoneNpc())
                    {
                        item.FoundEntities.Remove(factionId);
                        continue;
                    }

                    if (!faction.Members.Values.Any(x => MySession.Static.Players.IsPlayerOnline(x.PlayerId)))
                    {
                        item.FoundEntities.Remove(factionId);
                        continue;
                    }

                    var filteredBlocks = new List<MyCubeBlock>();

                    foreach (var block in blocks.Select(x=>x.FatBlock))
                    {
                        if (block.GetOwnerFactionTag() != faction.Tag || !Utilities.IsMatch(block.BlockDefinition,item))continue;
                        filteredBlocks.Add(block);
                    }
                   
                    var filteredBlocksCount = filteredBlocks.Count;
                    
                    var overCount = filteredBlocksCount - item.Limit;

                    if (!item.FoundEntities.ContainsKey(factionId))
                    {
                        item.FoundEntities.Add(factionId,overCount);
                    }

                    item.FoundEntities[factionId] = overCount;
                    
                }
                
            }

            _blockCache.Clear();

        }


        


    }
}
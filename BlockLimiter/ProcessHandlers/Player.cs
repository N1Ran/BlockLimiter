using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using BlockLimiter.Settings;
using BlockLimiter.Utility;
using NLog;
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
    public class Player : ProcessHandlerBase
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private readonly HashSet<MySlimBlock> _blockCache = new HashSet<MySlimBlock>();


        public override int GetUpdateResolution()
        {
            return 400;
        }

        public override void Handle()
        {

            if (!BlockLimiterConfig.Instance.EnableLimits && !BlockLimiterConfig.Instance.Annoy)
            {
                return;
            }

            _blockCache.Clear();
            EntityCache.GetBlocks(_blockCache);
            
            var blocks = new List<MySlimBlock>();
            
            blocks.AddRange(_blockCache);

            if (!blocks.Any()) return;


            var limitItems = BlockLimiterConfig.Instance.AllLimits;

            if (!limitItems.Any())
            {
                Log.Debug("No player limit found");
                return;
            }


            var players = MySession.Static.Players.GetOnlinePlayers().ToList();
            
            if (!players.Any())return;
            
            foreach (var item in limitItems)
            {

                if (!item.LimitPlayers || !item.BlockPairName.Any()) continue;

                if (!item.BlockPairName.Any())
                    continue;
                
                foreach (var player in players)
                {
                    var playerId= player.Identity.IdentityId;
                    if (playerId == 0)continue;

                    var playerBlocks = new List<MySlimBlock>();

                    foreach (var block in blocks)
                    {
                        if (!Utilities.IsMatch(block.BlockDefinition, item)) continue;
                        if (!Utilities.IsOwner(item.BlockOwnerState, block, playerId)) continue;
                        playerBlocks.Add(block);
                    }
                    
                    if (!playerBlocks.Any())
                    {
                        item.FoundEntities.Remove(playerId);
                        continue;
                    }


                    if (item.IgnoreNpcs && MySession.Static.Players.IdentityIsNpc(playerId))
                    {
                        item.FoundEntities.Remove(playerId);
                        continue;
                    }

                    var filteredBlocksCount = playerBlocks.Count;
                    
                    var overCount = filteredBlocksCount - item.Limit;
                    
                    if (!item.FoundEntities.ContainsKey(playerId))
                    {
                        item.FoundEntities.Add(playerId, overCount);
                    }

                    item.FoundEntities[playerId] = overCount;
                }

            }

            _blockCache.Clear();
            
        }
    }
}
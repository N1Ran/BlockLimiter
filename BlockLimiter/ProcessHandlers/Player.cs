/*
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
            return 700;
        }

        public override void Handle()
        {

            if (!BlockLimiterConfig.Instance.EnableLimits && !BlockLimiterConfig.Instance.Annoy)
            {
                return;
            }


            var limitItems = BlockLimiterConfig.Instance.AllLimits;

            if (limitItems.Count < 1)
            {
                Log.Debug("No player limit found");
                return;
            }
            
            var players = MySession.Static.Players.GetOnlinePlayers();
            
            if (players.Count < 1)return;
            
            _blockCache.Clear();
            GridCache.GetBlocks(_blockCache);
            
            if (_blockCache.Count < 1) return;

            foreach (var player in players)
            {
                if (player == null|| !player.IsRealPlayer || player.Character?.IsIdle == true|| player.Character?.IsDead == true) continue;
                foreach (var item in limitItems)
                {
                    if (item.BlockPairName.Count < 1 || !item.LimitPlayers) continue;
                    var playerId= player.Identity.IdentityId;
                    if (playerId == 0)continue;
                    
                    if (item.IgnoreNpcs && MySession.Static.Players.IdentityIsNpc(playerId))
                    {
                        item.FoundEntities.Remove(playerId);
                        continue;
                    }


                    var filteredBlocksCount = _blockCache.Count(x=> Utilities.IsMatch(x.BlockDefinition,item) && Utilities.IsOwner(item.BlockOwnerState, x, playerId));

                    if (filteredBlocksCount < 1)
                    {
                        item.FoundEntities.Remove(playerId);
                        continue;
                    }

                    var overCount = filteredBlocksCount - item.Limit;
                    
                    item.FoundEntities.AddOrUpdate(playerId,overCount, (key, oldValue) => overCount);
                }
            }


            _blockCache.Clear();
            
        }
    }
}
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlockLimiter.ProcessHandlers;
using BlockLimiter.Settings;
using BlockLimiter.Utility;
using NLog;
using Sandbox;
using Sandbox.Definitions;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using VRage.Collections;
using VRage.Game;

namespace BlockLimiter.Punishment
{
    public class Punish : ProcessHandlerBase
    {
        private static readonly Logger Log = BlockLimiter.Instance.Log;
        private readonly HashSet<MySlimBlock> _blockCache = new HashSet<MySlimBlock>();
        private static bool _firstCheckCompleted;

        public override int GetUpdateResolution()
        {
            return Math.Max(BlockLimiterConfig.Instance.PunishInterval,1) * 1000;
        }

        public override void Handle()
        {
            if (!BlockLimiterConfig.Instance.EnableLimits)return;
            var limitItems = BlockLimiterConfig.Instance.AllLimits;
            

            if (!limitItems.Any())
            {
                return;
            }
            

            _blockCache.Clear();
            GridCache.GetBlocks(_blockCache);


            if (!_blockCache.Any())
            {
                return;
            }
            
            var punishBlocks = new MyConcurrentDictionary<MySlimBlock,LimitItem.PunishmentType>();

            var punishCount = 0;
            var blocks = _blockCache.ToList();

            if (BlockLimiterConfig.Instance.KillNoOwnerBlocks)
            {
                var noOwnerBlocks = _blockCache.Where(x => x.BlockDefinition.ContainsComputer() && x.OwnerId == 0).ToList();
                Block.KillBlocks(noOwnerBlocks);
            }

            foreach (var item in limitItems)
            {
                if (!item.FoundEntities.Any() ||
                    item.Punishment == LimitItem.PunishmentType.None) continue;
                
                foreach (var (id,count) in item.FoundEntities)
                {
                    if (id == 0 || Utilities.IsExcepted(id, item.Exceptions))
                    {
                        continue;
                    }

                    if (count <= item.Limit) continue;


                    for (var i = blocks.Count; i --> 0;)
                    {
                        var block = blocks[i];

                        if (block?.BuiltBy == null || block.CubeGrid.IsPreview)
                        {
                            blocks.RemoveAtFast(1);
                            continue;
                        }

                        if (!Block.IsMatch(block.BlockDefinition, item)) continue;

                        var defBase = MyDefinitionManager.Static.GetDefinition(block.BlockDefinition.Id);

                        if (defBase != null && !_firstCheckCompleted && !defBase.Context.IsBaseGame) continue;

                        if (Math.Abs(punishCount - count) <= item.Limit)
                        {
                            break;
                        }

                        if (item.IgnoreNpcs)
                        {
                            if (MySession.Static.Players.IdentityIsNpc(block.FatBlock.BuiltBy) || MySession.Static.Players.IdentityIsNpc(block.FatBlock.OwnerId))

                            {
                                item.FoundEntities.Remove(id);
                                continue;
                            }
                        }

                        if (item.LimitGrids && block.CubeGrid.EntityId == id)
                        {
                            punishCount++;
                            punishBlocks[block] = item.Punishment;
                            continue;
                        }

                        if (item.LimitPlayers)
                        {
                            if (Block.IsOwner(block, id))
                            {
                                punishCount++;
                                punishBlocks[block] = item.Punishment;
                                continue;
                            }
                        }

                        if (!item.LimitFaction) continue;
                        var faction = MySession.Static.Factions.TryGetFactionById(id);
                        if (faction == null || !block.FatBlock.GetOwnerFactionTag().Equals(faction.Tag)) continue;
                        punishCount++;
                        punishBlocks[block] = item.Punishment;
                    }

                    

                }
                
            }
            
            _blockCache.Clear();

            
            if (!punishBlocks.Keys.Any())
            {
                return;
            }

            _firstCheckCompleted = !_firstCheckCompleted;

            Block.Punish(punishBlocks);
        }

    }
}
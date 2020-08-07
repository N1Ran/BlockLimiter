using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlockLimiter.Patch;
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
            BlockSwitchPatch.KeepOffBlocks.Clear();
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

                foreach (var item in limitItems.Where(item => item.FoundEntities.Any() && item.Punishment != LimitItem.PunishmentType.None))
                {

                    var idsToRemove = new HashSet<long>();

                    foreach (var (id,count) in item.FoundEntities)
                    {
                        if (id == 0 || Utilities.IsExcepted(id, item.Exceptions))
                        {
                            continue;
                        }

                        if (count <= item.Limit) continue;
                            foreach (var block in _blockCache)
                            {
                                if (block?.BuiltBy == null || block.CubeGrid.IsPreview)
                                {
                                    continue;
                                }

                                if (!item.IsMatch(block.BlockDefinition)) continue;

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
                                        idsToRemove.Add(id);
                                        continue;
                                    }
                                }

                                if (item.Punishment == LimitItem.PunishmentType.ShutOffBlock &&
                                    block.FatBlock is MyFunctionalBlock fBlock && (!fBlock.Enabled || block.FatBlock.MarkedForClose || block.FatBlock.Closed))
                                {
                                    punishCount++;
                                    continue;
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
                                if (faction == null || block.FatBlock.GetOwnerFactionTag()?.Equals(faction.Tag) == false) continue;
                                punishCount++;
                                punishBlocks[block] = item.Punishment;
                            }
                    

                    }


                    idsToRemove.ForEach(x=>item.FoundEntities.Remove(x));
                }

            
            _blockCache.Clear();

            
            if (!punishBlocks.Any())
            {
                return;
            }
            _firstCheckCompleted = !_firstCheckCompleted;
            Block.Punish(punishBlocks);
        }

    }
}
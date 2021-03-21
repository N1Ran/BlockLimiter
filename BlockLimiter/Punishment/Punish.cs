using System;
using System.Collections.Generic;
using System.Linq;
using BlockLimiter.Patch;
using BlockLimiter.ProcessHandlers;
using BlockLimiter.Settings;
using BlockLimiter.Utility;
using NLog;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using VRage.Collections;

namespace BlockLimiter.Punishment
{
    public class Punish : ProcessHandlerBase
    {
        private static readonly Logger Log = BlockLimiter.Instance.Log;
        private readonly HashSet<MySlimBlock> _blockCache = new HashSet<MySlimBlock>();
        private static bool _firstCheckCompleted;
        private static List<ulong> _approvedPunishList = new List<ulong>();

        public override int GetUpdateResolution()
        {
            return Math.Max(BlockLimiterConfig.Instance.PunishInterval,1) * 1000;
        }

        public override void Handle()
        {

            if (!BlockLimiterConfig.Instance.EnableLimits)return;
            GridCache.GetBlocks(_blockCache);
            RunPunishment(_blockCache);
            _blockCache.Clear();
        }

        public static int RunPunishment(HashSet<MySlimBlock> blocks,List<LimitItem.PunishmentType>punishmentTypes = null)
        {
            
            var totalBlocksPunished = 0;

            if (blocks.Count == 0 || !BlockLimiterConfig.Instance.EnableLimits)
            {
                return 0;
            }

            var limitItems = BlockLimiterConfig.Instance.AllLimits;

            BlockSwitchPatch.KeepOffBlocks.Clear();

            if (limitItems.Count == 0) return 0;

            var punishBlocks = new MyConcurrentDictionary<MySlimBlock,LimitItem.PunishmentType>();

            var punishCount = 0;

            if (_firstCheckCompleted)
            {
                _approvedPunishList.Clear();
            }

            foreach (var item in limitItems.Where(item => item.FoundEntities.Count > 0 && item.Punishment != LimitItem.PunishmentType.None))
            {
                if (punishmentTypes != null && !punishmentTypes.Contains(item.Punishment)) continue;
                var idsToRemove = new HashSet<long>();

                foreach (var (id,count) in item.FoundEntities)
                {
                    if (id == 0 || item.IsExcepted(id))
                    {
                        idsToRemove.Add(id);
                        continue;
                    }

                    if (count <= item.Limit) continue;

                    foreach (var block in blocks)
                    {
                        if (block.CubeGrid.IsPreview || !item.IsMatch(block.BlockDefinition))
                        {
                            continue;
                        }


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

                        var playerSteamId = MySession.Static.Players.TryGetSteamId(id);

                        if (playerSteamId > 0 && !_approvedPunishList.Contains(playerSteamId))
                        {
                            if (!Annoy.AnnoyList.Contains(playerSteamId) )
                            {
                                if (!Annoy.AnnoyQueue.Contains(playerSteamId)) Annoy.AnnoyQueue.Enqueue(playerSteamId);
                                _approvedPunishList.Add(playerSteamId);
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
                        if (faction == null || block.FatBlock.GetOwnerFactionTag()?.Equals(faction.Tag) == false) continue;
                        punishCount++;
                        punishBlocks[block] = item.Punishment;
                    }
                

                }


                idsToRemove.ForEach(x=>item.FoundEntities.Remove(x));
            }

            totalBlocksPunished = punishBlocks.Count;
            _firstCheckCompleted = !_firstCheckCompleted;
            if (totalBlocksPunished == 0)
            {
                return totalBlocksPunished;
            }

            Block.Punish(punishBlocks);

            return totalBlocksPunished;

        }

    }
}
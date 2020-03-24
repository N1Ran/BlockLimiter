using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using BlockLimiter.ProcessHandlers;
using BlockLimiter.Settings;
using BlockLimiter.Utility;
using NLog;
using Sandbox;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Library.Collections;
using VRageMath;

namespace BlockLimiter.Punishment
{
    public class Punish : ProcessHandlerBase
    {
        private static readonly Logger Log = BlockLimiter.Instance.Log;
        private readonly HashSet<MySlimBlock> _blockCache = new HashSet<MySlimBlock>();

        public override int GetUpdateResolution()
        {
            return BlockLimiterConfig.Instance.PunishInterval;
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
            
            var removeBlocks = new MyConcurrentDictionary<MySlimBlock,LimitItem.PunishmentType>();

            var punishCount = 0;

            foreach (var item in limitItems)
            {
                if (!item.FoundEntities.Any() ||
                    item.Punishment == LimitItem.PunishmentType.None) continue;
                
                foreach (var (id,count) in item.FoundEntities)
                {
                    if (id == 0 || Utilities.IsExcepted(id, item.Exceptions))
                    {
                        item.FoundEntities.Remove(id);
                        continue;
                    }

                    if (count <= item.Limit) continue;
                    
                    
                    if (item.LimitGrids && GridCache.TryGetGridById(id, out var grid))
                    {
                        if (item.IgnoreNpcs)
                        {
                            if (grid.BigOwners.Any(x=>MySession.Static.Players.IdentityIsNpc(x)))
                                continue;
                        }
                            
                        foreach (var block in grid.CubeBlocks)
                        {
                            if (Math.Abs(punishCount - count) <= item.Limit)
                            {
                                break;
                            }
                            if (!Block.IsMatch(block.BlockDefinition,item))continue;
                            punishCount++;
                            removeBlocks[block] = item.Punishment;
                        }
                        continue;

                    }

                    var player = MySession.Static.Players.TryGetIdentity(id);
                    
                    if (player != null && item.LimitPlayers)
                    {
                        if (Utilities.IsExcepted(player.IdentityId,item.Exceptions)) continue;
                        
                        foreach (var block in _blockCache.ToList())
                        {
                            if (block == null) continue;
                            if (Math.Abs(punishCount - count) <= item.Limit)
                            {
                                break;
                            }
                            if (!Block.IsOwner(item.BlockOwnerState,block,player.IdentityId)) continue;
                            if (!Block.IsMatch(block.BlockDefinition,item))continue;
                            punishCount++;
                            removeBlocks[block] = item.Punishment;
                        }
                        
                        continue;

                    }

                    if (!item.LimitFaction)
                    {
                        continue;
                    }
                    var faction = MySession.Static.Factions.TryGetFactionById(id);
                    if (faction == null) continue;
                    if (item.IgnoreNpcs && faction.IsEveryoneNpc()) continue;
                    foreach (var block in _blockCache.Where(x=>x.FatBlock.GetOwnerFactionTag()==faction.Tag))
                    {
                        if (Math.Abs(punishCount - count) <= item.Limit)
                        {
                            break;
                        }
                        if (!Block.IsMatch(block.BlockDefinition,item))continue;
                        if (removeBlocks.ContainsKey(block)) continue;
                        punishCount++;
                        removeBlocks[block] = item.Punishment;
                    }

                }
                
            }
            
            _blockCache.Clear();

            
            if (!removeBlocks.Keys.Any())
            {
                return;
            }
            
            Task.Run(() =>
            {
                
                MySandboxGame.Static.Invoke(() =>
                {
                    foreach (var (block, punishment) in removeBlocks)
                    {
                        try
                        {
                            switch (punishment)
                            {
                                case LimitItem.PunishmentType.DeleteBlock:
                                    if (BlockLimiterConfig.Instance.EnableLog)
                                        Log.Info(
                                        $"removed {block.BlockDefinition.BlockPairName} from {block.CubeGrid.DisplayName}");
                                    block.CubeGrid.RazeBlock(block.Position);
                                    continue;
                                case LimitItem.PunishmentType.ShutOffBlock:
                                    if (!(block.FatBlock is MyFunctionalBlock funcBlock)) continue;
                                    if (BlockLimiterConfig.Instance.EnableLog)
                                        Log.Info(
                                        $"Turned off {block.BlockDefinition.BlockPairName} from {block.CubeGrid.DisplayName}");
                                    funcBlock.Enabled = false;
                                    continue;
                                case LimitItem.PunishmentType.Explode:
                                    if (BlockLimiterConfig.Instance.EnableLog)
                                        Log.Info(
                                        $"Destroyed {block.BlockDefinition.BlockPairName} from {block.CubeGrid.DisplayName}");
                                    block.DoDamage(block.BlockDefinition.MaxIntegrity * 10, MyDamageType.Explosion);
                                    continue;
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }

                        }
                        catch (Exception e)
                        {
                            if(BlockLimiterConfig.Instance.EnableLog)Log.Error(e);
                        }
                    }
                }, "BlockLimiter");
            });
        }

    }
}
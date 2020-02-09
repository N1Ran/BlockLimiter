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
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
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

            
            if (_blockCache.Any())
                return;
            
            var removeBlocks = new Dictionary<MySlimBlock,LimitItem.PunishmentType>();

            foreach (var item in limitItems)
            {
                if (!item.FoundEntities.Any() ||
                    item.Punishment == LimitItem.PunishmentType.None) continue;
                
                var count = 0;
                
                foreach (var (id,overCount) in item.FoundEntities)
                {
                    if (item.Exceptions.Contains(id.ToString())) continue;

                    if (overCount<= 0) continue;
                    
                    if (overCount - count <= 0)
                    {
                        continue;
                    }

                    var player = MySession.Static.Players.TryGetIdentity(id);
                    
                    if (player != null)
                    {
                        if (item.Exceptions.Contains(player.DisplayName)) continue;
                        foreach (var block in _blockCache)
                        {
                            if (overCount - count <= 0) break;
                            if (removeBlocks.ContainsKey(block)||(block.OwnerId != player.IdentityId && block.BuiltBy != player.IdentityId)) continue;
                            if (!Utilities.IsMatch(block.BlockDefinition,item))continue;
                            count++;
                            removeBlocks.Add(block,item.Punishment);
                        }
                        
                        if(item.Punishment == LimitItem.PunishmentType.Explode || item.Punishment == LimitItem.PunishmentType.DeleteBlock)
                            item.FoundEntities.Remove(player.IdentityId);
                        continue;

                    }

                    if (GridCache.TryGetGridById(id, out var entity))
                    {
                        if (entity is MyCubeGrid grid)
                        {
                            if (item.IgnoreNpcs)
                            {
                                if (grid.BigOwners.Any(x=>MySession.Static.Players.IdentityIsNpc(x)))
                                    continue;
                            }
                            
                            foreach (var block in grid.CubeBlocks)
                            {
                                if (overCount - count <= 0) break;
                                if (!Utilities.IsMatch(block.BlockDefinition,item))continue;
                                if (removeBlocks.ContainsKey(block)) continue;
                                count++;
                                removeBlocks.Add(block,item.Punishment);
                            }
                            if(item.Punishment == LimitItem.PunishmentType.Explode || item.Punishment == LimitItem.PunishmentType.DeleteBlock)
                                item.FoundEntities.Remove(grid.EntityId);
                        }
                        continue;

                    }

                    if (!item.LimitFaction || overCount - count <= 0) continue;
                    var faction = MySession.Static.Factions.TryGetFactionById(id);
                    if (faction == null) continue;
                    if (item.IgnoreNpcs && faction.IsEveryoneNpc()) continue;
                    foreach (var block in _blockCache.Where(x=>x.FatBlock.GetOwnerFactionTag()==faction.Tag))
                    {
                        if (overCount - count <= 0) break;
                        if (!Utilities.IsMatch(block.BlockDefinition,item))continue;
                        if (removeBlocks.ContainsKey(block)) continue;
                        count++;
                        removeBlocks.Add(block,item.Punishment);
                    }
                    if(item.Punishment == LimitItem.PunishmentType.Explode || item.Punishment == LimitItem.PunishmentType.DeleteBlock)
                        item.FoundEntities.Remove(faction.FactionId);
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
                                    break;
                                case LimitItem.PunishmentType.ShutOffBlock:
                                    if (!(block.FatBlock is MyFunctionalBlock funcBlock)) continue;
                                    if (BlockLimiterConfig.Instance.EnableLog)
                                        Log.Info(
                                        $"Turned off {block.BlockDefinition.BlockPairName} from {block.CubeGrid.DisplayName}");
                                    funcBlock.Enabled = false;
                                    break;
                                case LimitItem.PunishmentType.Explode:
                                    if (BlockLimiterConfig.Instance.EnableLog)
                                        Log.Info(
                                        $"Destroyed {block.BlockDefinition.BlockPairName} from {block.CubeGrid.DisplayName}");
                                    block.DoDamage(block.BlockDefinition.MaxIntegrity * 10, MyDamageType.Explosion);
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }

                        }
                        catch (Exception e)
                        {
                            Log.Error(e);
                        }
                    }
                }, "BlockLimiter");
            });
        }

    }
}
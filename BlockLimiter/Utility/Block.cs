using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlockLimiter.Patch;
using BlockLimiter.Settings;
using NLog;
using NLog.Fluent;
using Sandbox;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch.API.Managers;
using Torch.Managers.ChatManager;
using VRage.Collections;
using VRage.Game;
using VRage.Profiler;
using VRageMath;

namespace BlockLimiter.Utility
{
    public static class Block
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public static void KillBlock(MyFunctionalBlock block)
        {
            if (Thread.CurrentThread != MySandboxGame.Static.UpdateThread)
            {
                BlockLimiter.Instance.Torch.Invoke(() => block.Enabled = false);
            }
            else
            {
                block.Enabled = false;
            }
               
            BlockLimiter.Instance.Log.Info($"Turned off {block.BlockDefinition?.Id.ToString().Substring(16)} from {block.CubeGrid?.DisplayName}");
        }

        public static bool IsWithinLimits(MyCubeBlockDefinition block, long playerId, MyObjectBuilder_CubeGrid grid, out string limitName)
        {
            limitName = string.Empty;
            var allow = true;
            if (block == null) return true;
            var faction = MySession.Static.Factions.GetPlayerFaction(playerId);

            if (grid != null && Grid.IsSizeViolation(grid)) return false;

            foreach (var item in BlockLimiterConfig.Instance.AllLimits)
            {
                limitName = item.Name;
                if (item.BlockList.Count == 0 || !item.IsMatch(block)) continue;
                
                if (item.IsExcepted(playerId) || (grid != null && item.IsExcepted(grid.EntityId)))
                    continue;



                if (item.Limit == 0 && (item.LimitGrids || item.LimitPlayers || item.LimitFaction))
                {

                    return false;
                }

                if (grid != null && item.IsGridType(grid,playerId))
                {
                    var gridId = grid.EntityId;

                    if (gridId > 0 && item.LimitGrids && item.FoundEntities.TryGetValue(gridId, out var gCount))
                    {

                        
                        if (gCount >= item.Limit)
                        {
                            allow = false;
                            break;
                        }

                    

                    }
                }


                if (playerId > 0 && item.LimitPlayers && item.FoundEntities.TryGetValue(playerId, out var pCount))
                {

                    if (pCount >= item.Limit)
                    {
                        allow = false;
                        break;
                    }
                }



                if (faction == null || !item.LimitFaction || !item.FoundEntities.TryGetValue(faction.FactionId, out var fCount)) continue;
                {

                    if (fCount < item.Limit) continue;
                    allow = false;
                    break;
                }
                
                
            }

            return allow;
            
        }

        public static bool IsWithinLimits(MyCubeBlockDefinition def, long ownerId, long gridId, int count, out string limit)
        {
            limit = string.Empty;
            if (def == null || Math.Abs(ownerId + gridId) < 1) return true;
            
            var ownerFaction = MySession.Static.Factions.GetPlayerFaction(ownerId);


            var allow = true;

            if (Grid.IsSizeViolation(gridId)) return false;

            if (BlockLimiterConfig.Instance.AllLimits.Count == 0) return true;
            var foundGrid = GridCache.TryGetGridById(gridId, out var grid);
            if (!foundGrid)
            {
                GridCache.AddGrid(grid);
            }
            var subGrids = Grid.GetSubGrids(grid);
            foreach (var item in BlockLimiterConfig.Instance.AllLimits)
            {
                limit = item.Name;
                if (!item.IsMatch(def)) continue;
                
                if ((ownerId > 0 && item.IsExcepted(ownerId)) ||
                    gridId > 0 && item.IsExcepted(gridId))
                    continue;


                if (foundGrid && !item.IsGridType(grid)) continue;

                if (item.Limit == 0 && (item.LimitGrids || item.LimitPlayers || item.LimitFaction))
                {
                    return false;
                }


                if (item.LimitGrids && gridId > 0)
                {
                    item.FoundEntities.TryGetValue(gridId, out var gCount);
                    
                    if (foundGrid && item.IsGridType(grid))
                    {
                        if (gCount + count > item.Limit)
                        {
                            allow = false;
                            break;
                        }
                        //Counts found subgrid blocks too. 
                        var subGBlockCount = 0;
                        foreach (var subGrid in subGrids)
                        {
                            if (!item.FoundEntities.TryGetValue(subGrid.EntityId, out var subGCount))
                            {
                                continue;
                            }
                            subGBlockCount += subGCount;
                        }
                        

                        if (subGBlockCount + count + gCount > item.Limit)
                        {
                            allow = false;
                            break;
                        }
                    }
                }


                if (ownerId > 0 && item.LimitPlayers && item.FoundEntities.TryGetValue(ownerId, out var pCount))
                {

                    if (pCount + count > item.Limit)
                    {
                        allow = false;
                        break;
                    }
                }



                if (ownerFaction == null || !item.LimitFaction || !item.FoundEntities.TryGetValue(ownerFaction.FactionId, out var fCount)) continue;
                {

                    if (fCount + count <= item.Limit) continue;
                    allow = false;
                    break;
                }
                
                
            }


            return allow;

        }

        public static bool IsOwner(MySlimBlock block, long playerId)
        {
            return block.BuiltBy == playerId || block.OwnerId == playerId;
        }

        public static void IncreaseCount(MyCubeBlockDefinition def, List<long> playerIds, int amount = 1, long gridId = 0)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits) return;
            
            var factions = new List<MyFaction>();
            foreach (var playerId in playerIds)
            {
                var faction = MySession.Static.Factions.GetPlayerFaction(playerId);
                if (faction == null) continue;
                factions.Add(faction);
            }
            
            foreach (var limit in BlockLimiterConfig.Instance.AllLimits)
            {
                if (!limit.IsMatch(def)) continue;

                var foundGrid = GridCache.TryGetGridById(gridId, out var grid);

                if (foundGrid && !limit.IsGridType(grid))
                {
                    limit.FoundEntities.Remove(gridId);
                    continue;
                }

                if (limit.LimitGrids && gridId > 0)
                {
                    limit.FoundEntities.AddOrUpdate(gridId, amount, (l, i) => i+amount);
                }

                if (limit.LimitPlayers && playerIds.Count > 0)
                {
                    foreach (var playerId in playerIds)
                    {
                        if (playerId == 0) continue;
                        if (limit.IgnoreNpcs)
                        {
                            if (MySession.Static.Players.IdentityIsNpc(playerId)) continue;
                            if (foundGrid && MySession.Static.Players.IdentityIsNpc(GridCache.GetBuilders(grid).FirstOrDefault())) continue;
                    
                        }

                        limit.FoundEntities.AddOrUpdate(playerId, amount, (l, i) => i+amount);

                    }
                }

                if (!limit.LimitFaction || factions.Count <= 0) continue;
                foreach (var faction in factions)
                {
                    limit.FoundEntities.AddOrUpdate(faction.FactionId, amount, (l, i) => i+amount);
                }

            }

        }

        public static void DecreaseCount(MyCubeBlockDefinition def, List<long> playerIds, int amount = 1, long gridId = 0)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits) return;
            var factions = new List<MyFaction>();

            foreach (var playerId in playerIds)
            {
                var faction = MySession.Static.Factions.GetPlayerFaction(playerId);
                if (faction == null) continue;
                factions.Add(faction);
            }

            foreach (var limit in BlockLimiterConfig.Instance.AllLimits)
            {
                if (!limit.IsMatch(def))continue;

                var foundGrid = GridCache.TryGetGridById(gridId, out var grid);

                if (foundGrid && !limit.IsGridType(grid))
                {
                    limit.FoundEntities.Remove(gridId);
                    continue;
                }

                if (limit.LimitGrids && gridId > 0)
                    limit.FoundEntities.AddOrUpdate(gridId, 0, (l, i) => Math.Max(0,i - amount));

                foreach (var playerId in playerIds)
                {
                    if (playerId == 0) continue;
                    if (limit.IgnoreNpcs)
                    {
                        if (MySession.Static.Players.IdentityIsNpc(playerId))
                        {
                            limit.FoundEntities.Remove(playerId);
                            continue;
                        }
                        if (foundGrid && MySession.Static.Players.IdentityIsNpc(GridCache.GetBuilders(grid).FirstOrDefault())) continue;
                    
                    }

                    if (limit.LimitPlayers)
                        limit.FoundEntities.AddOrUpdate(playerId, 0, (l, i) => Math.Max(0,i - amount));
                }

                if (limit.LimitFaction && factions.Count > 0)
                    foreach (var faction in factions)
                    {
                        limit.FoundEntities.AddOrUpdate(faction.FactionId, 0, (l, i) => Math.Max(0,i - amount));
                    }
                limit.ClearEmptyEntities();
            }

        }

        public static bool CanAdd(List<MyObjectBuilder_CubeBlock> blocks, long id, out List<MyObjectBuilder_CubeBlock> nonAllowedBlocks)
        {
            var newList = new List<MyObjectBuilder_CubeBlock>();
            if (!BlockLimiterConfig.Instance.EnableLimits)
            {
                nonAllowedBlocks = newList;
                return true;
            }
            
            foreach (var limit in BlockLimiterConfig.Instance.AllLimits)
            {
                if (limit.IgnoreNpcs)
                {
                    if (MySession.Static.Players.IdentityIsNpc(id)) continue;
                }

                limit.FoundEntities.TryGetValue(id, out var currentCount);
                
                if(limit.IsExcepted(id)) continue;
                
                var affectedBlocks = blocks.Where(x => limit.IsMatch(Utilities.GetDefinition(x))).ToList();
                
                if (affectedBlocks.Count <= limit.Limit - currentCount ) continue;
                
                var take = affectedBlocks.Count - (limit.Limit - currentCount);
                
                newList.AddRange(affectedBlocks.Where(x=>!newList.Contains(x)).Take(take));
            }

            nonAllowedBlocks = newList;
            return newList.Count == 0;
        }
       
        public static bool CanAdd(List<MySlimBlock> blocks, long id, out List<MySlimBlock> nonAllowedBlocks)
        {
            nonAllowedBlocks = new List<MySlimBlock>();

            if (!BlockLimiterConfig.Instance.EnableLimits)
            {
                return true;
            }

            foreach (var limit in BlockLimiterConfig.Instance.AllLimits)
            {
                if (limit.IgnoreNpcs)
                {
                    if (MySession.Static.Players.IdentityIsNpc(id)) continue;
                }

                if(limit.IsExcepted(id)) continue;
                if (!limit.FoundEntities.TryGetValue(id, out var currentCount)) continue;
                var affectedBlocks = blocks.Where(x => limit.IsMatch(x.BlockDefinition)).ToList();
                if (affectedBlocks.Count <= limit.Limit - currentCount ) continue;
                var take = affectedBlocks.Count - (limit.Limit - currentCount);
                var list = nonAllowedBlocks;
                nonAllowedBlocks.AddRange(affectedBlocks.Where(x=>!list.Contains(x)).Take(take));
            }

            return nonAllowedBlocks.Count == 0;
        }

        /*
        public static void Punish(MyConcurrentDictionary<MySlimBlock, LimitItem.PunishmentType> removalCollection)
        {

            if (removalCollection.Count == 0 || !BlockLimiterConfig.Instance.EnableLimits) return;
            var chatManager = BlockLimiter.Instance.Torch.CurrentSession.Managers.GetManager<ChatManagerServer>();
            Log.Warn($"Starting punishment for {removalCollection.Count}");
            lock (removalCollection)
            {
                //Task.WaitAll(removalCollection.Select(d => PunishBlock(d.Key, d.Value)).ToArray());
                var tasks = new List<Task>();
                foreach (var (block,punishmentType) in removalCollection)
                {
                    tasks.Add(Task.Run(()=>PunishBlock(block,punishmentType)));
                }

                var finishedTask = Task.WhenAny(tasks);
                tasks.Remove(finishedTask);
                /*

                Task.Run(() =>
                {
                    Parallel.ForEach(removalCollection, new ParallelOptions{MaxDegreeOfParallelism = 5},collective =>
                    {
                        var (k, v) = collective;
                        PunishBlock(k, v);
                    });
                });
                Log.Warn("Punishment Complete");

            }

        }



        private static Task<int>  PunishBlock(MySlimBlock block, LimitItem.PunishmentType punishment)
        {
            var chatManager = BlockLimiter.Instance.Torch.CurrentSession.Managers.GetManager<ChatManagerServer>();
            var ownerSteamId = MySession.Static.Players.TryGetSteamId(block.OwnerId);
            if (block.IsDestroyed || block.FatBlock.Closed || block.FatBlock.MarkedForClose) return Task.FromResult(0);
            Color color = Color.Yellow;

            switch (punishment)
            {
                case LimitItem.PunishmentType.None:
                    return Task.FromResult(0);
                case LimitItem.PunishmentType.DeleteBlock:
                    BlockLimiter.Instance.Torch.InvokeAsync(() => { block.CubeGrid?.RemoveBlock(block); });

                    BlockLimiter.Instance.Log.Info(
                        $"Removed {block.BlockDefinition} from {block.CubeGrid.DisplayName}");
                    break;
                case LimitItem.PunishmentType.ShutOffBlock:
                    if (!(block.FatBlock is MyFunctionalBlock fBlock)) return Task.FromResult(0);
                    KillBlock(fBlock);
                    break;
                case LimitItem.PunishmentType.Explode:

                    BlockLimiter.Instance.Log.Info(
                        $"Destroyed {block.BlockDefinition} from {block.CubeGrid.DisplayName}");
                    BlockLimiter.Instance.Torch.InvokeAsync(() =>
                    {
                        block.DoDamage(block.BlockDefinition.MaxIntegrity, MyDamageType.Fire);
                    });
                    break;
                default:
                    return Task.FromResult(0);
            }

            if (ownerSteamId == 0 || !MySession.Static.Players.IsPlayerOnline(block.OwnerId)) return Task.FromResult(1);

            chatManager?.SendMessageAsOther(BlockLimiterConfig.Instance.ServerName, 
                           $"Punishing {((MyTerminalBlock)block.FatBlock).CustomName} from {block.CubeGrid.DisplayName} with {punishment}",color,ownerSteamId);        
                    return Task.FromResult(1);
        }
        
        */

        public static int FixIds()
        {
            var result = 0;
            if (!BlockLimiterConfig.Instance.EnableLimits)
                return result;
            var blockCache = new HashSet<MySlimBlock>();

            GridCache.GetBlocks(blockCache);

            var test = Task.WhenAll(blockCache.Select(FixBlockOwnership).ToArray());
            result = test.Result.Sum();

            test.Dispose();
            return result;
        }
        
        private static Task<int> FixBlockOwnership(MySlimBlock block)
        {

            if (block == null || block.OwnerId == block.BuiltBy || 
                block.FatBlock?.MarkedForClose == true || !block.BlockDefinition.ContainsComputer()) 
                return Task.FromResult(0);
            if (block.OwnerId == 0 && block.BuiltBy > 0 )
            {
                block.FatBlock?.CubeGrid?.ChangeOwner(block.FatBlock,block.OwnerId,block.BuiltBy);
            }
            else
            {
                block.TransferAuthorship(block.OwnerId);
            }

            return Task.FromResult(1);
        }



    }
}
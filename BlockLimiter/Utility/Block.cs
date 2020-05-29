using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using BlockLimiter.Patch;
using BlockLimiter.Settings;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;

namespace BlockLimiter.Utility
{
    public static class Block
    {
        private static readonly HashSet<LimitItem> Limits = BlockLimiterConfig.Instance.AllLimits;

        
        public static bool IsWithinLimits(MyCubeBlockDefinition block, long playerId, MyObjectBuilder_CubeGrid grid = null)
        {
            
            var allow = true;
            if (block == null) return true;
            var faction = MySession.Static.Factions.GetPlayerFaction(playerId);

            if (playerId > 0 && Utilities.IsExcepted(playerId, new List<string>()) || (faction != null && Utilities.IsExcepted(faction.FactionId,new List<string>()))) return true;

            if (grid != null && Grid.IsSizeViolation(grid)) return false;

            foreach (var item in BlockLimiterConfig.Instance.AllLimits)
            {
                if (!item.BlockList.Any() || !IsMatch(block, item)) continue;
                
                if (grid != null && faction != null && (Utilities.IsExcepted(playerId,item.Exceptions) || Utilities.IsExcepted(faction.FactionId,item.Exceptions) || Utilities.IsExcepted(grid.EntityId,item.Exceptions)))
                    continue;

                if (item.Limit == 0 && (item.LimitGrids || item.LimitPlayers || item.LimitFaction))
                {

                    return false;
                }


                if (grid != null && Grid.IsGridType(grid,item))
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

        public static bool IsWithinLimits(MyCubeBlockDefinition def, long ownerId, long gridId, int count = 1)
        {
            if (def == null || Math.Abs(ownerId + gridId) < 1) return true;


            var ownerFaction = MySession.Static.Factions.GetPlayerFaction(ownerId);


            if (ownerId > 0 && Utilities.IsExcepted(ownerId, new List<string>()) || (ownerFaction != null && Utilities.IsExcepted(ownerFaction.FactionId,new List<string>())) ||
                gridId > 0 && Utilities.IsExcepted(gridId, new List<string>())) return true;

            var allow = true;

            if (Grid.IsSizeViolation(gridId)) return false;


            foreach (var item in BlockLimiterConfig.Instance.AllLimits)
            {
                if (!item.BlockList.Any() || !IsMatch(def, item)) continue;
                
                if (ownerId > 0 && (Utilities.IsExcepted(ownerId,item.Exceptions) || ownerFaction != null && Utilities.IsExcepted(ownerFaction.FactionId,item.Exceptions) || gridId > 0 && Utilities.IsExcepted(gridId,item.Exceptions)))
                    continue;
                if (item.Limit == 0 && (item.LimitGrids || item.LimitPlayers || item.LimitFaction))
                {
                    return false;
                }


                if (item.LimitGrids && gridId > 0 && item.FoundEntities.TryGetValue(gridId, out var gCount))
                {
                    if (GridCache.TryGetGridById(gridId, out var grid) && Grid.IsGridType(grid,item))
                    {
                        if (gCount + count > item.Limit)
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

        public static bool IsMatch(MyCubeBlockDefinition block, LimitItem item)
        {
            if (!item.BlockList.Any() || block == null) return false;
            return item.BlockList.Any(x=>x.Equals(block.ToString().Substring(16),StringComparison.OrdinalIgnoreCase)) || item.BlockList.Any(x => x.Equals(block.Id.SubtypeId.ToString(), StringComparison.OrdinalIgnoreCase))
                   || item.BlockList.Any(x => x.Equals(block.Id.TypeId.ToString().Substring(16), StringComparison.OrdinalIgnoreCase))
                   || item.BlockList.Any(x=>x.Equals(block.BlockPairName,StringComparison.OrdinalIgnoreCase));
        }


        public static void IncreaseCount(MyCubeBlockDefinition def, long playerId, int amount = 1, long gridId = 0)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits) return;

            var faction = MySession.Static.Factions.GetPlayerFaction(playerId);
            
            foreach (var limit in Limits)
            {
                if (!IsMatch(def,limit))continue;

                if (limit.IgnoreNpcs)
                {
                    if (MySession.Static.Players.IdentityIsNpc(playerId)) continue;
                    if (GridCache.TryGetGridById(gridId, out var grid) &&
                        MySession.Static.Players.IdentityIsNpc(grid.BigOwners.FirstOrDefault())) continue;
                    
                }

                if (limit.LimitPlayers && playerId > 0)
                    limit.FoundEntities.AddOrUpdate(playerId, amount, (l, i) => i+amount);
                if (limit.LimitGrids && gridId > 0)
                    limit.FoundEntities.AddOrUpdate(gridId, amount, (l, i) => i+amount);

                if (limit.LimitFaction && faction != null)
                    limit.FoundEntities.AddOrUpdate(faction.FactionId, amount, (l, i) => i+amount);


            }

        }

        public static void DecreaseCount(MyCubeBlockDefinition def, long playerId, int amount = 1, long gridId = 0)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits) return;
            var faction = MySession.Static.Factions.GetPlayerFaction(playerId);

            foreach (var limit in Limits)
            {
                if (!IsMatch(def,limit))continue;

                if (limit.IgnoreNpcs)
                {
                    if (MySession.Static.Players.IdentityIsNpc(playerId)) continue;
                    if (GridCache.TryGetGridById(gridId, out var grid) &&
                        MySession.Static.Players.IdentityIsNpc(grid.BigOwners.FirstOrDefault())) continue;
                    
                }

                if (limit.LimitPlayers && playerId > 0)
                    limit.FoundEntities.AddOrUpdate(playerId, 0, (l, i) => Math.Max(0,i - amount));
                if (limit.LimitGrids && gridId > 0)
                    limit.FoundEntities.AddOrUpdate(gridId, 0, (l, i) => Math.Max(0,i - amount));
                if (limit.LimitFaction && faction != null)
                    limit.FoundEntities.AddOrUpdate(faction.FactionId, 0, (l, i) => Math.Max(0,i - amount));

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
                if(Utilities.IsExcepted(id, limit.Exceptions)) continue;
                var affectedBlocks = blocks.Where(x => IsMatch(Utilities.GetDefinition(x), limit)).ToList();
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

                if(Utilities.IsExcepted(id, limit.Exceptions)) continue;
                if (!limit.FoundEntities.TryGetValue(id, out var currentCount)) continue;
                var affectedBlocks = blocks.Where(x => IsMatch(x.BlockDefinition, limit)).ToList();
                if (affectedBlocks.Count <= limit.Limit - currentCount ) continue;
                var take = affectedBlocks.Count - (limit.Limit - currentCount);
                var list = nonAllowedBlocks;
                nonAllowedBlocks.AddRange(affectedBlocks.Where(x=>!list.Contains(x)).Take(take));
            }

            return nonAllowedBlocks.Count == 0;
        }




    }
}
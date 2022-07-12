using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using BlockLimiter.Settings;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;

namespace BlockLimiter.Utility
{
    public static class UpdateLimits
    {

        private static readonly Logger Log = BlockLimiter.Instance.Log;
        private static readonly Queue<long> _queue = new Queue<long>();


        public static void Enqueue(long id)
        {
            lock (_queue)
            {
                if (_queue.Contains(id)) return;
                _queue.Enqueue(id);
            }
        }

        public static void Dequeue()
        {
            lock (_queue)
            {
                if (_queue.Count == 0) return;
                if (GridCache.GetBlockCount() == 0) return;
                var id = _queue.Dequeue();
                if (GridCache.TryGetGridById(id, out var grid))
                {
                    GridLimit(grid);
                    return;
                }

                var faction = MySession.Static.Factions.TryGetFactionById(id);
                if (faction != null)
                {
                    FactionLimit(id);
                    return;
                }

                if (!MySession.Static.Players.HasIdentity(id)) return;
                PlayerLimit(id);
            }

        }

        public static bool PlayerLimit(long id)
        {
            if (id == 0) return false;
            var playerBlocks = new HashSet<MySlimBlock>();

            GridCache.GetPlayerBlocks(playerBlocks,id);

            var limits = BlockLimiterConfig.Instance.AllLimits;
            if (limits?.Count == 0) return false;
            if (playerBlocks.Count == 0)
            {
                foreach (var limit in BlockLimiterConfig.Instance.AllLimits)
                {
                    limit.FoundEntities.Remove(id);
                }
                return false;
            }

            Parallel.ForEach(BlockLimiterConfig.Instance.AllLimits, new ParallelOptions {MaxDegreeOfParallelism = 5},limit =>
            {
                if (!limit.LimitPlayers)
                {
                    limit.FoundEntities.Remove(id);
                    return;
                }
                var limitedBlocks = playerBlocks.Count(x =>
                    limit.IsMatch(x.BlockDefinition));
                if (limitedBlocks == 0)
                {
                    limit.FoundEntities.Remove(id);
                    return;
                }

                limit.FoundEntities[id] = limitedBlocks;


            });
            return true;
        }
        
        
        public static void GridLimit(MyCubeGrid grid)
        {
            if (grid == null) return;
            
            var blocks = new HashSet<MySlimBlock>();
            blocks.UnionWith(grid.CubeBlocks);

            if (blocks.Count == 0)
            {
                return;
            }

            var limits = new List<LimitItem>(BlockLimiterConfig.Instance.AllLimits.Where(x=>x.LimitGrids));
            if (limits.Count == 0) return;
            Parallel.ForEach(limits,new ParallelOptions{MaxDegreeOfParallelism = 5}, limit =>
            {
                if (!limit.IsGridType(grid))
                {
                    limit.FoundEntities.Remove(grid.EntityId);
                    return;
                }
                
                var limitedBlocks = blocks.Count(x => limit.IsMatch(x.BlockDefinition));

                if (limitedBlocks == 0)
                {
                    limit.FoundEntities.Remove(grid.EntityId);
                    return;
                }
                limit.FoundEntities[grid.EntityId] = limitedBlocks;

            });


        }




        public static bool FactionLimit(long id)
        {
            if (id == 0) return false;
            var factionBlocks = new HashSet<MySlimBlock>();
            var limits = BlockLimiterConfig.Instance.AllLimits;

            GridCache.GetFactionBlocks(factionBlocks,id);

            if (factionBlocks.Count == 0)
            {
                foreach (var limit in limits)
                {
                    limit.FoundEntities.Remove(id);  
                }
                return false;
            }


            var faction = MySession.Static.Factions.TryGetFactionById(id);
            if (faction == null) return false;

            Parallel.ForEach(limits, new ParallelOptions{MaxDegreeOfParallelism = 5},limit =>
            {
                if (!limit.LimitFaction)
                {
                    limit.FoundEntities.Remove(id);
                    return;
                }

                var factionBlockCount = factionBlocks.Count(x => limit.IsMatch(x.BlockDefinition));

                if (factionBlockCount == 0)
                {
                    limit.FoundEntities.Remove(id);
                    return;
                }

                limit.FoundEntities[id] = factionBlockCount;

            });
            return true;
        }
        
               
        public static void ResetLimits(bool updateGrids = true, bool updatePlayers = true, bool updateFactions = true)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits)
            {
                foreach (var limit in BlockLimiterConfig.Instance.AllLimits)
                {
                    limit.FoundEntities.Clear();
                }
                return;
            }

            if (updateGrids)  Task.WhenAll(UpdateGrids());
            if (updateFactions) Task.WhenAll(UpdateFactions());
            if (updatePlayers) Task.WhenAll(UpdatePlayers());
            
            Log.Warn("Count Complete");

        }

        private static async Task UpdateGrids()
        {
            var grids = new HashSet<MyCubeGrid>();
            
            GridCache.GetGrids(grids);
            
            var tasks = grids.Select(t => Task.Run(() => Enqueue(t.EntityId))).ToList();

            await Task.WhenAll(tasks);

        }

        private static async Task UpdateFactions()
        {
            var factions = MySession.Static.Factions;
            
            var tasks = factions.Select(t => Task.Run(() => FilterFaction(t.Value))).ToList();

            void FilterFaction(MyFaction faction)
            {
                if (faction.IsEveryoneNpc()) return;
                Enqueue(faction.FactionId);
            }

            await Task.WhenAll(tasks);
        }

        private static async Task UpdatePlayers()
        {
            var players = MySession.Static.Players.GetAllPlayers();

            var tasks = players.Select(t => Task.Run(() => RunPlayer(t))).ToList();

            void RunPlayer(MyPlayer.PlayerId player)
            {
                if (player.SteamId == 0) return;

                var identity = Utilities.GetPlayerIdentityFromSteamId(player.SteamId);

                if (string.IsNullOrEmpty(identity.DisplayName))
                    return;

                if (identity.IdentityId == 0) return;

                Enqueue(identity.IdentityId);
            }

            await Task.WhenAll(tasks);
            
        }
        
    }
}
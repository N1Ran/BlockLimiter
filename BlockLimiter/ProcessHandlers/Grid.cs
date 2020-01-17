using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Timers;
using BlockLimiter.Settings;
using BlockLimiter.Utility;
using NLog;
using ParallelTasks;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.Entity.EntityComponents.Interfaces;
using VRage.ModAPI;

namespace BlockLimiter.ProcessHandlers
{
    public class Grid : ProcessHandlerBase
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private readonly HashSet<MyEntity> _entityCache = new HashSet<MyEntity>();

        public override int GetUpdateResolution()
        {
            return 200;
        }

        /// <summary>
        /// Checks grids and makes a list of grids to disable
        /// </summary>
        public override void Handle()
        {

            if (!BlockLimiterConfig.Instance.EnableLimits && !BlockLimiterConfig.Instance.Annoy)
            {
                return;
            }
            
            _entityCache.Clear();
            EntityCache.GetEntities(_entityCache);
            
            var grids = _entityCache.OfType<MyCubeGrid>().ToList();

            if (grids?.Any() == false)
            {
                Log.Debug("No grid found");
                return;
            }

            var limitItems = BlockLimiterConfig.Instance.AllLimits;

            if (limitItems == null || limitItems?.Any(x=>x.LimitGrids)==false)
            {
                Log.Debug("No grid limit found");
                return;
            }
            
            foreach (var grid in grids)
            {
                if (grid == null || grid.EntityId == 0)
                {
                    continue;
                }

                BlockLimiterConfig.Instance.DisabledEntities.Remove(grid.EntityId);

                var gridSize = grid.BlocksCount;
                var gridType = grid.GridSizeEnum;
                var isStatic = grid.IsStatic;

                if (BlockLimiterConfig.Instance.MaxBlockSizeShips > 0 && !isStatic && gridSize >= BlockLimiterConfig.Instance.MaxBlockSizeShips)
                {
                    BlockLimiterConfig.Instance.DisabledEntities.Add(grid.EntityId);
                }

                if (BlockLimiterConfig.Instance.MaxBlockSizeStations > 0 && isStatic && gridSize >= BlockLimiterConfig.Instance.MaxBlockSizeStations)
                {
                    if (!BlockLimiterConfig.Instance.DisabledEntities.Contains(grid.EntityId))
                    {
                        BlockLimiterConfig.Instance.DisabledEntities.Add(grid.EntityId);
                    }
                }

                if (BlockLimiterConfig.Instance.MaxBlocksLargeGrid > 0 && gridType == MyCubeSize.Large && gridSize >= BlockLimiterConfig.Instance.MaxBlocksLargeGrid)
                {
                    if (!BlockLimiterConfig.Instance.DisabledEntities.Contains(grid.EntityId))
                    {
                        BlockLimiterConfig.Instance.DisabledEntities.Add(grid.EntityId);
                    }
                }

                if (BlockLimiterConfig.Instance.MaxBlocksSmallGrid > 0 && gridType == MyCubeSize.Small && gridSize >= BlockLimiterConfig.Instance.MaxBlocksSmallGrid)
                {
                    if (!BlockLimiterConfig.Instance.DisabledEntities.Contains(grid.EntityId))
                    {
                        BlockLimiterConfig.Instance.DisabledEntities.Add(grid.EntityId);
                    }
                }
                    
                    

                foreach (var item in limitItems)
                {
                    if (!item.BlockPairName.Any() || !item.LimitGrids)
                    {
                        continue;
                    }

                    if (grid.Flags == (EntityFlags)4)
                    {
                        item.FoundEntities.Remove(grid.EntityId);
                        continue;
                    }
                    
                    
                    var isGridType = false;
                    
                    switch (item.GridTypeBlock)
                    {
                        case LimitItem.GridType.SmallGridsOnly:
                            isGridType = gridType == MyCubeSize.Small;
                            break;
                        case LimitItem.GridType.LargeGridsOnly:
                            isGridType = gridType == MyCubeSize.Large;
                            break;
                        case LimitItem.GridType.StationsOnly:
                            isGridType = grid.IsStatic;
                            break;
                        case LimitItem.GridType.AllGrids:
                            isGridType = true;
                            break;
                        case LimitItem.GridType.ShipsOnly:
                            isGridType = !isStatic;
                            break;
                    }

                    if (!isGridType)
                    {
                        item.FoundEntities.Remove(grid.EntityId);
                        continue;
                    }
                    
                    var builders = EntityCache.GetBuilders(grid);
                    if (builders == null || builders?.Any() == false)
                    {
                        item.FoundEntities.Remove(grid.EntityId);
                        continue;
                    }

                    if (item.IgnoreNpcs && builders.All(x => MySession.Static.Players.IdentityIsNpc(x)))

                    {
                        item.FoundEntities.Remove(grid.EntityId);
                        continue;
                    }

                    var gridBlocks = new List<MySlimBlock>();
                    
                    gridBlocks.AddRange(grid.CubeBlocks);
                    
                    if (gridBlocks?.Any()== false) 
                    {
                        item.FoundEntities.Remove(grid.EntityId);
                        continue;
                    }

                    var filteredBlocks = new List<MySlimBlock>();
                    
                    foreach (var block in gridBlocks)
                    {
                        if (!Utilities.IsMatch(block.BlockDefinition, item)) continue;
                        filteredBlocks.Add(block);
                    }
                    
                    var filteredBlocksCount = filteredBlocks.Count;

                    /*if (filteredBlocksCount < item.Limit)
                    {
                        item.DisabledEntities.Remove(grid.EntityId);
                        item.ViolatingEntities.Remove(grid.EntityId);
                        continue;
                    }*/
                    
                    var gridId = grid.EntityId;
                    
                    /*if (!item.DisabledEntities.Contains(gridId))item.DisabledEntities.Add(gridId);

                    if (filteredBlocksCount <= item.Limit)
                    {
                        item.ViolatingEntities.Remove(grid.EntityId);
                        continue;
                    }*/

                    var overCount = filteredBlocksCount - item.Limit;
                    
                    if (!item.FoundEntities.ContainsKey(gridId))
                    {
                        item.FoundEntities.Add(gridId, overCount);
                        continue;
                    }

                    item.FoundEntities[gridId] = overCount;
                }
                

            }

            _entityCache.Clear();

        }



    }
}
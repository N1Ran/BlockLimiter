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

            foreach (var item in limitItems)
            {
                if (!item.BlockPairName.Any() || !item.LimitGrids)
                {
                    continue;
                }
               
                foreach (var grid in grids)
                {
                   
                    if (grid == null || grid.EntityId == 0 || grid.EntityId == null)
                    {
                        continue;
                    }
                    if (grid.Flags == (EntityFlags)4)
                    {
                        item.DisabledEntities.Remove(grid.EntityId);
                        item.ViolatingEntities.Remove(grid.EntityId);
                        continue;
                    }
                    
                    
                    var isGridType = false;
                    switch (item.GridTypeBlock)
                    {
                        case LimitItem.GridType.SmallGridsOnly:
                            isGridType = grid.GridSizeEnum == MyCubeSize.Small;
                            break;
                        case LimitItem.GridType.LargeGridsOnly:
                            isGridType = grid.GridSizeEnum == MyCubeSize.Large;
                            break;
                        case LimitItem.GridType.StationsOnly:
                            isGridType = grid.IsStatic;
                            break;
                        case LimitItem.GridType.AllGrids:
                            isGridType = true;
                            break;
                        case LimitItem.GridType.ShipsOnly:
                            isGridType = !grid.IsStatic;
                            break;
                    }

                    if (!isGridType)
                    {
                        item.DisabledEntities.Remove(grid.EntityId);
                        item.ViolatingEntities.Remove(grid.EntityId);
                        continue;
                    }
                    
                    var builders = EntityCache.GetBuilders(grid);
                    if (builders == null || builders?.Any() == false)
                    {
                        item.DisabledEntities.Remove(grid.EntityId);
                        item.ViolatingEntities.Remove(grid.EntityId);
                        continue;
                    }

                    if (item.IgnoreNpcs && builders.All(x => MySession.Static.Players.IdentityIsNpc(x)))

                    {
                        item.DisabledEntities.Remove(grid.EntityId);
                        item.ViolatingEntities.Remove(grid.EntityId);
                        continue;
                    }

                    var gridBlocks = new List<MySlimBlock>();
                    
                    gridBlocks.AddRange(grid.CubeBlocks);
                    
                    if (gridBlocks?.Any()== false) 
                    {
                        item.DisabledEntities.Remove(grid.EntityId);
                        item.ViolatingEntities.Remove(grid.EntityId);
                        continue;
                    }

                    var filteredBlocks = new List<MySlimBlock>();
                    
                    foreach (var block in gridBlocks)
                    {
                        if (!Utilities.IsMatch(block.BlockDefinition, item)) continue;
                        filteredBlocks.Add(block);
                    }
                    
                    var filteredBlocksCount = filteredBlocks.Count;

                    if (filteredBlocksCount < item.Limit)
                    {
                        item.DisabledEntities.Remove(grid.EntityId);
                        item.ViolatingEntities.Remove(grid.EntityId);
                        continue;
                    }
                    
                    var gridId = grid.EntityId;
                    
                    if (!item.DisabledEntities.Contains(gridId))item.DisabledEntities.Add(gridId);

                    if (filteredBlocksCount <= item.Limit)
                    {
                        item.ViolatingEntities.Remove(grid.EntityId);
                        continue;
                    }

                    var overCount = filteredBlocksCount - item.Limit;
                    
                    if (!item.ViolatingEntities.ContainsKey(gridId))
                    {
                        item.ViolatingEntities.Add(gridId, overCount);
                        continue;
                    }

                    item.ViolatingEntities[gridId] = overCount;

                }

            }
            _entityCache.Clear();

        }



    }
}
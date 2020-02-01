using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
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
        private readonly HashSet<MyEntity> _gridCache = new HashSet<MyEntity>();

        public override int GetUpdateResolution()
        {
            return 400;
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
            
            _gridCache.Clear();
            GridCache.GetGrids(_gridCache);


            if (_gridCache.Count < 1)
            {
                return;
            }

            var limitItems = BlockLimiterConfig.Instance.AllLimits;

            foreach (var grid in _gridCache.OfType<MyCubeGrid>())
            {
                if (grid.EntityId == 0)
                {
                    continue;
                }


                var gridType = grid.GridSizeEnum;
                var isStatic = grid.IsStatic;

                    
                var builders = GridCache.GetBuilders(grid);
                var gridBlocks = new HashSet<MySlimBlock>();
                gridBlocks.UnionWith(grid.CubeBlocks);
                var gridId = grid.EntityId;

                if (limitItems == null || limitItems.Count < 1)continue;
                
                foreach (var item in limitItems)
                {
                    if (item.BlockPairName.Count < 1 || !item.LimitGrids)
                    {
                        continue;
                    }

                    if (item.Exceptions.Contains(grid.EntityId.ToString()) ||
                        item.Exceptions.Contains(grid.DisplayName))
                    {
                        item.FoundEntities.Remove(grid.EntityId);
                        continue;
                    }

                    if (grid.Flags == (EntityFlags)4)
                    {
                        item.FoundEntities.Remove(grid.EntityId);
                        continue;
                    }

                    if (grid.MarkedForClose || grid.MarkedForClose)
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
                    
                    if (builders == null || builders.Count < 1)
                    {
                        item.FoundEntities.Remove(grid.EntityId);
                        continue;
                    }

                    if (item.IgnoreNpcs && builders.All(x => MySession.Static.Players.IdentityIsNpc(x)))

                    {
                        item.FoundEntities.Remove(grid.EntityId);
                        continue;
                    }
                    
                    if (gridBlocks.Count < 1) 
                    {
                        item.FoundEntities.Remove(grid.EntityId);
                        continue;
                    }

                    var filteredBlocks = new HashSet<MySlimBlock>();
                    
                    filteredBlocks.UnionWith(gridBlocks.Where(x=>Utilities.IsMatch(x.BlockDefinition,item)));
                    
                    
                    var filteredBlocksCount = filteredBlocks.Count;

                    var overCount = filteredBlocksCount - item.Limit;
                    
                    if (!item.FoundEntities.ContainsKey(gridId))
                    {
                        item.FoundEntities.Add(gridId, overCount);
                        continue;
                    }

                    item.FoundEntities[gridId] = overCount;
                }
                
            }

            _gridCache.Clear();

        }



    }
}
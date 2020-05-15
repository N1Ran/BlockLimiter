using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using BlockLimiter.Settings;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Graphics.GUI;
using VRage.Game;

namespace BlockLimiter.Utility
{
    public static class Grid
    {
        public static bool IsSizeViolation(MyObjectBuilder_CubeGrid grid)
        {

            if (grid == null)
            {
                return false;
            }
            var gridSize = grid.CubeBlocks.Count;
            var gridType = grid.GridSizeEnum;
            var isStatic = grid.IsStatic;

            if (BlockLimiterConfig.Instance.MaxBlockSizeShips > 0 && !isStatic && gridSize >= BlockLimiterConfig.Instance.MaxBlockSizeShips)
            {
                return  true;
            }

            if (BlockLimiterConfig.Instance.MaxBlockSizeStations > 0 && isStatic && gridSize >= BlockLimiterConfig.Instance.MaxBlockSizeStations)
            {
                return  true;
            }

            if (BlockLimiterConfig.Instance.MaxBlocksLargeGrid > 0 && gridType == MyCubeSize.Large && gridSize >= BlockLimiterConfig.Instance.MaxBlocksLargeGrid)
            {
                return  true;
            }

            if (BlockLimiterConfig.Instance.MaxBlocksSmallGrid > 0 && gridType == MyCubeSize.Small && gridSize >= BlockLimiterConfig.Instance.MaxBlocksSmallGrid)
            {
                return  true;
            }

            return false;
        }
        public static bool IsSizeViolation(MyCubeGrid grid)
        {
            if (grid == null)
                return false;
            
            var gridSize = grid.CubeBlocks.Count;
            var gridType = grid.GridSizeEnum;
            var isStatic = grid.IsStatic;

            if (BlockLimiterConfig.Instance.MaxBlockSizeShips > 0 && !isStatic && gridSize >= BlockLimiterConfig.Instance.MaxBlockSizeShips)
            {
                return  true;
            }

            if (BlockLimiterConfig.Instance.MaxBlockSizeStations > 0 && isStatic && gridSize >= BlockLimiterConfig.Instance.MaxBlockSizeStations)
            {
                return  true;
            }

            if (BlockLimiterConfig.Instance.MaxBlocksLargeGrid > 0 && gridType == MyCubeSize.Large && gridSize >= BlockLimiterConfig.Instance.MaxBlocksLargeGrid)
            {
                return  true;
            }

            if (BlockLimiterConfig.Instance.MaxBlocksSmallGrid > 0 && gridType == MyCubeSize.Small && gridSize >= BlockLimiterConfig.Instance.MaxBlocksSmallGrid)
            {
                return  true;
            }

            return false;
        }

        public static bool CanMerge(MyCubeGrid grid1, MyCubeGrid grid2)
        {
            if (grid1 == null || grid2 == null) return true;
            
            var blocksHash = new HashSet<MySlimBlock>();
            
            blocksHash.UnionWith(grid1.CubeBlocks);
            blocksHash.UnionWith(grid2.CubeBlocks);
            
            var testGrid = new MyCubeGrid();
            testGrid.CubeBlocks.UnionWith(blocksHash);
            if (IsSizeViolation(testGrid)) return false;

            foreach (var limit in BlockLimiterConfig.Instance.AllLimits)
            {
                if (!limit.LimitGrids) continue;

                if (Utilities.IsExcepted(grid1.EntityId, limit.Exceptions)|| Utilities.IsExcepted(grid2.EntityId, limit.Exceptions)) continue;
                
                if (blocksHash.Count(x=> Block.IsMatch(x.BlockDefinition,limit)) <= limit.Limit) continue;
                return false;
            }

            return true;

        }

        public static bool AllowConversion(MyCubeGrid grid)
        {
            if (IsSizeViolation(grid)) return false;

            foreach (var limit in BlockLimiterConfig.Instance.AllLimits)
            {
                if (!limit.LimitGrids) continue;
                switch (limit.GridTypeBlock)
                {
                    case LimitItem.GridType.ShipsOnly when !grid.IsStatic:
                    case LimitItem.GridType.StationsOnly when grid.IsStatic:
                    case LimitItem.GridType.AllGrids:
                    case LimitItem.GridType.SmallGridsOnly:
                    case LimitItem.GridType.LargeGridsOnly:
                        continue;
                }
                
                if (grid.BigOwners.Count > 0 && grid.BigOwners.Any(x=> Utilities.IsExcepted(x, limit.Exceptions))) continue;


                var count = grid.CubeBlocks.Count(x => Block.IsMatch(x.BlockDefinition, limit));


                if (count <= limit.Limit)
                {
                    continue;
                }

                return false;

            }

            return true;
        }
        
        public static bool IsGridType(MyCubeGrid grid, LimitItem item)
        {
            switch (item.GridTypeBlock)
            {
                case LimitItem.GridType.SmallGridsOnly:
                    return grid.GridSizeEnum == MyCubeSize.Small;
                case LimitItem.GridType.LargeGridsOnly:
                    return grid.GridSizeEnum == MyCubeSize.Large;
                case LimitItem.GridType.StationsOnly:
                    return grid.IsStatic;
                case LimitItem.GridType.ShipsOnly:
                    return !grid.IsStatic;
                case LimitItem.GridType.AllGrids:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsGridType(MyObjectBuilder_CubeGrid grid, LimitItem item)
        {
            switch (item.GridTypeBlock)
            {
                case LimitItem.GridType.AllGrids:
                    return true;
                case LimitItem.GridType.SmallGridsOnly:
                    return grid.GridSizeEnum == MyCubeSize.Small;
                case LimitItem.GridType.LargeGridsOnly:
                    return grid.GridSizeEnum == MyCubeSize.Large;
                case LimitItem.GridType.StationsOnly:
                    return grid.IsStatic;
                case LimitItem.GridType.ShipsOnly:
                    return !grid.IsStatic;
                default:
                    return false;
            }
        }

        public static bool IsBlockType(MyCubeBlockDefinition def, LimitItem.GridType type)
        {
            var isType = false;
            
            switch (type)
            {
                case LimitItem.GridType.AllGrids:
                    isType = true;
                    break;
                case LimitItem.GridType.SmallGridsOnly:
                    isType = def.CubeSize == MyCubeSize.Small;
                    break;
                case LimitItem.GridType.LargeGridsOnly:
                    isType = def.CubeSize == MyCubeSize.Large;
                    break;
                case LimitItem.GridType.StationsOnly:
                    break;
                case LimitItem.GridType.ShipsOnly:
                    break;
            }

            return isType;
        }

        public static bool CanSpawn(MyObjectBuilder_CubeGrid grid, long playerId)
        {
            if (Utilities.IsExcepted(playerId, new List<string>())) return true;
            if (grid == null || IsSizeViolation(grid)) return false;

            return playerId == 0 || Block.CanAdd(grid.CubeBlocks, playerId, out _);
        }


    }
}
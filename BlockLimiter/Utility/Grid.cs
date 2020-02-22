using System;
using System.Linq;
using System.Net;
using BlockLimiter.Settings;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Graphics.GUI;
using VRage.Game;

namespace BlockLimiter.Utility
{
    public static class Grid
    {
        public static bool BlockGridSpawn(MyObjectBuilder_CubeGrid grid)
        {
            if (GridSizeViolation(grid))
                return true;
            return grid.CubeBlocks.Any(x => Block.AllowBlock(MyDefinitionManager.Static.GetCubeBlockDefinition(x), 0,(MyObjectBuilder_CubeGrid) null));
        }



        public static bool GridSizeViolation(MyObjectBuilder_CubeGrid grid)
        {
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
        public static bool GridSizeViolation(MyCubeGrid grid)
        {
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

        public static bool AllowGridChange(MyCubeGrid grid)
        {
            if (GridSizeViolation(grid)) return false;

            foreach (var limit in BlockLimiterConfig.Instance.AllLimits)
            {
                if (limit.GridTypeBlock != LimitItem.GridType.AllGrids)
                {
                    switch (limit.GridTypeBlock)
                    {
                        case LimitItem.GridType.ShipsOnly when !grid.IsStatic:
                        case LimitItem.GridType.StationsOnly when grid.IsStatic:
                            continue;
                    }
                }

                if (limit.Exceptions.Contains(grid.BigOwners[0].ToString())) continue;

                var count = grid.CubeBlocks.Count(x => Block.IsMatch(x.BlockDefinition, limit));

                limit.FoundEntities[grid.EntityId] = 0;

                if (count <= limit.Limit)
                {
                    continue;
                }
                
                limit.FoundEntities[grid.EntityId] = count;

                return false;

            }

            return true;
        }
        
        public static bool BuildOnGrid(MyObjectBuilder_CubeGrid grid, out int blockCount)
        {
            blockCount = grid.CubeBlocks.Count;
            
            var gridType = grid.GridSizeEnum;
            var isStatic = grid.IsStatic;

            if (BlockLimiterConfig.Instance.MaxBlockSizeShips > 0 && !isStatic && blockCount >= BlockLimiterConfig.Instance.MaxBlockSizeShips)
            {
                return  false;
            }

            if (BlockLimiterConfig.Instance.MaxBlockSizeStations > 0 && isStatic && blockCount >= BlockLimiterConfig.Instance.MaxBlockSizeStations)
            {
                return  false;
            }

            if (BlockLimiterConfig.Instance.MaxBlocksLargeGrid > 0 && gridType == MyCubeSize.Large && blockCount >= BlockLimiterConfig.Instance.MaxBlocksLargeGrid)
            {
                return  false;
            }

            if (BlockLimiterConfig.Instance.MaxBlocksSmallGrid > 0 && gridType == MyCubeSize.Small && blockCount >= BlockLimiterConfig.Instance.MaxBlocksSmallGrid)
            {
                return  false;
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
                default:
                    return true;
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
                    throw new ArgumentOutOfRangeException();
            }
        }

    }
}
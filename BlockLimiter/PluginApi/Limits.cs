using System.Collections.Generic;
using BlockLimiter.Settings;
using BlockLimiter.Utility;
using Sandbox.Game.Entities.Cube;
using VRage.Game;

namespace BlockLimiter.PluginApi
{
    public static class Limits
    {
        public static bool CheckLimits(MyObjectBuilder_CubeGrid[] grids, long id = 0)
        {
            if (grids.Length == 0 || !BlockLimiterConfig.Instance.EnableLimits || Utilities.IsExcepted(id))
            {
                return false;
            }

            foreach (var grid in grids)
            {
                if (Grid.CanSpawn(grid, id)) continue;
                return true;
            }
            
            return false;

        }

        public static bool CanAdd(List<MySlimBlock> blocks, long id, out List<MySlimBlock> nonAllowedBlocks)
        {
            return Block.CanAdd(blocks, id, out nonAllowedBlocks);
        }
    }
}
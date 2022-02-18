﻿using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game;

namespace BlockLimiter.PluginApi
{
    public static class PointCheckApi
    {
        private static Func<MyCubeGrid, int> _GetGridBP;
        private static Func<MyCubeBlock, int> _GetBlockBP;
        private static Func<string, int> _GetGridBPId;

        /// <summary>
        /// Call on init or when you want to use the API
        /// </summary>
        public static void Init()
        {
            MyAPIGateway.Utilities.RegisterMessageHandler(321651981321, APIAssignment);
            MyAPIGateway.Utilities.SendModMessage(32165198165, "RequestingAPI");
        }

        /// <summary>
        /// Call on mod close
        /// </summary>
        public static void Close()
        {
            MyAPIGateway.Utilities.UnregisterMessageHandler(321651981321, APIAssignment);
        }

        public static int GetGridBP(MyCubeGrid grid) => _GetGridBP.Invoke(grid);
        public static int GetBlockBP(MyCubeBlock block) => _GetBlockBP.Invoke(block);
        public static int GetBlockBPById(string id) => _GetGridBPId.Invoke(id);
        
        public static int GetGridBP(MyObjectBuilder_CubeGrid grid)
        {
            var result = 0;
            if (!IsInstalled()) return 0;
            foreach (var block in grid.CubeBlocks)
            {
                result += _GetGridBPId.Invoke(block.SubtypeId.ToString());
            }

            return result;
        }

        public static bool IsInstalled() => _GetGridBP != null;


        private static void APIAssignment(object obj)
        {
            var dict = obj as IReadOnlyDictionary<string, Delegate>;

            if (dict == null)
                return;

            AssignDelegate(dict, "GetGridBP", ref _GetGridBP);
            AssignDelegate(dict, "GetBlockBP", ref _GetBlockBP);
            AssignDelegate(dict, "GetGridBPId", ref _GetGridBPId);
        }

        private static void AssignDelegate<T>(IReadOnlyDictionary<string, Delegate> delegates, string name, ref T field) where T : class
        {
            if (delegates == null)
            {
                field = null;
                return;
            }

            Delegate del;
            if (!delegates.TryGetValue(name, out del))
                throw new Exception($"PCAPI :: Couldn't find {name} delegate of type {typeof(T)}");

            field = del as T;

            if (field == null)
                throw new Exception($"PCAPI :: Delegate {name} is not type {typeof(T)}, instead it's: {del.GetType()}");
        }

    }
}

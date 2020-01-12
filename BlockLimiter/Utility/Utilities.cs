using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Timers;
using BlockLimiter.Settings;
using Sandbox.Definitions;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch.Managers;
using Torch.Mod;
using Torch.Mod.Messages;
using Torch.Utils;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRage.Network;

namespace BlockLimiter.Utility
{
    public static class Utilities
    {
        [ReflectedStaticMethod(Type = typeof(MyCubeBuilder), Name = "SpawnGridReply", OverrideTypes = new []{typeof(bool), typeof(ulong)})]
        private static Action<bool, ulong> _spawnGridReply;

       /* [ReflectedMethodInfo(typeof(MyMechanicalConnectionBlockBase), "NotifyTopPartFailed")]
        private static Action<MySession.LimitResult> _attachGridReply;*/

        public static string GetPlayerNameFromSteamId(ulong steamId)
        {
            var pid = MySession.Static.Players.TryGetIdentityId(steamId);
            if (pid == 0)
                return null;
            var id = MySession.Static.Players.TryGetIdentity(pid);
            return id?.DisplayName;
        }

        public static long GetPlayerIdFromSteamId(ulong steamId)
        {
            return MySession.Static.Players.TryGetIdentityId(steamId);
        }

        public static ulong GetSteamIdFromPlayerId(long playerId)
        {
            return MySession.Static.Players.TryGetSteamId(playerId);
        }

        public static void SendFailSound(ulong target)
        {
            _spawnGridReply(false, target);
        }

        public static bool TryGetEntityByNameOrId(string nameOrId, out IMyEntity entity)
        {
            if (long.TryParse(nameOrId, out long id))
                return MyAPIGateway.Entities.TryGetEntityById(id, out entity);

            foreach (var ent in MyEntities.GetEntities())
            {
                if (ent.DisplayName == nameOrId)
                {
                    entity = ent;
                    return true;
                }
            }

            entity = null;
            return false;
        }

        public static long NextInt64(Random rnd)
        {
            var buffer = new byte[sizeof(long)];
            rnd.NextBytes(buffer);
            return BitConverter.ToInt64(buffer, 0);
        }

        public static void ValidationFailed()
        {
            ((MyMultiplayerServerBase)MyMultiplayer.Static).ValidationFailed(MyEventContext.Current.Sender.Value);
        }

        public static bool IsMatch(MyCubeBlockDefinition block, LimitItem item)
        {
            var found = false;
            if (item.BlockPairName.Count < 1) return false;
            foreach (var name in item.BlockPairName)
            {
                if (!name.Equals(block.BlockPairName, StringComparison.OrdinalIgnoreCase)) continue;
                found = true;
                break;
            }

            return found;
        }

        public static bool IsOwner(LimitItem.OwnerState state, MySlimBlock block, long playerId)
        {
            var correctOwner = false;
            switch (state)
            {
                case LimitItem.OwnerState.BuiltbyId:
                    correctOwner = block.BuiltBy == playerId;
                    break;
                case LimitItem.OwnerState.OwnerId:
                    correctOwner = block.OwnerId == playerId;
                    break;
                case LimitItem.OwnerState.OwnerAndBuiltbyId:
                    correctOwner = block.OwnerId == playerId && block.BuiltBy == playerId;
                    break;
                case LimitItem.OwnerState.OwnerOrBuiltbyId:
                    correctOwner = block.OwnerId == playerId || block.BuiltBy == playerId;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }

            return correctOwner;
        }
    }
}

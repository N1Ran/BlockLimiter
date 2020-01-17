using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BlockLimiter.Settings;
using Sandbox;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Entities.Blocks;
using Sandbox.ModAPI;
using Torch.Managers.PatchManager;
using Torch.Mod;
using Torch.Mod.Messages;
using Torch.Utils;
using VRage.Game;
using VRage.Network;
using System.Linq;
using BlockLimiter.ProcessHandlers;
using BlockLimiter.Utility;
using NLog;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using VRage.Utils;

namespace BlockLimiter.Handlers
{
    [PatchShim]
    public static class ProjectionHandler
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private static readonly FieldInfo OriginalGridFields = typeof(MyProjectorBase).GetField("m_originalGridBuilders", BindingFlags.NonPublic | BindingFlags.Instance);
        private static  readonly MethodInfo RemoveProjectionMethod = typeof(MyProjectorBase).GetMethod("OnRemoveProjectionRequest", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo NewBlueprintMethod = typeof(MyProjectorBase).GetMethod("OnNewBlueprintSuccess", BindingFlags.NonPublic | BindingFlags.Instance);
        private  static List<MyObjectBuilder_CubeBlock> _blockList = new List<MyObjectBuilder_CubeBlock>();

        public static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(NewBlueprintMethod).Prefixes.Add(typeof(ProjectionHandler).GetMethod(nameof(PrefixNewBlueprint)));

        }

        public static void PrefixNewBlueprint(MyProjectorBase __instance, ref List<MyObjectBuilder_CubeGrid> projectedGrids)
        {

            if (!BlockLimiterConfig.Instance.EnableLimits || !BlockLimiterConfig.Instance.LimitItems.Any(l=>l.RestrictProjection))return;
            
            var proj = __instance;
            if (proj == null)
            {
                Log.Debug("Null projector in ProjectionHandler");
                return;
            }

            foreach (var projectedGrid in projectedGrids)
            {
                _blockList.Clear();
                var grid = projectedGrid;
                var blocks = projectedGrid.CubeBlocks;
                var remoteUserId = MyEventContext.Current.Sender.Value;
                var count = 0;
                for (var i = blocks.Count - 1; i >= 0; i--)
                {
                    var block = blocks[i];
                    block.BuiltBy = __instance.BuiltBy;
                    if (IsAllowed(block, remoteUserId))
                    {
                        _blockList.Add(block);
                        continue;
                    }
                    blocks.RemoveAtFast(i);
                    count++;
                }
                if (count <= 0) continue;
                MyMultiplayer.RaiseEvent(__instance, x => (Action)Delegate.CreateDelegate(typeof(Action), x, RemoveProjectionMethod), new EndpointId(remoteUserId));
                Task.Run(() =>
                {
                    Thread.Sleep(100);
                    MySandboxGame.Static.Invoke(() =>
                        {
                            ((IMyProjector)__instance).SetProjectedGrid(null);
                            Thread.Sleep(500);
                            ((IMyProjector)__instance).SetProjectedGrid(grid);
                        },
                        "BlockLimiter");
                });
               ModCommunication.SendMessageTo(new NotificationMessage($"Blocklimiter removed {count} blocks blueprint!", 15000, MyFontEnum.Red), remoteUserId);
                //((IMyProjector)__instance).SetProjectedGrid(projectedGrid);
            }

        }

        private static bool IsAllowed(MyObjectBuilder_CubeBlock block, ulong target)
        {
            
            if (!BlockLimiterConfig.Instance.EnableLimits) return true;

            var limitItems = BlockLimiterConfig.Instance.AllLimits;

            if (limitItems.Count < 1) return true;

            var playerId = Utilities.GetPlayerIdFromSteamId(target);
            var playerFaction = MySession.Static.Factions.GetPlayerFaction(playerId);
            var blockDef = FindDefinition(block);
            if (blockDef == null) return true;
            var remove = false;
            foreach (var item in limitItems)
            {
                if (item.BlockPairName.Count < 1) continue;
                if (!Utilities.IsMatch(blockDef, item)) continue;

                if (item.Exceptions.Any())
                {
                    var skip = false;
                    foreach (var id in item.Exceptions)
                    {
                        if (long.TryParse(id, out var someId) && (someId == playerId || someId == playerFaction?.FactionId))
                        {
                            skip = true;
                            break;
                        }

                        if (ulong.TryParse(id, out var steamId) && steamId == target)
                        {
                            skip = true;
                            break;
                        }

                        if (Utilities.TryGetEntityByNameOrId(id, out var entity) && entity != null && ((MyCharacter) entity).ControlSteamId == target)
                        {
                            skip = true;
                            break;
                        }

                        if (id.Length > 4 && playerFaction == null) continue;
                        if (id.Equals(playerFaction?.Tag,StringComparison.OrdinalIgnoreCase)) continue;
                        skip = true;
                        break;
                    }
                    
                    if (skip)continue;
                }
                
                if (item.Limit == 0)
                {
                    remove = true;
                    break;
                }

                if (item.FoundEntities.TryGetValue(playerId, out var playerCount))
                {
                    if (playerCount >= 0)
                    {
                        remove = true;
                        break;
                    }
                }


                if (playerFaction==null)break;
                if (!item.FoundEntities.TryGetValue(playerId, out var factionCount)|| factionCount >= 0)
                {
                    continue;
                }

                remove = true;
                break;

            }

            return !remove;

        }

        
        private static Dictionary<MyStringHash, MyDefinitionBase> _cache  = new Dictionary<MyStringHash, MyDefinitionBase>();
        private static MyCubeBlockDefinition FindDefinition(MyObjectBuilder_CubeBlock block)
        {
            if (_cache.TryGetValue(block.SubtypeId, out var def))
                return (MyCubeBlockDefinition)def;

            foreach (var baseDef in MyDefinitionManager.Static.GetAllDefinitions().OfType<MyCubeBlockDefinition>().Where(baseDef => baseDef.Id.SubtypeId.Equals(block.SubtypeId)))
            {
                _cache[block.SubtypeId] = baseDef;
                return baseDef;

            }

            _cache[block.SubtypeId] = null;
            return null;
        }
    }
}
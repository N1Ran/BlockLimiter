using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using BlockLimiter.Patch;
using BlockLimiter.ProcessHandlers;
using BlockLimiter.Punishment;
using BlockLimiter.Settings;
using BlockLimiter.Utility;
using NLog;
using Sandbox;
using Sandbox.Definitions;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch.API.Plugins;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Session;
using Torch.Session;
using Torch.Views;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.VisualScripting;
using VRage.Network;
using VRage.Profiler;
using Grid = BlockLimiter.Utility.Grid;

namespace BlockLimiter
{
    public class BlockLimiter : TorchPluginBase, IWpfPlugin
    {
        public readonly Logger Log = LogManager.GetLogger("BlockLimiter");
        private Thread _processThread;
        private List<Thread> _processThreads;
        private static bool _running;
        public static BlockLimiter Instance { get; private set; }
        private TorchSessionManager _sessionManager;
        private List<ProcessHandlerBase> _limitHandlers;
        public readonly HashSet<LimitItem> VanillaLimits = new HashSet<LimitItem>();
        
        private int _updateCounter;


        private void DoInit()
        {

            _limitHandlers = new List<ProcessHandlerBase>
            {
                new Annoy(),
                new Punish()
            };
            _processThreads = new List<Thread>();
            _processThread = new Thread(PluginProcessing);
            _processThread.Start();
            
            MyCubeGrid.OnSplitGridCreated += MyCubeGridOnOnSplitGridCreated;
            MyMultiplayer.Static.ClientJoined += StaticOnClientJoined;
            MyCubeGrids.BlockBuilt += MyCubeGridsOnBlockBuilt;
            MySession.Static.Factions.FactionStateChanged += FactionsOnFactionStateChanged;
            MyEntities.OnEntityAdd += MyEntitiesOnOnEntityAdd;
        }

        private void MyEntitiesOnOnEntityAdd(MyEntity entity)
        {
            if (!(entity is MyCubeGrid grid)) return;
            if (!BlockLimiterConfig.Instance.CountProjections && (grid.Projector != null||grid.IsPreview)) return;

            var blocks = grid.CubeBlocks;

            foreach (var block in blocks)
            {
                Block.IncreaseCount(block.BlockDefinition,block.OwnerId,1,grid.EntityId);
            }


        }

        private void FactionsOnFactionStateChanged(MyFactionStateChange factionState, long fromFaction, long toFaction, long playerId, long senderId)
        {
            if (factionState == MyFactionStateChange.RemoveFaction)
            {
                foreach (var limit in BlockLimiterConfig.Instance.AllLimits)
                {
                    limit.Exceptions.Remove(fromFaction.ToString());
                    limit.FoundEntities.Remove(fromFaction);
                }

                return;
            }

            UpdateLimits.FactionLimit(fromFaction);
            UpdateLimits.FactionLimit(toFaction);
            UpdateLimits.PlayerLimit(playerId);

        }

        private void MyCubeGridsOnBlockBuilt(MyCubeGrid grid, MySlimBlock block)
        {
            if (!GridCache.TryGetGridById(grid.EntityId, out _))
            {
                GridCache.AddGrid(grid.EntityId);
                return;
            }
            Block.IncreaseCount(block.BlockDefinition,block.BuiltBy,1,grid.EntityId);


        }

        private static void MyCubeGridOnOnSplitGridCreated(MyCubeGrid grid)
        {
            if (grid == null) return;
            
            Task.Run(() =>
            {
                Thread.Sleep(100);
                var entity = MyEntities.GetEntityByName(grid.Name);
                var newStateGrid = entity as MyCubeGrid;
                if (newStateGrid == null) return;
                UpdateLimits.GridLimit(newStateGrid);
            });
        }

        private static void StaticOnClientJoined(ulong obj)
        {
            var player = MySession.Static.Players.TryGetPlayerBySteamId(obj);
            if (player == null)return;
            UpdateLimits.PlayerLimit(player.Identity.IdentityId);
        }



        private void GetVanillaLimits()
        {
            var limits = new List<LimitItem>(MySession.Static.BlockTypeLimits.Count());

            switch (MySession.Static.BlockLimitsEnabled)
            {
                case MyBlockLimitsEnabledEnum.NONE:
                    break;
                case MyBlockLimitsEnabledEnum.GLOBALLY:
                    break;
                case MyBlockLimitsEnabledEnum.PER_FACTION:
                    limits.AddRange(MySession.Static.BlockTypeLimits.Select(item => new LimitItem
                    {
                        LimitFaction = true,
                        LimitPlayers = false,
                        LimitGrids = false,
                        Limit = item.Value,
                        BlockList = new List<string>{item.Key}
                    }));

                    break;
                case MyBlockLimitsEnabledEnum.PER_PLAYER:
                    limits.AddRange(MySession.Static.BlockTypeLimits.Select(item => new LimitItem
                    {
                        LimitFaction = false,
                        LimitPlayers = true,
                        LimitGrids = false,
                        Limit = item.Value,
                        BlockList = new List<string>{item.Key}
                    }));

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            VanillaLimits.UnionWith(limits);
        }



        private void PluginProcessing()
        {
            try
            {
                foreach (var handler in _limitHandlers)
                {
                    ProcessHandlerBase currentHandler = handler;
                    var thread = new Thread(() =>
                    {
                        while (_running)
                        {
                            if (currentHandler.CanProcess())
                            {
                                try
                                {
                                    currentHandler.Handle();
                                }
                                catch (Exception ex)
                                {
                                    if (BlockLimiterConfig.Instance.EnableLog)
                                        Log.Warn("Handler Problems: {0} - {1}", currentHandler.GetUpdateResolution(),
                                            ex);
                                }

                                currentHandler.LastUpdate = DateTime.Now;
                            }

                            Thread.Sleep(100);
                        }

                    });
                    _processThreads.Add(thread);
                    thread.Start();
                }

                foreach (Thread thread in _processThreads)
                    thread.Join();

            }
            catch (ThreadAbortException ex)
            {
                if (BlockLimiterConfig.Instance.EnableLog) 
                    Log.Trace(ex);
            }
            catch (Exception ex)
            {
                if (BlockLimiterConfig.Instance.EnableLog) 
                    Log.Error(ex);
            }
        }

        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            Instance = this;
            Load();
            //CopyOver();
            _sessionManager = Torch.Managers.GetManager<TorchSessionManager>();
            if (_sessionManager != null)
                _sessionManager.SessionStateChanged += SessionChanged;
            
        }



        public override void Update()
        {
            base.Update();
            if (MyAPIGateway.Session == null|| !BlockLimiterConfig.Instance.EnableLimits)
                return;
            if (++_updateCounter % 1000 == 0)
            {
                GridCache.Update();
            }

        }


        private  void SessionChanged(ITorchSession session, TorchSessionState state)
        {
            _running = state == TorchSessionState.Loaded;
            switch (state)
            {
                case TorchSessionState.Loaded:
                    DoInit();
                    EnableControl();
                    GetVanillaLimits();
                    GridCache.Update();
                    Utilities.UpdateLimits(BlockLimiterConfig.Instance.UseVanillaLimits, out BlockLimiterConfig.Instance.AllLimits);
                    ResetLimits();
                    break;
                case TorchSessionState.Unloading:
                    Dispose();
                    break;
            }
        }

        private static void Load()
        {
            BlockLimiterConfig.Instance.Load();
        }
        
        private UserControl _control;
        private UserControl Control => _control ?? (_control = new PropertyGrid{ DataContext = BlockLimiterConfig.Instance});
        public UserControl GetControl()
        {
            return Control;
        }
        private void EnableControl(bool enable = true)
        {
            _control?.Dispatcher?.Invoke(() =>
            {
                Control.IsEnabled = enable;
                Control.DataContext = BlockLimiterConfig.Instance;
            });

        }

        /*
        
        /// <summary>
        /// TO Do Remove on next update
        /// </summary>
        private void CopyOver()
        {
            foreach (var limit in BlockLimiterConfig.Instance.LimitItems)
            {
                if (limit.BlockPairName.Count == 0) continue;
                limit.BlockList.AddRange(limit.BlockPairName);
                limit.BlockPairName.Clear();
            }
            BlockLimiterConfig.Instance.Save();

        }
        */

        public override void Dispose()
        {
            base.Dispose();
            foreach (var thread in _processThreads)
                thread.Abort();
            _processThread.Abort();
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

            if (updateGrids)
            {
                var grids = new HashSet<MyCubeGrid>();
                GridCache.GetGrids(grids);
                
                
                Task.Run(() =>
                {
                    Thread.Sleep(500);
                    foreach (var grid in grids)
                    {
                        if (grid == null) continue;
                        Parallel.Invoke(()=>
                        {
                            Thread.Sleep(100);
                            UpdateLimits.GridLimit(grid);
                        });
                    }
                });
            }

            if (updatePlayers)
            {
                Task.Run(() =>
                {
                    Thread.Sleep(300);
                    foreach (var player in MySession.Static.Players.GetAllPlayers())
                    {
                        if (player.SteamId == 0) continue;
                        var identity = Utilities.GetPlayerIdFromSteamId(player.SteamId);
                        if (identity == 0) continue;
                        Parallel.Invoke(()=>UpdateLimits.PlayerLimit(identity));
                    }
                });
            }

            if (updateFactions)
            {
                Task.Run(() =>
                {
                    Thread.Sleep(100);
                    foreach (var (id,faction) in MySession.Static.Factions.Factions)
                    {
                        if (faction.IsEveryoneNpc() || id == 0) continue;
                        Parallel.Invoke(()=>UpdateLimits.FactionLimit(id));
                    }
                });
            }

        }

        
        
        public static bool CheckLimits_future(MyObjectBuilder_CubeGrid[] grids, long id = 0)
        {
            if (grids.Length == 0 ||!BlockLimiterConfig.Instance.EnableLimits || Utilities.IsExcepted(id, new List<string>()))
            {
                return false;
            }


            return !grids.Any(Grid.IsSizeViolation) && grids.Any(x=> !Grid.CanSpawn(x,id));
        }

        public static bool CanAdd(List<MySlimBlock> blocks, long id, out List<MySlimBlock> nonAllowedBlocks)
        {
            return Block.CanAdd(blocks, id, out nonAllowedBlocks);
        }
        

        
    }


    }

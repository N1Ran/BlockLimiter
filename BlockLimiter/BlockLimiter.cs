using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Instrumentation;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using BlockLimiter.Patch;
using BlockLimiter.ProcessHandlers;
using BlockLimiter.Punishment;
using BlockLimiter.Settings;
using BlockLimiter.Utility;
using MultigridProjector.Api;
using Newtonsoft.Json;
using NLog;
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
using Torch.Managers;
using Torch.Session;
using Torch.Views;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using Grid = BlockLimiter.Utility.Grid;

namespace BlockLimiter
{
    public class BlockLimiter : TorchPluginBase, IWpfPlugin
    {
        public readonly Logger Log = LogManager.GetLogger("BlockLimiter");
        private Thread _processThread;
        private List<Thread> _processThreads = new List<Thread>();
        private static bool _running;
        public static BlockLimiter Instance { get; private set; }
        public static string ChatName => Instance.Torch.Config.ChatName;
        public static string ChatColor => Instance.Torch.Config.ChatColor;
        private TorchSessionManager _sessionManager;
        private List<ProcessHandlerBase> _limitHandlers = new List<ProcessHandlerBase>();
        public readonly HashSet<LimitItem> VanillaLimits = new HashSet<LimitItem>();
        private int _updateCounter;
        public static IPluginManager PluginManager { get; private set; }
        public string timeDataPath = "";

        public IMultigridProjectorApi MultigridProjectorApi;

        private void DoInit()
        {
            MultigridProjectorApi = new MultigridProjectorTorchAgent(_sessionManager.CurrentSession);

            _limitHandlers = new List<ProcessHandlerBase>
            {
                new Annoy(),
                new Punish()
            };
            _processThreads = new List<Thread>();
            _processThread = new Thread(PluginProcessing);
            _processThread.Start();
            
            MyMultiplayer.Static.ClientJoined += StaticOnClientJoined;
            MyCubeGrids.BlockBuilt += MyCubeGridsOnBlockBuilt;
            MySession.Static.Factions.FactionStateChanged += FactionsOnFactionStateChanged;
            MySession.Static.Factions.FactionCreated += FactionsOnFactionCreated;
            MyEntities.OnEntityAdd += MyEntitiesOnOnEntityAdd;
        }

        private void FactionsOnFactionCreated(long id)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits) return;
            UpdateLimits.FactionLimit(id);
        }

        private void MyEntitiesOnOnEntityAdd(MyEntity entity)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits) return;

            if (!(entity is MyCubeGrid grid)) return;
            
            if (grid.Projector != null||grid.IsPreview) return;

            var biggestGrid = Grid.GetBiggestGridInGroup(grid);

            var blocks = grid.CubeBlocks;

            foreach (var block in blocks)
            {
                if (block.BuiltBy == block.OwnerId)
                    Block.IncreaseCount(block.BlockDefinition,block.BuiltBy,1,grid.EntityId);
                else
                {
                    Block.IncreaseCount(block.BlockDefinition,block.BuiltBy,1,grid.EntityId);
                    Block.IncreaseCount(block.BlockDefinition,block.OwnerId);
                }
                if (BlockLimiterConfig.Instance.CountSubGrids && biggestGrid != null && biggestGrid != grid)
                {
                    Block.IncreaseCount(block.BlockDefinition,grid.EntityId,1,biggestGrid.EntityId);

                }
            }


        }

        private void FactionsOnFactionStateChanged(MyFactionStateChange factionState, long fromFaction, long toFaction, long playerId, long senderId)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits || (factionState != MyFactionStateChange.FactionMemberLeave && factionState != MyFactionStateChange.FactionMemberAcceptJoin && factionState != MyFactionStateChange.RemoveFaction
                ))return;
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
            if (grid == null || !BlockLimiterConfig.Instance.EnableLimits) return;

            var biggestGrid = Grid.GetBiggestGridInGroup(grid);

            if (!GridCache.TryGetGridById(grid.EntityId, out _))
            {
                GridCache.AddGrid(grid);
                return;
            }

            if (grid == biggestGrid || !BlockLimiterConfig.Instance.CountSubGrids)
            {
                Block.IncreaseCount(block.BlockDefinition,block.BuiltBy,1,grid.EntityId);
                return;
            }

            Block.IncreaseCount(block.BlockDefinition,block.BuiltBy,1,grid.EntityId);
            Block.IncreaseCount(block.BlockDefinition,block.BuiltBy,1,biggestGrid.EntityId);
        }


        private static void StaticOnClientJoined(ulong obj, string playerName)
        {
            if (obj == 0) return;
            if (!BlockLimiterConfig.Instance.EnableLimits) return;
            var identityId = Utilities.GetPlayerIdFromSteamId(obj);
            if (identityId == 0)return;
            var playerFaction = MySession.Static.Factions.GetPlayerFaction(identityId);
            UpdateLimits.PlayerLimit(identityId);
            if (playerFaction != null)UpdateLimits.PlayerLimit(playerFaction.FactionId);
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

                foreach (var thread in _processThreads)
                    thread.Join();

            }
            catch (ThreadAbortException ex)
            {
                    Log.Trace(ex);
            }
            catch (Exception ex)
            {
                    Log.Error(ex);
            }
        }

        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            Instance = this;
            PluginManager = Torch.Managers.GetManager<PluginManager>();
            Load();
            _sessionManager = Torch.Managers.GetManager<TorchSessionManager>();
            if (_sessionManager != null)
                _sessionManager.SessionStateChanged += SessionChanged;

        }



        public override void Update()
        {
            base.Update();
            if (MyAPIGateway.Session == null|| !BlockLimiterConfig.Instance.EnableLimits)
                return;
            if (++_updateCounter % 100 != 0) return;
            GridCache.Update();
            MergeBlockPatch.MergeBlockCache?.Clear();


        }


        private  void SessionChanged(ITorchSession session, TorchSessionState state)
        {
            _running = state == TorchSessionState.Loaded;
            switch (state)
            {
                case TorchSessionState.Loading:
                    timeDataPath = Path.Combine(Torch.CurrentSession.KeenSession.CurrentPath, "BLPlayerTime.json");
                    if (!File.Exists(timeDataPath)) File.Create(timeDataPath);

                    break;

                case TorchSessionState.Loaded:

                    var data = File.ReadAllText(timeDataPath);
                    if (!string.IsNullOrEmpty(data))
                    {
                        PlayerTimeModule.PlayerTimes =
                            JsonConvert.DeserializeObject<List<PlayerTimeModule.PlayerTimeData>>(data);
                    }

                    Torch.CurrentSession.Managers.GetManager<IMultiplayerManagerServer>().PlayerJoined +=
                        PlayerTimeModule.LogTime;
                    DoInit();
                    EnableControl();
                    GetVanillaLimits();
                    if (BlockLimiterConfig.Instance.EnableLimits)
                    {
                        Activate(); 
                    }
                    break;
                case TorchSessionState.Unloading:
                    break;
                default:
                    return;
            }
        }

        public void Activate()
        {
            if (_sessionManager == null) return;
            Task.Run(() =>
            {
                var test = Torch.InvokeAsync(GridCache.Update);
                Task.WaitAny(test);
                if (test.Result <= 0) return;
                BlockLimiterConfig.Instance.AllLimits =
                    new HashSet<LimitItem>(
                        Utilities.UpdateLimits(BlockLimiterConfig.Instance.UseVanillaLimits));
               if (BlockLimiterConfig.Instance.BlockOwnershipTransfer) Block.FixIds();
                ResetLimits();
            });
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


        public override void Dispose()
        {
            base.Dispose();
            try
            {
                foreach (var thread in _processThreads)
                    thread.Abort();
                _processThread.Abort();
            }
            catch (Exception e)
            {
                Log.Warn(e,"Session failed to load.  Check world for corruption");

            }
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
                if (grids.Count > 0)
                    Task.Run(() =>
                    {
                        Parallel.ForEach(grids, grid =>
                        {
                        if (grid == null) return;

                        UpdateLimits.GridLimit(grid);
                        });
                    });


            }

            if (updatePlayers)
            {
                var players = MySession.Static.Players.GetAllPlayers();
                if (players.Count > 0)
                {
                    Task.Run(() =>
                    {
                        Parallel.ForEach(players, player =>
                        {
                            if (player.SteamId == 0) return;

                            var identity = Utilities.GetPlayerIdFromSteamId(player.SteamId);

                            if (identity == 0) return;

                            UpdateLimits.PlayerLimit(identity);
                        });
                    });

                }

            }

            if (updateFactions)
            {
                Task.Run(() =>
                {
                    Parallel.ForEach(MySession.Static.Factions, factionInfo =>
                    {
                        var (id, faction) = factionInfo;

                        if (faction.IsEveryoneNpc() || id == 0) return;

                        UpdateLimits.FactionLimit(id);
                    });
                });
            }

        }


        //The methods below are method used by other plugins to check limits from Blocklimiter
        #region External Access
        
        public static bool CheckLimits_future(MyObjectBuilder_CubeGrid[] grids, long id = 0)
        {
            if (grids.Length == 0 ||!BlockLimiterConfig.Instance.EnableLimits || Utilities.IsExcepted(id))
            {
                return false;
            }

            foreach (var grid in grids)
            {
                if (Grid.CanSpawn(grid,id)) continue;
                return true;
            }

            return false;

        }

        public static bool CanAdd(List<MySlimBlock> blocks, long id, out List<MySlimBlock> nonAllowedBlocks)
        {
            return Block.CanAdd(blocks, id, out nonAllowedBlocks);
        }
        
        #endregion

        
    }


    }

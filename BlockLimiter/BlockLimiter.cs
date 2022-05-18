using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using BlockLimiter.Patch;
using BlockLimiter.PluginApi;
using BlockLimiter.PluginApi.MultigridProjectorApi;
using BlockLimiter.ProcessHandlers;
using BlockLimiter.Punishment;
using BlockLimiter.Settings;
using BlockLimiter.Utility;
using Newtonsoft.Json;
using NLog;
using NLog.Fluent;
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
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;

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
        private int _updateCounter100;
        private int _updateCounter10;
        public static IPluginManager PluginManager { get; private set; }
        public string timeDataPath = "";
        private MyConcurrentHashSet<MySlimBlock> _justAdded = new MyConcurrentHashSet<MySlimBlock>();

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
            UpdateLimits.Enqueue(id);
        }

        /// <summary>
        /// Adds newly added grids to cache and update count to meet change
        /// </summary>
        /// <param name="entity"></param>
        private void MyEntitiesOnOnEntityAdd(MyEntity entity)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits) return;

            if (!(entity is MyCubeGrid grid)) return;

            if (grid.Projector != null) return;
            // Do Not Add to grid cache at this point to allow MyCubeGridsOnBlockBuild to add and prevent double counts
            var blocks = grid.CubeBlocks;
            GridCache.AddGrid(grid);
            foreach (var block in blocks)
            {
                if (_justAdded.Contains(block))
                {
                    _justAdded.Remove(block);
                    continue;
                }
                _justAdded.Add(block);
                Block.IncreaseCount(block.BlockDefinition,
                    block.BuiltBy == block.OwnerId
                        ? new List<long> {block.BuiltBy}
                        : new List<long> {block.BuiltBy, block.OwnerId}, 1, grid.EntityId);
            }


        }
        
        /// <summary>
        /// Event to refresh player's faction/player limits to account for change
        /// </summary>
        /// <param name="factionState"></param>
        /// <param name="fromFaction"></param>
        /// <param name="toFaction"></param>
        /// <param name="playerId"></param>
        /// <param name="senderId"></param>
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

            UpdateLimits.Enqueue(fromFaction);
            UpdateLimits.Enqueue(toFaction);
            UpdateLimits.Enqueue(playerId);

        }

        /// <summary>
        /// Accounts for block being added to a grid that already exist in the world
        /// </summary>
        /// <param name="grid"></param>
        /// <param name="block"></param>
        private void MyCubeGridsOnBlockBuilt(MyCubeGrid grid, MySlimBlock block)
        {
            if (grid == null || !BlockLimiterConfig.Instance.EnableLimits) return;

            //This adds grid to cache and also prevents double count from MyEntitiesOnEntityAdd
            if (!GridCache.TryGetGridById(grid.EntityId, out _))
            {
                GridCache.AddGrid(grid);
                return;
            }

            if (_justAdded.Contains(block))
            {
                _justAdded.Remove(block);
                return;
            }

            _justAdded.Add(block);
            GridCache.AddBlock(block);
            
            Block.IncreaseCount(block.BlockDefinition,new List<long>{block.BuiltBy},1,grid.EntityId);
            
        }


        private static void StaticOnClientJoined(ulong obj, string playerName)
        {
            if (obj == 0) return;
            if (!BlockLimiterConfig.Instance.EnableLimits) return;
            var identityId = Utilities.GetPlayerIdFromSteamId(obj);
            if (identityId == 0)return;
            //var playerFaction = MySession.Static.Factions.GetPlayerFaction(identityId);
            UpdateLimits.Enqueue(identityId);
            //if (playerFaction != null)UpdateLimits.Enqueue(playerFaction.FactionId);
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



        /// <summary>
        /// Hey Ryo,
        /// This makes new sleeping threads for punishment and annoy.  This will show up in DotTrace since it's a process that
        /// never ends, but it's not always running a method.  Think of it as a cheap timer.  
        /// </summary>
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
            if (++_updateCounter10 % 10 == 0)
            {
                GridChange.ClearRemoved();
                UpdateLimits.Dequeue();
                Punish.Update();
            }
            if (++_updateCounter100 % 100 != 0) return;
            MergeBlockPatch.MergeBlockCache?.Clear();
        }


        private  void SessionChanged(ITorchSession session, TorchSessionState state)
        {
            _running = state == TorchSessionState.Loaded;
            switch (state)
            {
                case TorchSessionState.Loading:
                    var storageDir = Path.Combine(Torch.CurrentSession.KeenSession.CurrentPath, "Storage");
                    if (!Directory.Exists(storageDir))
                    {
                        Directory.CreateDirectory(storageDir);
                    }
                    timeDataPath = Path.Combine(storageDir, "BLPlayerTime.json");
                    if (!File.Exists(timeDataPath))
                    {
                        var stream = File.Create(timeDataPath);
                        Log.Warn($"Creating Player Time data at {timeDataPath}");
                        stream.Dispose();
                    }

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
            BlockLimiterConfig.Instance.AllLimits =
                new HashSet<LimitItem>(
                    Utilities.UpdateLimits(BlockLimiterConfig.Instance.UseVanillaLimits));
            EssentialsPlayerAccount.InitializeCommunication();
            Task.Run(() =>
            {
                var task = Torch.InvokeAsync(GridCache.Update);
                Task.WaitAll(task);
                if (task.Result <= 0) return;
                if (BlockLimiterConfig.Instance.BlockOwnershipTransfer)
                {
                    var num = Block.FixIds();
                    if (num > 0) Log.Warn($"Reviewed {num} block ownership");

                }
                ResetLimits(true,false,false);
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
                Log.Warn(e.StackTrace,"Session failed to load.  Check world for corruption");

            }
        }

        public static void ResetLimits(bool updateGrids = true, bool updatePlayers = true, bool updateFactions = true)
        {
            UpdateLimits.ResetLimits(updateGrids,updatePlayers,updateFactions);
        }
        /*
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
                {
                    Parallel.ForEach(grids, grid =>
                    {
                        UpdateLimits.Enqueue(grid.EntityId);
                    });
                }


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

                            var identity = Utilities.GetPlayerIdentityFromSteamId(player.SteamId);

                            if (string.IsNullOrEmpty(identity.DisplayName))
                                return;

                            if (identity.IdentityId == 0) return;

                            UpdateLimits.Enqueue(identity.IdentityId);
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

                        UpdateLimits.Enqueue(id);
                    });
                });
            }

        }


        */
        //The methods below are method used by other plugins to check limits from Blocklimiter
        #region External Access
        
        public static bool CheckLimits_future(MyObjectBuilder_CubeGrid[] grids, long id = 0)
        {
            return PluginApi.Limits.CheckLimits(grids, id);

        }

        public static bool CanAdd(List<MySlimBlock> blocks, long id, out List<MySlimBlock> nonAllowedBlocks)
        {

            return PluginApi.Limits.CanAdd(blocks, id, out nonAllowedBlocks);
        }
        
        #endregion

        
    }


    }

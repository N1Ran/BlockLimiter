using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Controls;
using BlockLimiter.ProcessHandlers;
using BlockLimiter.Punishment;
using BlockLimiter.Settings;
using BlockLimiter.Utility;
using NLog;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch.API.Plugins;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.Managers.PatchManager;
using Torch.API.Session;
using Torch.Session;
using Torch.Views;
using VRage.Game;
using VRage.Game.ModAPI;

namespace BlockLimiter
{
    public class BlockLimiter : TorchPluginBase, IWpfPlugin
    {
        private PatchManager _pm;
        private PatchContext _context;

        private readonly Logger _log = LogManager.GetLogger("BlockLimiter");
        private Thread _processThread;
        private List<Thread> _processThreads;
        private static bool _running;
        public static BlockLimiter Instance { get; private set; }
        private TorchSessionManager _sessionManager;
        private List<ProcessHandlerBase> _limitHandlers;
        public List<LimitItem> VanillaLimits = new List<LimitItem>();

        private void DoInit()
        {

            _limitHandlers = new List<ProcessHandlerBase>
            {
                new Player(),
                new Faction(),
                new ProcessHandlers.Grid(),
                new Annoy(),
                new Punish()
            };
            _processThreads = new List<Thread>();
            _processThread = new Thread(PluginProcessing);
            _processThread.Start();

            GetVanillaLimits();
            BlockLimiterConfig.Instance.UpdateLimits(BlockLimiterConfig.Instance.UseVanillaLimits);
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
                        BlockPairName = new List<string>{item.Key}
                    }));

                    break;
                case MyBlockLimitsEnabledEnum.PER_PLAYER:
                    limits.AddRange(MySession.Static.BlockTypeLimits.Select(item => new LimitItem
                    {
                        LimitFaction = false,
                        LimitPlayers = true,
                        LimitGrids = false,
                        Limit = item.Value,
                        BlockPairName = new List<string>{item.Key}
                    }));

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            VanillaLimits = limits;
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
                                        _log.Warn("Handler Problems: {0} - {1}", currentHandler.GetUpdateResolution(),
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
                    _log.Trace(ex);
            }
            catch (Exception ex)
            {
                if (BlockLimiterConfig.Instance.EnableLog) 
                    _log.Error(ex);
            }
        }

        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            _pm = torch.Managers.GetManager<PatchManager>();
            _context = _pm.AcquireContext();
            //Patch(_context);
            Instance = this;
            Load();
            _sessionManager = Torch.Managers.GetManager<TorchSessionManager>();
            if (_sessionManager != null)
                _sessionManager.SessionStateChanged += SessionChanged;
            
        }

        public override void Update()
        {
            base.Update();
            if (MyAPIGateway.Session == null)
                return;
            EntityCache.Update();
        }


        private  void SessionChanged(ITorchSession session, TorchSessionState state)
        {
            _running = state == TorchSessionState.Loaded;
            switch (state)
            {
                case TorchSessionState.Loaded:
                    DoInit();
                    EnableControl();
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

        public override void Dispose()
        {
            base.Dispose();
            _pm.FreeContext(_context);
            foreach (var thread in _processThreads)
                thread.Abort();
            _processThread.Abort();
        }

        
        public static bool CheckLimits_future(MyObjectBuilder_CubeGrid[] grids)
        {
            var hasLimit = false;
            
            foreach (var grid in grids)
            {
                foreach (var item in BlockLimiterConfig.Instance.LimitItems)
                {
                    if (grid == null)
                    {
                        continue;
                    }
                    
                    var isGridType = false;
                    switch (item.GridTypeBlock)
                    {
                        case LimitItem.GridType.SmallGridsOnly:
                            isGridType = grid.GridSizeEnum == MyCubeSize.Small;
                            break;
                        case LimitItem.GridType.LargeGridsOnly:
                            isGridType = grid.GridSizeEnum == MyCubeSize.Large;
                            break;
                        case LimitItem.GridType.StationsOnly:
                            isGridType = grid.IsStatic;
                            break;
                        case LimitItem.GridType.AllGrids:
                            isGridType = true;
                            break;
                        case LimitItem.GridType.ShipsOnly:
                            isGridType = !grid.IsStatic;
                            break;
                    }

                    if (!isGridType)
                    {
                        continue;
                    }
                    
                    var gridBlocks = grid.CubeBlocks;
                    var filteredBlocks = gridBlocks.Select(s=>MyDefinitionManager.Static.GetCubeBlockDefinition(s.GetId())).Where(def => Utilities.IsMatch(def, item)).ToList();

                    foreach (var block in gridBlocks)
                    {
                        if (item.FoundEntities.TryGetValue(block.BuiltBy, out var bCount))
                        {
                            if (bCount >= 0)
                            {
                                hasLimit = true;
                                break;
                            }
                        }
                        
                        if (item.FoundEntities.TryGetValue(block.Owner, out var oCount))
                        {
                            if (bCount >= 0)
                            {
                                hasLimit = true;
                                break;
                            }
                        }

                        var ownerFaction = MySession.Static.Factions.GetPlayerFaction(block.BuiltBy);

                        if (ownerFaction == null) continue;

                        if (!item.FoundEntities.ContainsKey(ownerFaction.FactionId)) continue;

                        hasLimit = true;
                        break;

                    }
                    
                    if (!filteredBlocks.Any() || filteredBlocks.Count() < item.Limit)
                    {
                        continue;
                    }

                    hasLimit = true;
                    break;

                }
            }

            return hasLimit;
        }

       private static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(typeof(MySlimBlock).GetMethod(nameof(MySlimBlock.TransferAuthorship))).Prefixes.
                Add(typeof(BlockLimiter).GetMethod(nameof(OnTransfer), BindingFlags.Static | BindingFlags.NonPublic));
        }

        public static event Action<MySlimBlock, long> SlimOwnerChanged;

        // ReSharper disable once InconsistentNaming
        private static bool OnTransfer(MySlimBlock __instance, long newOwner)
        {
            SlimOwnerChanged?.Invoke(__instance, newOwner);
            return true; // false cancels.
        }
        
        
    }


    }

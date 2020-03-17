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
using Torch.Managers.PatchManager;
using Torch.API.Session;
using Torch.Session;
using Torch.Views;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.VisualScripting;
using VRage.Network;
using VRage.Profiler;
using Grid = BlockLimiter.Utility.Grid;

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
        public HashSet<LimitItem> VanillaLimits = new HashSet<LimitItem>();
        
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
            
            MyCubeGrids.BlockDestroyed += MyCubeGridsOnBlockDestroyed;
            MyCubeGrid.OnSplitGridCreated += MyCubeGridOnOnSplitGridCreated;
            
            MyMultiplayer.Static.ClientJoined += StaticOnClientJoined;
            
        }

        private void MyCubeGridOnOnSplitGridCreated(MyCubeGrid obj)
        {
            if (obj == null) return;
            Grid.UpdateLimit(obj);
        }

        private void StaticOnClientJoined(ulong obj)
        {
            var player = MySession.Static.Players.TryGetPlayerBySteamId(obj);
            if (player == null)return;
            Block.UpdatePlayerLimits(player);
        }

        private void MyCubeGridsOnBlockDestroyed(MyCubeGrid arg1, MySlimBlock arg2)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits)return;
            Grid.UpdateLimit(arg1);
            foreach (var owner in arg1.BigOwners)
            {
                if (!MySession.Static.Players.TryGetPlayerId(owner, out var playerId)) continue;
                var player = MySession.Static.Players.GetPlayerById(playerId);
                if (player == null) continue;
                Task.Run(() =>
                {
                    Thread.Sleep(100);
                    MySandboxGame.Static.Invoke(() =>
                    {
                        Grid.UpdateLimit(arg1);
                    }, "BlockLimiter");
                });

            }
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
            Instance = this;
            Patch(_context);
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
            if (++_updateCounter % 100 == 0)
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

        public override void Dispose()
        {
            base.Dispose();
            _pm.FreeContext(_context);
            foreach (var thread in _processThreads)
                thread.Abort();
            _processThread.Abort();
        }

        public static void ResetLimits()
        {
            var grids = new HashSet<MyCubeGrid>();
           GridCache.GetGrids(grids);
           
           Task.Run(() =>
           {
               Thread.Sleep(300);
               MySandboxGame.Static.Invoke(() =>
               {
                   foreach (var grid in grids)
                   {
                       if (grid == null) continue;
                       Grid.UpdateLimit(grid);
                   }
               }, "BlockLimiter");
           });
           
           Task.Run(() =>
           {
               Thread.Sleep(100);
               MySandboxGame.Static.Invoke(() =>
               {
                   foreach (var playerId in MySession.Static.Players.GetAllPlayers())
                   {
                       if (!playerId.IsValid) continue;
                       var player = MySession.Static.Players.GetPlayerById(playerId);
                       if (player == null) continue;
                       Block.UpdatePlayerLimits(player);
                   }
               }, "BlockLimiter");
           });
           
           Task.Run(() =>
           {
               Thread.Sleep(500);
               MySandboxGame.Static.Invoke(() =>
               {

                   foreach (var id in MySession.Static.Factions.Factions.Keys)
                   {
                       Block.UpdateFactionLimits(id);
                       
                   }
               }, "BlockLimiter");
           });


        }

        
        public static bool CheckLimits_future(MyObjectBuilder_CubeGrid[] grids)
        {

            if (!BlockLimiterConfig.Instance.EnableLimits)
            {
                return false;
            }

            return !grids.Any(Grid.IsSizeViolation) && 
                   !grids.Any(z=>z.CubeBlocks.Any(b=>Block.AllowBlock(MyDefinitionManager.Static.GetCubeBlockDefinition(b),0,z)));
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
            if (!BlockLimiterConfig.Instance.EnableLimits || !BlockLimiterConfig.Instance.BlockOwnershipTransfer)
            {
                SlimOwnerChanged?.Invoke(__instance, newOwner);
                return true;
            }
            
            if (!Block.AllowBlock(__instance.BlockDefinition, newOwner, __instance.CubeGrid))
            {
                Utilities.ValidationFailed();
                return false;
            }
            
            Block.Add(__instance.BlockDefinition,newOwner);
            SlimOwnerChanged?.Invoke(__instance, newOwner);
            return true;
        }
        
    }


    }

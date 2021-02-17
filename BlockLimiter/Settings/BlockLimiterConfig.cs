using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Xml.Serialization;
using BlockLimiter.Utility;
using Torch;
using Torch.Views;
using NLog;
using Sandbox.ModAPI;

namespace BlockLimiter.Settings
{
    [Serializable]
    public class BlockLimiterConfig : ViewModel

    {
        private bool _enable;
        private static BlockLimiterConfig _instance;
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private XmlAttributeOverrides _overrides;
        
        [XmlIgnore]
        public HashSet<LimitItem> AllLimits = new HashSet<LimitItem>();
        public BlockLimiterConfig()
        {
            _limitItems = new MtObservableCollection<LimitItem>();
            _limitItems.CollectionChanged += ItemsCollectionChanged;

        }


        public static BlockLimiterConfig Instance => _instance ?? (_instance = new BlockLimiterConfig());

        [Display(EditorType = typeof(EmbeddedCollectionEditor))]
        public MtObservableCollection<LimitItem> LimitItems
        {
            get => _limitItems;
            set
            {
                _limitItems = value;
                OnPropertyChanged();
                Save();
            }
        }


        private bool _loading;
        private bool _vanillaLimits;
        private bool _annoy;
        private int _annoyInterval = 300;
        private int _annoyDuration = 15000;
        private string _serverName = BlockLimiter.ChatName;
        private string _annoyMsg = "You're in violation of set limits.  Use [!blocklimit mylimit] to view which limits you've exceeded";
        private int _punishInterval = 900;
        private int _maxBlockSizeShips = 0;
        private int _maxBlockSizeStations = 0;
        private int _maxBlocksSmallGrid = 0;
        private int _maxBlocksLargeGrid = 0;
        private bool _gridConvertBlocking;
        private bool _gridSpawnBlocking;
        private bool _blockOwnershipTransfer;
        private MtObservableCollection<LimitItem> _limitItems;
        private string _denyMessage = "Limit reached \n{BC} blocks denied \nBlockNames: \n{BL}";
        private string _projectionDenyMessage = "{BC} blocks removed from Projection. \nBlockNames = \n{BL}";
        private bool _mergerBlocking;
        private List<string> _generalException;
        private bool _countProjection;
        private bool _killNoOwnerBlocks;
        private string _logFileName = "BlockLimiter-${shortdate}.log";
        private int _maxBlockSizeProjections;
        private int _maxSmallGrids;
        private int _maxLargeGrids;


        #region General BlockCount Limit

        [Display(Order = 1, Name = "SmallGrids", GroupName = "General Count Limit", Description = "Max size for small grids")]
        public int MaxBlocksSmallGrid
        {
            get => _maxBlocksSmallGrid;
            set
            {
                _maxBlocksSmallGrid = value; 
                Changed();
            }
        }

        [Display(Order = 2, Name = "LargeGrids", GroupName = "General Count Limit", Description = "Max size for large grids")]
        public int MaxBlocksLargeGrid
        {
            get => _maxBlocksLargeGrid;
            set
            {
                _maxBlocksLargeGrid = value; 
                Changed();
            }
        }

        [Display(Order = 3, Name = "Ships", GroupName = "General Count Limit", Description = "Max size for moving grids")]
        public int MaxBlockSizeShips
        {
            get => _maxBlockSizeShips;
            set
            {
                _maxBlockSizeShips = value; 
                Changed();
            }
        }

        [Display(Order = 4, Name = "Stations", GroupName = "General Count Limit", Description = "Max size for static grids")]
        public int MaxBlockSizeStations
        {
            get => _maxBlockSizeStations;
            set
            {
                _maxBlockSizeStations = value; 
                Changed();
            }
        }

        [Display(Order = 5, Name = "Projection", GroupName = "General Count Limit", Description = "Max size for projections")]
        public int MaxBlockSizeProjections
        {
            get => _maxBlockSizeProjections;
            set
            {
                _maxBlockSizeProjections = value; 
                Changed();
            }
        }

        [Display(Order = 6, Name = "SmallGrids Allowed", GroupName = "General Count Limit",
            Description = "Max allowed small grid per player")]
        public int MaxSmallGrids
        {
            get => _maxSmallGrids;
            set
            {
                _maxSmallGrids = value;
                Changed();
            }
        }

        [Display(Order = 7, Name = "LargeGrids Allowed", GroupName = "General Count Limit",
            Description = "Max allowed large grid per player")]
        public int MaxLargeGrids
        {
            get => _maxLargeGrids;
            set
            {
                _maxLargeGrids = value;
                Changed();
            }
        }


        #endregion

        #region Main Settings

        [Display(Order = 1, GroupName = "*Main Settings", Name = "Enable")]
        public bool EnableLimits
        {
            get => _enable;
            set
            {
                _enable = value;
                Changed();
                if (value && MyAPIGateway.Session != null) BlockLimiter.Activate();
            }
        }

        [Display(Order = 2, GroupName = "*Main Settings", Name = "Server Name", Description = "Name used by the plugin when sending info to players")]
        public string ServerName
        {
            get => _serverName;
            set
            {
                _serverName = string.IsNullOrEmpty(value) ? BlockLimiter.ChatName : value; 
                Changed();
            }
        }

        [Display(Order = 3, GroupName = "*Main Settings", Name = "Deny Message", Description = "Message posted when limit is reached.  {BL} to list names of denied blocks. {BC} to give count of affected blocks")]
        public string DenyMessage
        {
            get => _denyMessage;
            set
            {
                _denyMessage = value; 
                Changed();
            }
        }

        [Display(Order = 4, GroupName = "*Main Settings", Name = "Projection Deny Message", Description = "Message posted when blocks are removed from projection.  {BL} to list names of denied blocks. {BC} to give count of affected blocks")]
        public string ProjectionDenyMessage
        {
            get => _projectionDenyMessage;
            set
            {
                _projectionDenyMessage = value; 
                Changed();
            }
        }


        
        [Display(Order = 5, GroupName = "*Main Settings", Name = "Use Vanilla Limits", Description = "This will add vanilla block limits to limiter's checks")]
        public bool UseVanillaLimits
        {
            get => _vanillaLimits;
            set
            {
                _vanillaLimits = value;
                AllLimits = Utilities.UpdateLimits(_vanillaLimits);
                Changed();
            }
        }
        

        [Display(Order = 7, GroupName = "*Main Settings", Name = "ShutOff UnOwned Blocks", Description = "Turns off any blocks that becomes UnOwned except power blocks, button panels and parachutes")]
        public bool KillNoOwnerBlocks
        {
            get => _killNoOwnerBlocks;
            set
            {
                _killNoOwnerBlocks = value;
                Changed();
            }
        }


        [Display(Order = 8, GroupName = "*Main Settings", Name = "Log File Name", Description = "Log file is saved under provided name. Leave empty to log into default Torch log file")]
        public string LogFileName
        {
            get => _logFileName;
            set
            {
                _logFileName = value;
                Changed();
            }
        }

        [Display(Order = 9, GroupName = "*Main Settings", Name = "Exception", Description = "Any player, grid or faction listed will be ignored by the plugin.")]
        public List<string> GeneralException
        {
            get => _generalException;
            set
            {
                _generalException = value;
                Changed();
            }
        }
        #endregion

        #region Blocking Functions
        [Display(Order = 1, GroupName = "Blocking Options", Name =  "Enable Grid Spawn Blocking", Description = "Will block entire grid from spawning or remove potential violation blocks before spawn")]
        public bool EnableGridSpawnBlocking
        {
            get => _gridSpawnBlocking;
            set
            {
                _gridSpawnBlocking = value; 
                Changed();
            }
        }

        [Display(Order = 2, GroupName = "Blocking Options", Name =  "Enable Grid Convert Blocking", Description = "Will block grid conversion if grid will violate limits upon conversion")]
        public bool EnableConvertBlock
        {
            get => _gridConvertBlocking;
            set
            {
                _gridConvertBlocking = value; 
                Changed();
            }
        }
        
        [Display(Order = 3, GroupName = "Blocking Options", Name =  "Enable Ownership Transfer Blocking", Description = "Will block ownership if player exceeds limit of block being transferred to them")]
        public bool BlockOwnershipTransfer
        {
            get => _blockOwnershipTransfer;
            set
            {
                _blockOwnershipTransfer = value;
                Changed();
            }
        }

                
        [Display(Order = 4, GroupName = "Blocking Options", Name = "Merger Blocking", Description = "Enables checking merge attempts with limits")]
        public bool MergerBlocking
        {
            get => _mergerBlocking;
            set
            {
                _mergerBlocking = value;
                Changed();
            }
        }

        

        #endregion

        #region Punishment

        [Display(Order = 1, Name = "Enable Annoyance Message", GroupName = "Punishment")]
        public bool Annoy
        {
            get => _annoy;
            set
            {
                _annoy = value; 
                Changed();
            }
        }

        [Display(Order = 5,Name = "Punishment Interval (s)", GroupName = "Punishment", Description = "How often the punishment is triggered.")]
        public int PunishInterval
        {
            get => _punishInterval;
            set
            {
                _punishInterval = value; 
                Changed();
            }
        }

        [Display(Order = 2, Name = "Annoyance Message", GroupName = "Punishment")]
        public string AnnoyMessage
        {
            get => _annoyMsg;
            set
            {
                _annoyMsg = value; 
                Changed();
            }
        }

        [Display(Order = 3,Name = "Annoy Message Interval (s)", GroupName = "Punishment", Description = "How often annoyance message is triggered in seconds")]
        public int AnnoyInterval
        {
            get => _annoyInterval;
            set
            {
                _annoyInterval = value; 
                Changed();
            }
        }

        [Display(Order = 4, Name = "Annoy Duration (ms)", GroupName = "Punishment", Description = "How long annoying message stays on the screen in ms")]
        public int AnnoyDuration
        {
            get => _annoyDuration;
            set
            {
                _annoyDuration = value; 
                Changed();
            }
        }

        #endregion


        #region Loading and Saving

        private void Changed(bool updated = true)
        {
            OnPropertyChanged();
            if (updated)Instance.Save(); 
        }

        /// <summary>
        ///     Loads our settings
        /// </summary>
        public void Load()
        {
            _loading = true;

            try
            {
                lock (this)
                {
                    var fileName = Path.Combine(BlockLimiter.Instance.StoragePath, "BlockLimiter.cfg");
                    if (File.Exists(fileName))
                    {
                        using (var reader = new StreamReader(fileName))
                        {
                            var x = _overrides != null ? new XmlSerializer(typeof(BlockLimiterConfig), _overrides) : new XmlSerializer(typeof(BlockLimiterConfig));
                            var settings = (BlockLimiterConfig)x.Deserialize(reader);
                            
                            reader.Close();
                            if(settings != null)_instance = settings;
                        }
                    }
                    else
                    {
                        Log.Info("No settings. Initialzing new file at " + fileName);
                        _instance = new BlockLimiterConfig();
                        _instance.LimitItems.Add(new LimitItem());
                        using (var writer = new StreamWriter(fileName))
                        {
                            var x = _overrides != null ? new XmlSerializer(typeof(BlockLimiterConfig), _overrides) : new XmlSerializer(typeof(BlockLimiterConfig));
                            x.Serialize(writer, _instance);
                            writer.Close();
                        }
                    }
                    LoggingConfig.Set();
                }
            }
            catch (Exception ex)
            {
                lock (this)
                {
                    Log.Error(ex);
                }
            }
            finally
            {
                _loading = false;
            }
        }
        
        /// <summary>
        ///     Saves our settings
        /// </summary>
        public void Save()
        {

            if (_loading)
                return;

            try
            {
                lock (this)
                {
                    var fileName = Path.Combine(BlockLimiter.Instance.StoragePath, "BlockLimiter.cfg");
                    using (var writer = new StreamWriter(fileName))
                    {
                        XmlSerializer x;
                        if (_overrides != null)
                            x = new XmlSerializer(typeof(BlockLimiterConfig), _overrides);
                        else
                            x = new XmlSerializer(typeof(BlockLimiterConfig));
                        x.Serialize(writer, _instance);
                        writer.Close();
                    }
                    LoggingConfig.Set();

                }
            }
            catch (Exception e)
            {
                lock (this)
                {
                    Log.Error(e);
                }
            }
        }

#endregion

        #region Events


        /// <summary>
        ///     Triggered when items changes.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ItemsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged();
            AllLimits = Utilities.UpdateLimits(UseVanillaLimits);
            Instance.Save(); 
        }


        #endregion


    }
}

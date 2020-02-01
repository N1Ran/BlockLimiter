using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using System.Xml;
using System.Xml.Serialization;
using BlockLimiter.Utility;
using Torch;
using Torch.Views;
using NLog;
using VRage.Collections;

namespace BlockLimiter.Settings
{
    [Serializable]
    public class BlockLimiterConfig : ViewModel

    {
        private bool _enable;
        private static BlockLimiterConfig _instance;
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private XmlAttributeOverrides _overrides;
        public HashSet<LimitItem> AllLimits = new HashSet<LimitItem>();
        public BlockLimiterConfig()
        {
            LimitItems = new MtObservableCollection<LimitItem>();
            LimitItems.CollectionChanged += ItemsCollectionChanged;

        }

        public static BlockLimiterConfig Instance => _instance ?? (_instance = new BlockLimiterConfig());

        [Display(EditorType = typeof(EmbeddedCollectionEditor))]
        public MtObservableCollection<LimitItem> LimitItems { get; set; }

        private bool _loading;
        private bool _vanillaLimits;
        private bool _annoy;
        private int _annoyInterval = 600;
        private int _annoyDuration = 15000;
        private string _serverName = "BlockLimiter";
        private string _annoyMsg = "You're in violation of set limits.  Use [!blocklimit mylimit] to view which limits you've exceeded";
        private int _punishInterval = 700;
        private int _maxBlockSizeShips = 0;
        private int _maxBlockSizeStations = 0;
        private int _maxBlocksSmallGrid = 0;
        private int _maxBlocksLargeGrid = 0;
        private bool _enableLog;
        private MyConcurrentHashSet<long> _disabledEntities = new MyConcurrentHashSet<long>();


        public string ServerName
        {
            get => _serverName;
            set
            {
                _serverName = value;
                OnPropertyChanged();
                Instance.Save();
            }
        }

        public bool EnableLimits
        {
            get => _enable;
            set
            {
                _enable = value;
                OnPropertyChanged();
                Instance.Save();
            }
        }

        [Display(Name = "Ships", GroupName = "General BlockCount Limit", Description = "Max size for moving grids")]
        public int MaxBlockSizeShips
        {
            get => _maxBlockSizeShips;
            set
            {
                _maxBlockSizeShips = value;
                OnPropertyChanged();
                Instance.Save();
            }
        }

        [Display(Name = "Stations", GroupName = "General BlockCount Limit", Description = "Max size for static grids")]
        public int MaxBlockSizeStations
        {
            get => _maxBlockSizeStations;
            set
            {
                _maxBlockSizeStations = value;
                OnPropertyChanged();
                Instance.Save();
            }
        }

        [Display(Name = "LargeGrids", GroupName = "General BlockCount Limit", Description = "Max size for large grids")]
        public int MaxBlocksLargeGrid
        {
            get => _maxBlocksLargeGrid;
            set
            {
                _maxBlocksLargeGrid = value;
                OnPropertyChanged();
                Instance.Save();
            }
        }
        
        [Display(Name = "SmallGrids", GroupName = "General BlockCount Limit", Description = "Max size for small grids")]
        public int MaxBlocksSmallGrid
        {
            get => _maxBlocksSmallGrid;
            set
            {
                _maxBlocksSmallGrid = value;
                OnPropertyChanged();
                Instance.Save();
            }
        }

        [XmlIgnore]
        [Display(Visible = false)]
        public MyConcurrentHashSet<long> DisabledEntities => _disabledEntities;
        
        
        [Display(Name = "Use Vanilla Limits", Description = "This will add vanilla block limits to limiter's checks")]
        public bool UseVanillaLimits
        {
            get => _vanillaLimits;
            set
            {
                _vanillaLimits = value;
                Instance.UpdateLimits(_vanillaLimits);
                OnPropertyChanged();
                Instance.Save();
            }
        }

        [Display(Name = "Enable Logs", Description = "Logs are only advice to check for issues with the limiter")]
        public bool EnableLog
        {
            get => _enableLog;
            set
            {
                _enableLog = value;
                OnPropertyChanged();
                Instance.Save();
            }
        }

        [Display(Name = "Enable Annoyance Message", GroupName = "Punishment")]
        public bool Annoy
        {
            get => _annoy;
            set
            {
                _annoy = value;
                OnPropertyChanged();
                Instance.Save();
            }
        }

        [Display(Name = "Punishment Interval (ms)", GroupName = "Punishment", Description = "How often the punishment is triggered.")]
        public int PunishInterval
        {
            get => _punishInterval;
            set
            {
                _punishInterval = value;
                OnPropertyChanged();
                Save();
            }
        }

        [Display(Name = "Annoyance Message", GroupName = "Punishment")]
        public string AnnoyMessage
        {
            get => _annoyMsg;
            set
            {
                _annoyMsg = value;
                OnPropertyChanged();
                Instance.Save();
            }
        }

        [Display(Name = "Annoy Message Interval (s)", GroupName = "Punishment", Description = "How often annoyance message is triggered in seconds")]
        public int AnnoyInterval
        {
            get => _annoyInterval;
            set
            {
                _annoyInterval = value;
                OnPropertyChanged();
                Instance.Save();
            }
        }

        [Display(Name = "Annoy Duration (ms)", GroupName = "Punishment", Description = "How long annoying message stays on the screen in ms")]
        public int AnnoyDuration
        {
            get => _annoyDuration;
            set
            {
                _annoyDuration = value;
                OnPropertyChanged();
                Instance.Save();
            }

        }


        #region Loading and Saving

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
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            finally
            {
                _loading = false;
            }
        }
        
        public void UpdateLimits(bool useVanilla)
        {
            AllLimits.Clear();
            if (useVanilla && BlockLimiter.Instance.VanillaLimits.Any())
            {
                AllLimits.UnionWith(BlockLimiter.Instance.VanillaLimits);
            }

            AllLimits.UnionWith(Instance.LimitItems);
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
                    Log.Info($"Saved");

                }

            }
            catch (Exception ex)
            {
                Log.Warn("Configuration failed to save");
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
    UpdateLimits(UseVanillaLimits);
    Instance.Save(); 
}


#endregion


    }
}

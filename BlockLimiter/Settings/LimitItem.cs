using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using Torch;
using Torch.Views;
using System.Xml.Serialization;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage;
using VRage.Collections;
using VRage.Dedicated.Configurator;
using VRage.Game;
using VRage.Profiler;

namespace BlockLimiter.Settings
{
    [Serializable]

    public class LimitItem : ViewModel
    {

        private bool _limitFaction;
        private bool _limitGrids;
        private bool _limitPlayer;
        private PunishmentType _punishType = PunishmentType.None;
        private GridType _gridType = GridType.AllGrids;
        private string _name;
        private List<string> _blockList = new List<string>();
        private List<string> _exceptions = new List<string>();
        private int _limit;
        private bool _restrictProjection;
        private bool _ignoreNpc;


        public LimitItem()
        {
            CollectionChanged += OnCollectionChanged;
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged();
            //BlockLimiter.ResetLimits();
            if (MyAPIGateway.Session != null)Reset();
            Save();
        }


        [XmlIgnore]
        [Display(Visible = false)]
        public ConcurrentDictionary<long,int> FoundEntities { get; } = new ConcurrentDictionary<long, int>();


        [Display(Order = 1, Name = "Name", Description = "Name of the limit. This helps with some of the commands")]
        public string Name
        {
            get => _name;
            set
            {
                _name = string.IsNullOrEmpty(value) ? _blockList.FirstOrDefault():value;
                OnPropertyChanged();
            }
        }

        [Display(Name = "Blocks", Description = "Block typeid, subtypeId and/or pair names from cubeblocks.sbc can be use here")]
        public List<string> BlockList
        {
            get => _blockList;
            set
            {
                _blockList =value;
                OnPropertyChanged();
            }
        }

        

        [Display(Name = "Exceptions", Description = "List of player or grid exception. You can also use entityId.")]
        public List<string> Exceptions
        {
            get => _exceptions;
            set
            {
                _exceptions = value;
                OnPropertyChanged();
            }
        }

        [Display(Name = "Limit", Description = "Limit value")]
        public int Limit
        {
            get => _limit;
            set
            {
                _limit = value;
                OnPropertyChanged();
            }
        }

        [Display(Name = "Limit Faction", GroupName = "Limits", Description = "Applies Limit to Factions")]
        public bool LimitFaction
        {
            get => _limitFaction;
            set
            {
                _limitFaction = value;
                OnPropertyChanged();
            }
        }

        [Display(Name = "GridType Limit", GroupName = "Limits",
            Description = "This is choose which grid type to block placement")]
        public GridType GridTypeBlock
        {
            get => _gridType;
            set
            {
                _gridType = value;
                OnPropertyChanged();
            }
        }
        
        [Display(Name = "Limit Grids", GroupName = "Limits", Description = "Applies Limit to Grids")]
        public bool LimitGrids
        {
            get => _limitGrids;
            set
            {
                _limitGrids = value;
                OnPropertyChanged();
            }
        }
        [Display(Name = "Limit Players", GroupName = "Limits", Description = "Applies Limit to Players")]
        public bool LimitPlayers
        {
            get => _limitPlayer;
            set
            {
                _limitPlayer = value;
                OnPropertyChanged();
            }
        }

        
        [Display(Name = "PunishmentType", GroupName = "Restrictions", Description = "Set's what to do to extra blocks in violation of the limit")]
        public PunishmentType Punishment
        {
            get => _punishType;
            set
            {
                _punishType = value;
                OnPropertyChanged();
                Save();
            }
        }

        [Display(Name = "IgnoreNPCs", GroupName = "Restrictions", Description = "Will ignore NPC owned grids")]
        public bool IgnoreNpcs
        {
            get => _ignoreNpc;
            set
            {
                _ignoreNpc = value;
                OnPropertyChanged();
            }
        }
        
        
        
        [Display(Name = "Restrict Projection", GroupName = "Restrictions",
            Description = "Removes block from projection once limit reached.")]
        public bool RestrictProjection
        {
            get => _restrictProjection;
            set
            {
                _restrictProjection = value;
                OnPropertyChanged();
            }
        }

        public bool IsMatch(MyCubeBlockDefinition definition)
        {
            if (!BlockList.Any() || definition == null) return false;

            if (GridTypeBlock != GridType.AllGrids)
            {
                switch (definition.CubeSize)
                {
                    case MyCubeSize.Small when (GridTypeBlock == GridType.LargeGridsOnly || GridTypeBlock == GridType.StationsOnly):
                    case MyCubeSize.Large when (GridTypeBlock == GridType.SmallGridsOnly):
                        return false;
                }
            }
            return BlockList.Any(x =>
                x.Equals(definition.ToString().Substring(16), StringComparison.OrdinalIgnoreCase) ||
                   x.Equals(definition.Id.SubtypeId.ToString(), StringComparison.OrdinalIgnoreCase) ||
                   x.Equals(definition.BlockPairName, StringComparison.OrdinalIgnoreCase) ||
                x.Equals(definition.Id.TypeId.ToString().Substring(16), StringComparison.OrdinalIgnoreCase));
        }

        public bool IsGridType(MyCubeGrid grid)
        {
            switch (GridTypeBlock)
            {
                case GridType.SmallGridsOnly:
                    return grid.GridSizeEnum == MyCubeSize.Small;
                case GridType.LargeGridsOnly:
                    return grid.GridSizeEnum == MyCubeSize.Large;
                case GridType.StationsOnly:
                    return grid.IsStatic;
                case GridType.ShipsOnly:
                    return !grid.IsStatic;
                case GridType.AllGrids:
                    return true;
                default:
                    return false;
            }
        }

        public bool IsGridType(MyObjectBuilder_CubeGrid grid)
        {
            switch (GridTypeBlock)
            {
                case GridType.AllGrids:
                    return true;
                case GridType.SmallGridsOnly:
                    return grid.GridSizeEnum == MyCubeSize.Small;
                case GridType.LargeGridsOnly:
                    return grid.GridSizeEnum == MyCubeSize.Large;
                case GridType.StationsOnly:
                    return grid.IsStatic;
                case GridType.ShipsOnly:
                    return !grid.IsStatic;
                default:
                    return false;
            }
        }


        public void Reset()
        {
            FoundEntities.Clear();
        }
        
        public override string ToString()
        {
            var useName = string.IsNullOrEmpty(Name) ? BlockList.FirstOrDefault() : Name;
            return $"{useName} - [{BlockList.Count} : {Limit}]";
        }
        


        private void Save()
        {
            Reset();
            BlockLimiterConfig.Instance.Save();
        }
        
        public enum PunishmentType
        {
            None,
            DeleteBlock,
            ShutOffBlock,
            Explode
        }
        
        public enum GridType
        {
            AllGrids,
            SmallGridsOnly,
            LargeGridsOnly,
            StationsOnly,
            ShipsOnly
        }
    }
}
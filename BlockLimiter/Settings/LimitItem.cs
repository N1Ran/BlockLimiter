﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Torch;
using Torch.Views;
using System.Xml.Serialization;
using BlockLimiter.PluginApi;
using BlockLimiter.Utility;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage.Game;

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
        private FilterType _filterType;
        private FilterOperator _limitOperator;
        private int _filterValue;


        public LimitItem()
        {
            CollectionChanged += OnCollectionChanged;
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged();
            if (MyAPIGateway.Session != null)Reset();
            Save();
        }


        [XmlIgnore]
        [Display(Visible = false)]
        public ConcurrentDictionary<long,int> FoundEntities { get; } = new ConcurrentDictionary<long, int>();


        [Display(GroupName =  "Description", Order = 1, Name = "Name", Description = "Name of the limit. This helps with some of the commands")]
        public string Name
        {
            get => _name;
            set
            {
                _name = string.IsNullOrEmpty(value) ? _blockList.FirstOrDefault():value;
                OnPropertyChanged();
            }
        }

        [Display(GroupName = "Description", Order = 2, Name = "Blocks", Description = "Block typeid, subtypeId and/or pair names from cubeblocks.sbc can be use here")]
        public List<string> BlockList
        {
            get => _blockList;
            set
            {
                _blockList =value;
                OnPropertyChanged();
            }
        }

        

        [Display(GroupName = "Description",Order = 4, Name = "Exceptions", Description = "List of player or grid exception. You can also use entityId.")]
        public List<string> Exceptions
        {
            get => _exceptions;
            set
            {
                _exceptions = value;
                OnPropertyChanged();
            }
        }

        [Display(GroupName = "Description", Order =  3,Name = "Limit", Description = "Limit value")]
        public int Limit
        {
            get => _limit;
            set
            {
                _limit = value;
                OnPropertyChanged();
            }
        }

        #region Options

        [Display(Name = "Limit Faction", Order = 2, GroupName = "Options", Description = "Applies Limit to Factions")]
        public bool LimitFaction
        {
            get => _limitFaction;
            set
            {
                _limitFaction = value;
                OnPropertyChanged();
            }
        }

        [Display(Name = "GridType Limit", GroupName = "Options", Order =  4,
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

        [Display(Name = "Limit Grids", Order = 3, GroupName = "Options", Description = "Applies Limit to Grids")]
        public bool LimitGrids
        {
            get => _limitGrids;
            set
            {
                _limitGrids = value;
                OnPropertyChanged();
            }
        }
        [Display(Name = "Limit Players", Order = 1, GroupName = "Options", Description = "Applies Limit to Players")]
        public bool LimitPlayers
        {
            get => _limitPlayer;
            set
            {
                _limitPlayer = value;
                OnPropertyChanged();
            }
        }
        #endregion
       
        #region Restrictions

        [Display(Name = "PunishmentType", Order = 3, GroupName = "Restrictions", Description = "Set's what to do to extra blocks in violation of the limit")]
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

        [Display(Name = "IgnoreNPCs", Order = 1, GroupName = "Restrictions", Description = "Will ignore NPC owned grids")]
        public bool IgnoreNpcs
        {
            get => _ignoreNpc;
            set
            {
                _ignoreNpc = value;
                OnPropertyChanged();
            }
        }
        
        
        
        [Display(Name = "Restrict Projection", GroupName = "Restrictions", Order = 2,
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
        #endregion

        #region Filter

        [Display(Name = "Filter Type", GroupName = "Filter", Description = "Filters limit base on what is set")]
        public FilterType LimitFilterType
        {
            get => _filterType;
            set
            {
                _filterType = value;
                OnPropertyChanged();
            }
        }

        [Display(Name = "Filter Operator", GroupName = "Filter", Description = "Filters limit base on what is set")]
        public FilterOperator LimitFilterOperator
        {
            get => _limitOperator;
            set
            {
                _limitOperator = value;
                OnPropertyChanged();
            }
        }

        [Display(Name = "Filter Value", GroupName = "Filter", Description = "Filters limit base on what is set")]
        public int FilterValue
        {
            get => _filterValue;
            set
            {
                _filterValue = value;
                OnPropertyChanged();
            }
        }
        #endregion

        #region Utilities

        internal bool IsExcepted(object target)
        {
            if (target == null) return false;
            var allExceptions = new HashSet<string>(Exceptions);
            allExceptions.UnionWith(BlockLimiterConfig.Instance.GeneralException);

            if (allExceptions.Count == 0) return false;

            MyIdentity identity = null;
            MyFaction faction = null;
            long identityId = 0;
            ulong playerSteamId = 0;
            string displayName = "";
            HashSet<long> gridOwners = new HashSet<long>();

            switch (target)
            {
                case HashSet<long> owners:
                    gridOwners.UnionWith(owners);
                    break;
                case ulong steamId:
                    if (steamId == 0) return false;
                    playerSteamId = steamId;
                    identityId = Utilities.GetPlayerIdFromSteamId(steamId);
                    identity = MySession.Static.Players.TryGetIdentity(identityId);
                    displayName = identity.DisplayName;
                    faction = MySession.Static.Factions.GetPlayerFaction(identityId);
                    break;
                case string name:
                    if (allExceptions.Contains(name)) return true;
                    if (Utilities.TryGetPlayerByNameOrId(name, out identity))
                    {
                        identityId = identity.IdentityId;
                        faction = MySession.Static.Factions.GetPlayerFaction(identityId);
                        displayName = identity.DisplayName;
                        playerSteamId = Utilities.GetSteamIdFromPlayerId(identityId);
                    }
                    break;
                case long id:
                    if (id == 0) return false;
                    if (allExceptions.Contains(id.ToString())) return true;
                    if (GridCache.TryGetGridById(id, out var foundGrid))
                    {
                        if (allExceptions.Contains(foundGrid.DisplayName)) return true;
                        var owners = GridCache.GetOwners(foundGrid);
                        owners.UnionWith(GridCache.GetBuilders(foundGrid));
                        if (owners.Count == 0) break;
                        gridOwners.UnionWith(owners);
                        break;
                    }
                    identityId = id;
                    identity = MySession.Static.Players.TryGetIdentity(id);
                    if (identity != null)
                    {
                        faction = MySession.Static.Factions.GetPlayerFaction(id);
                        displayName = identity.DisplayName;
                        playerSteamId = Utilities.GetSteamIdFromPlayerId(id);

                    }
                    else
                    {
                        faction = (MyFaction) MySession.Static.Factions.TryGetFactionById(id);
                    }

                    break;
                case MyFaction targetFaction:
                    if (allExceptions.Contains(targetFaction.Tag) ||
                        allExceptions.Contains(targetFaction.FactionId.ToString()))
                        return true;
                    break;
                case MyPlayer player:
                    if (player.IsBot || player.IsWildlifeAgent) return true;
                    var playerIdentity = player.Identity;
                    if (playerIdentity == null) return false;
                    if (playerIdentity.IdentityId > 0)
                    {
                        if (allExceptions.Contains(playerIdentity.IdentityId.ToString())) return true;
                        displayName = playerIdentity.DisplayName;
                        identityId = playerIdentity.IdentityId;
                    }

                    playerSteamId = Utilities.GetSteamIdFromPlayerId(playerIdentity.IdentityId);
                    break;
                case MyCubeGrid grid:
                {
                    if (allExceptions.Contains(grid.DisplayName) || allExceptions.Contains(grid.EntityId.ToString()))
                        return true;
                    var owners = new HashSet<long>(GridCache.GetOwners(grid));
                    owners.UnionWith(GridCache.GetBuilders(grid));
                    if (owners.Count == 0) break;
                    gridOwners.UnionWith(owners);
                    break;
                }
            }

            foreach (var owner in gridOwners)
            {
                if (owner == 0) continue;
                if (allExceptions.Contains(owner.ToString())) return true;
                identity = MySession.Static.Players.TryGetIdentity(owner);
                playerSteamId = Utilities.GetSteamIdFromPlayerId(owner);
                if (playerSteamId > 0 && allExceptions.Contains(playerSteamId.ToString())) return true;
                if (identity != null)
                {
                    if (allExceptions.Contains(identity.DisplayName)) return true;
                }
                faction = MySession.Static.Factions.GetPlayerFaction(owner);
                if (faction != null && (allExceptions.Contains(faction.Tag) ||
                                        allExceptions.Contains(faction.FactionId.ToString()))) return true;
            }

            if (playerSteamId > 0 && allExceptions.Contains(playerSteamId.ToString())) return true;
            if (identityId > 0 && allExceptions.Contains(identityId.ToString())) return true;
            if (identity != null && allExceptions.Contains(identity.DisplayName)) return true;
            if (faction != null && (allExceptions.Contains(faction.Tag)|| allExceptions.Contains(faction.FactionId.ToString()))) return true;
            return !string.IsNullOrEmpty(displayName) && allExceptions.Contains(displayName);
        }

        internal bool IsMatch(MyCubeBlockDefinition definition)
        {
            var blockList = new HashSet<string>(_blockList);
            if (blockList.Count == 0 || definition == null) return false;

            
            if (GridTypeBlock != GridType.AllGrids)
            {

                if (definition.CubeSize == MyCubeSize.Small && (GridTypeBlock == GridType.LargeGridsOnly ||
                                                                GridTypeBlock == GridType.StationsOnly ||
                                                                GridTypeBlock == GridType.SupportedStationsOnly))
                    return false;

                if (definition.CubeSize == MyCubeSize.Large && GridTypeBlock == GridType.SmallGridsOnly) return false;
            }

            var defString = new HashSet<string>
            {
                definition.Id.ToString().Substring(16), definition.Id.TypeId.ToString().Substring(16),
                definition.Id.SubtypeId.ToString(), definition.BlockPairName
            };

            return blockList.Any(block => !string.IsNullOrEmpty(block) && defString.Contains(block, StringComparer.OrdinalIgnoreCase));
        }

        internal bool IsFilterType(MyCubeGrid grid)
        {
            if (LimitFilterType == FilterType.None) return true;
            switch (LimitFilterType)
            {
                case FilterType.PlayerPlayTime:
                    var owners = new HashSet<long>(GridCache.GetOwners(grid));
                    owners.UnionWith(GridCache.GetBuilders(grid));
                    if (owners.Count == 0) break;
                    var player = MySession.Static.Players.TryGetIdentity(owners.FirstOrDefault());
                    break;
                case FilterType.GridBlockCount:
                    return LimitFilterOperator == FilterOperator.GreaterThan
                        ? grid.CubeBlocks.Count > FilterValue
                        : grid.CubeBlocks.Count < FilterValue;
                case FilterType.FactionMemberCount:
                    var owners1 = new HashSet<long>(GridCache.GetOwners(grid));
                    owners1.UnionWith(GridCache.GetBuilders(grid));
                    if (owners1.Count == 0) break;
                    var ownerFaction = MySession.Static.Factions.GetPlayerFaction(owners1.FirstOrDefault());
                    if (ownerFaction == null) break;
                    if (LimitFilterOperator == FilterOperator.GreaterThan) return ownerFaction.Members.Count > FilterValue;
                    else
                    {
                        return ownerFaction.Members.Count < FilterValue;
                    }
                case FilterType.GridMass:
                    grid.GetCurrentMass(out var baseMass, out var _);
                    return LimitFilterOperator == FilterOperator.GreaterThan
                        ? baseMass > FilterValue
                        : baseMass < FilterValue;
                case FilterType.GridPoints:
                    if (!PointCheckApi.IsInstalled())
                    {
                        BlockLimiter.Instance.Log.Warn("GridPoint check not active");
                        return false;
                    }
                    var gridScore = PointCheckApi.GetGridBP(grid);
                    if (gridScore == 0) return false;
                    return LimitFilterOperator == FilterOperator.GreaterThan
                        ? gridScore > FilterValue
                        : gridScore < FilterValue;
                default:
                    return false;
            }

            return false;
        }
        
        internal bool IsFilterType(MyObjectBuilder_CubeGrid grid, long playerId = 0)
        {
            if (LimitFilterType == FilterType.None) return true;
            switch (LimitFilterType)
            {
                case FilterType.PlayerPlayTime:
                    if (playerId == 0) break;
                    var player = MySession.Static.Players.TryGetSteamId(playerId);
                    if (player == 0) break;
                    var playerTime = PlayerTimeModule.GetTime(player);
                    return LimitFilterOperator == FilterOperator.GreaterThan
                        ? (DateTime.Now - playerTime).TotalDays > FilterValue
                        : (DateTime.Now - playerTime).TotalDays < FilterValue;
                case FilterType.GridBlockCount:
                    return LimitFilterOperator == FilterOperator.GreaterThan
                        ? grid.CubeBlocks.Count > FilterValue
                        : grid.CubeBlocks.Count < FilterValue;
                case FilterType.FactionMemberCount:
                    if (playerId == 0)break;
                    var ownerFaction = MySession.Static.Factions.GetPlayerFaction(playerId);
                    if (ownerFaction == null) break;
                    if (LimitFilterOperator == FilterOperator.GreaterThan) return ownerFaction.Members.Count > FilterValue;
                    else
                    {
                        return ownerFaction.Members.Count < FilterValue;
                    }
                case FilterType.GridPoints:
                    if(!PointCheckApi.IsInstalled()) break;
                    var gridPoint = PointCheckApi.GetGridBP(grid);
                    if (gridPoint == 0) break;
                    return _limitOperator == FilterOperator.GreaterThan ? gridPoint > _filterValue : gridPoint < _filterValue;
                default:
                    return false;
            }

            return false;
        }
        
        internal bool IsGridType(MyCubeGrid grid)
        {
            bool isGridType = false;
            var isFilterType = IsFilterType(grid);

            switch (GridTypeBlock)
            {
                case GridType.SmallGridsOnly:
                    isGridType = grid.GridSizeEnum == MyCubeSize.Small;
                    break;
                case GridType.LargeGridsOnly:
                    isGridType =  grid.GridSizeEnum == MyCubeSize.Large;
                    break;
                case GridType.StationsOnly:
                    isGridType = grid.GridSizeEnum != MyCubeSize.Small && grid.IsStatic && grid.IsUnsupportedStation;
                    break;
                case GridType.ShipsOnly:
                    if (!grid.IsStatic)
                        isGridType = true;
                    break;
                case GridType.AllGrids:
                    isGridType = true;
                    break;
                case GridType.SupportedStationsOnly:
                    isGridType = grid.IsStatic && !grid.IsUnsupportedStation;
                    break;
            }

            return isGridType && isFilterType;
        }

        internal bool IsGridType(MyObjectBuilder_CubeGrid grid, long playerId = 0)
        {
            bool isGridType = false;
            var isFilterType = IsFilterType(grid,playerId);

            switch (GridTypeBlock)
            {
                case GridType.AllGrids:
                    isGridType = true;
                    break;
                case GridType.SmallGridsOnly:
                    isGridType = grid.GridSizeEnum == MyCubeSize.Small;
                    break;
                case GridType.LargeGridsOnly:
                    isGridType = grid.GridSizeEnum == MyCubeSize.Large;
                    break;
                case GridType.StationsOnly:
                    isGridType = grid.IsStatic;
                    break;
                case GridType.ShipsOnly:
                    isGridType = !grid.IsStatic;
                    break;
            }

            return isGridType && isFilterType;
        }

        internal void ClearEmptyEntities()
        {
            var foundEntities = FoundEntities;
            if (foundEntities == null || foundEntities.Count == 0) return;
            foreach (var entity in foundEntities)
            {
             if (entity.Value ==0) FoundEntities.Remove(entity.Key);
            }
        }
        
        internal void Reset()
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


        #endregion

        #region Enum

        public enum PunishmentType
        {
            None,
            DeleteBlock,
            ShutOffBlock,
            Explode
        }

        public enum FilterType
        {
            None,
            PlayerPlayTime,
            GridBlockCount,
            FactionMemberCount,
            GridMass,
            GridPoints
        }

        public enum FilterOperator
        {
            LessThan,
            GreaterThan
        }

        public enum GridType
        {
            AllGrids,
            SmallGridsOnly,
            LargeGridsOnly,
            StationsOnly,
            ShipsOnly,
            SupportedStationsOnly
        }


        #endregion
    }
}
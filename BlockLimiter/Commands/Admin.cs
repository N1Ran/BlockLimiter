using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BlockLimiter.ProcessHandlers;
using BlockLimiter.Settings;
using BlockLimiter.Utility;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch.API.Plugins;
using Torch.Commands;
using Torch.Commands.Permissions;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;

namespace BlockLimiter.Commands
{
    public partial class Player
    {
        private static DateTime _lastRun = DateTime.MinValue;
        private static bool _doCheck = false;
        
        [Command("enable", "enable/disable blocklimit plugin")]
        [Permission(MyPromoteLevel.Admin)]
        public void Enable(bool enable = true)
        {
            BlockLimiterConfig.Instance.EnableLimits = enable;

            Context.Respond(enable ? "BlockLimiter Enabled" : "BlockLimiter Disabled");
        }
        
        [Command("log", "enable/disable blocklimit log")]
        [Permission(MyPromoteLevel.Admin)]
        public void EnableLog(bool enable = true)
        {
            BlockLimiterConfig.Instance.EnableLog = enable;

            Context.Respond(enable ? "Logging Enabled" : "Logging Disabled");
        }
        
        [Command("update", "updates limits")]
        [Permission(MyPromoteLevel.Moderator)]
        public void UpdateLimits()
        {
            var time = DateTime.Now - _lastRun;
            if (time.TotalMinutes < 1)
            {
                var timeRemaining = TimeSpan.FromMinutes(1) - time;
                Context.Respond($"Cooldown in effect.  Try again in {timeRemaining.TotalSeconds:N0} seconds");
                return;
            }

            var args = Context.Args;

            GridCache.Update();

            if (args.Count == 0)
            {
                if (!_doCheck)
                {
                    Context.Respond("Warning: This command will drop sim speed for few seconds/minutes while recalculating limits.  Run command again to proceed");
                    _doCheck = true;
                    Task.Run(() =>
                    {
                        Thread.Sleep(30000);
                        _doCheck = false;
                    });
                    return;
                }
                _doCheck = false;
                BlockLimiterConfig.Instance.Save();
                BlockLimiter.ResetLimits();
                _lastRun = DateTime.Now;
            
                Context.Respond("Limits updated");
                return;
            }

            foreach (var arg in args)
            {
                if (arg.StartsWith("-player"))
                {
                    var name = arg.Replace("-player=", "");
                    if (!Utilities.TryGetPlayerByNameOrId(name, out var identity))
                    {
                        Context.Respond($"Player {name} not found");
                        continue;
                    }

                    Context.Respond($"Updated {identity.DisplayName} limits");
                    Utility.UpdateLimits.PlayerLimit(identity.IdentityId);
                    continue;
                }
                
                if (arg.StartsWith("-grid"))
                {
                    var gridName = arg.Replace("-grid=", "");

                    if (!Utilities.TryGetEntityByNameOrId(gridName, out var entity))
                    {
                        
                        Context.Respond($"No entity with the name {gridName} found");
                        continue;
                    }

                    if (!(entity is MyCubeGrid grid))
                    {
                        Context.Respond("No grid found");
                        continue;
                    }
            
                    Context.Respond($"{grid.DisplayName} limits updated");
                    Utility.UpdateLimits.GridLimit(grid);
                    continue;

                }
                
                if (arg.StartsWith("-faction"))
                {
                    var factionTag = arg.Replace("-faction=","");
                    var faction = MySession.Static.Factions.TryGetFactionByTag(factionTag);
                    if (faction == null)
                    {
                        Context.Respond($"{factionTag} was not found in factions.  Check spelling and case.");
                        continue;
                    }
                    Context.Respond($"{faction.Tag} limits updated");
                    Utility.UpdateLimits.FactionLimit(faction.FactionId);
                }
            }

        }

        [Command("reload", "Reloads current BlockLimiter.cfg and apply any changes to current session")]
        public void Reload()
        {
            var time = DateTime.Now - _lastRun;
            if (time.TotalMinutes < 1)
            {
                var timeRemaining = TimeSpan.FromMinutes(1) - time;
                Context.Respond($"Cooldown in effect.  Try again in {timeRemaining.TotalSeconds:N0} seconds");
                return;
            }
            
            if (!_doCheck)
            {
                Context.Respond("Warning: This command will drop sim speed for few seconds/minutes while recalculating limits.  Run command again to proceed");
                _doCheck = true;
                Task.Run(() =>
                {
                    Thread.Sleep(30000);
                    _doCheck = false;
                });
                return;
            }
            
            _doCheck = false;
            
            BlockLimiterConfig.Instance.Load();
            BlockLimiterConfig.Instance.Save();
            BlockLimiter.ResetLimits();
            
            _lastRun = DateTime.Now;
            
            Context.Respond("Limits reloaded from config file");
        }

        
        [Command("violations", "gets the list of violations per limit")]
        [Permission(MyPromoteLevel.Moderator)]
        public void GetViolations()
        {
            if (!BlockLimiterConfig.Instance.EnableLimits)
            {
                Context.Respond("Plugin disabled");
                return;
            }

            var limitItems = BlockLimiterConfig.Instance.AllLimits;

            if (!limitItems.Any(x=>x.FoundEntities.Any()))
            {
                Context.Respond("No violations found");
                return;
            }
            var sb = new StringBuilder();
            
            //Todo: Add conditions
            /*
            foreach (var arg in Context.Args)
            {
                if (arg.StartsWith("--gps="))
                {
                    
                }
                
                if (arg.StartsWith("--grid="))
                {
                    
                }
                
                if (arg.StartsWith("--player="))
                {
                    
                }
                
                if (arg.StartsWith("--faction="))
                {
                    
                }
                
            }
            */
            
            foreach (var item in limitItems)
            {
                if (!item.BlockList.Any() || !item.FoundEntities.Any(x => x.Value > 0)) continue;
                
                var itemName = string.IsNullOrEmpty(item.Name) ? item.BlockList.FirstOrDefault() : item.Name;

                sb.AppendLine($"{itemName} Violators");

                foreach (var (entity,count) in item.FoundEntities)
                {
                    if (count <= item.Limit) continue;
                    
                    var faction = MySession.Static.Factions.TryGetFactionById(entity);
                    if (faction != null)
                    {
                        sb.AppendLine($"FactionLimit for {faction.Tag} = {count}/{item.Limit}");
                        continue;
                    }

                    var player = MySession.Static.Players.TryGetIdentity(entity);
                    if (player != null)
                    {
                        sb.AppendLine($"PlayerLimit for {player.DisplayName} = {count}/{item.Limit}");
                        continue;
                    }
                    
                    if(!GridCache.TryGetGridById(entity, out var grid))continue;
                    sb.AppendLine($"GridLimit for {grid.DisplayName} =  {count}/{item.Limit}");
                }

                sb.AppendLine();
            }

            sb.AppendLine();


            if (Context.Player == null || Context.Player.IdentityId == 0)
            {
                Context.Respond(sb.ToString());
                return;
            }

            ModCommunication.SendMessageTo(new DialogMessage(BlockLimiterConfig.Instance.ServerName,"List of Violations",sb.ToString()),Context.Player.SteamUserId);

        }


        [Command("playerlimit", "gets the current limits of targeted player")]
        public void GetPlayerLimit(string name)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits)
            {
                Context.Respond("Plugin disabled");
                return;
            }

            var sb = new StringBuilder();

            if (!Utilities.TryGetPlayerByNameOrId(name, out var id))
            {
                Context.Respond($"Player {name} not found");
                return;
            }

            sb = Utilities.GetLimit(id.IdentityId);
            
            
            if (Context.Player == null || Context.Player.IdentityId == 0)
            {
                Context.Respond(sb.ToString());
                return;
            }

            ModCommunication.SendMessageTo(new DialogMessage(BlockLimiterConfig.Instance.ServerName,"PlayerLimit",sb.ToString()),Context.Player.SteamUserId);
              
        }

        [Command("gridlimit", "gets the current limits of specified grid")]
        public void GridLimit(string id)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits)
            {
                Context.Respond("Plugin disabled");
                return;
            }
            
            if (string.IsNullOrEmpty(id))
            {
                Context.Respond("Grid name/Id is needed for this command");
                return;
            }

            if (!Utilities.TryGetEntityByNameOrId(id, out var entity) || !(entity is MyCubeGrid grid))
            {
                Context.Respond("Grid not found");
                return;
            }
            
            var sb = new StringBuilder();
            
            var limitItems = new List<LimitItem>();
            
            limitItems.AddRange(BlockLimiterConfig.Instance.AllLimits);

            if (!limitItems.Any())
            {
                Context.Respond("No limit found");
                return;
            }

            sb.AppendLine($"Grid Limits for {grid.DisplayName}");

            foreach (var item in limitItems.Where(x=>x.LimitGrids))
            {
                {
                    if (!item.FoundEntities.TryGetValue(grid.EntityId, out var gCount))continue;

                    var itemName = string.IsNullOrEmpty(item.Name) ? item.BlockList.FirstOrDefault() : item.Name;
                        
                    sb.AppendLine($"-->{itemName} = {gCount }/{item.Limit}");
                }
            }
            
            
            if (Context.Player == null || Context.Player.IdentityId == 0)
            {
                Context.Respond(sb.ToString());
                return;
            }

            ModCommunication.SendMessageTo(new DialogMessage(BlockLimiterConfig.Instance.ServerName,"Faction Limits",sb.ToString()),Context.Player.SteamUserId);


        }

        [Command("factionlimit", "gets the current limits of specified faction")]
        public void ListFactionLimit(string factionTag)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits)
            {
                Context.Respond("Plugin disabled");
                return;
            }

            if (string.IsNullOrEmpty(factionTag))
            {
                Context.Respond("Faction tag is needed for this command");
                return;
            }

            var faction = MySession.Static.Factions.TryGetFactionByTag(factionTag);

            if (faction == null)
            {
                Context.Respond($"Faction with tag {factionTag} not found");
                return;
            }
            var sb = new StringBuilder();
            
            var limitItems = new List<LimitItem>();
            
            limitItems.AddRange(BlockLimiterConfig.Instance.AllLimits);

            if (!limitItems.Any())
            {
                Context.Respond("No limit found");
                return;
            }

            sb.AppendLine($"Faction Limits for {faction.Tag}");

            foreach (var item in limitItems.Where(x=>x.LimitFaction))
            {
                {
                    if (!item.FoundEntities.TryGetValue(faction.FactionId, out var fCount))continue;

                    var itemName = string.IsNullOrEmpty(item.Name) ? item.BlockList.FirstOrDefault() : item.Name;
                        
                    sb.AppendLine($"-->{itemName} = {fCount}/{item.Limit}");
                }
            }
            
            
            if (Context.Player == null || Context.Player.IdentityId == 0)
            {
                Context.Respond(sb.ToString());
                return;
            }

            ModCommunication.SendMessageTo(new DialogMessage(BlockLimiterConfig.Instance.ServerName,"Faction Limits",sb.ToString()),Context.Player.SteamUserId);
        }



    }
}
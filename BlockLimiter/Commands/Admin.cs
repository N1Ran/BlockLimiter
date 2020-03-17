using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        [Command("enable", "enable/disable blocklimit plugin")]
        [Permission((MyPromoteLevel.Admin))]
        public void Enable(bool enable = true)
        {
            BlockLimiterConfig.Instance.EnableLimits = enable;

            Context.Respond(enable ? "BlockLimiter Enabled" : "BlockLimiter Disabled");
        }
        
        [Command("log", "enable/disable blocklimit log")]
        [Permission((MyPromoteLevel.Admin))]
        public void EnableLog(bool enable = true)
        {
            BlockLimiterConfig.Instance.EnableLog = enable;

            Context.Respond(enable ? "Logging Enabled" : "Logging Disabled");
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
            
            
            foreach (var item in limitItems)
            {
                if (!item.BlockPairName.Any() || !item.FoundEntities.Any(x => x.Value > 0)) continue;
                
                var itemName = string.IsNullOrEmpty(item.Name) ? item.BlockPairName.FirstOrDefault() : item.Name;

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
        public void GetPlayerLimit(string id)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits)
            {
                Context.Respond("Plugin disabled");
                return;
            }

            var sb = new StringBuilder();
            
            if (long.TryParse(id, out var identityId))
            {
               sb = Utilities.GetLimit(identityId);
            }

            else
            {
                var player = MySession.Static.Players.GetAllIdentities()
                    .FirstOrDefault(x => x.DisplayName.Equals(id, StringComparison.OrdinalIgnoreCase));
                //var player = MySession.Static.Players.GetPlayerByName(id);

                if (player == null)
                {
                    Context.Respond("Player not found");
                    return;
                }
                var playerId = player.IdentityId;
                
                if (playerId == 0)
                {
                    Context.Respond("Player not found");
                    return;
                }
                sb = Utilities.GetLimit(playerId);
            }
            
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

                    var itemName = string.IsNullOrEmpty(item.Name) ? item.BlockPairName.FirstOrDefault() : item.Name;
                        
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

                    var itemName = string.IsNullOrEmpty(item.Name) ? item.BlockPairName.FirstOrDefault() : item.Name;
                        
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
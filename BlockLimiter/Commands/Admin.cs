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
        [Command("violations", "gets the list of violations per limit")]
        [Permission(MyPromoteLevel.Moderator)]
        public void GetViolations()
        {
            var limitItems = BlockLimiterConfig.Instance.AllLimits;

            if (!limitItems.Any(x=>x.FoundEntities.Any()))
            {
                Context.Respond("No violations found");
                return;
            }

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
            
            var sb = new StringBuilder();
            
            foreach (var item in limitItems)
            {
                if (!item.BlockPairName.Any() || !item.FoundEntities.Any(x => x.Value > 0)) continue;
                
                var itemName = string.IsNullOrEmpty(item.Name) ? item.BlockPairName.FirstOrDefault() : item.Name;

                sb.AppendLine($"{itemName} Violators");

                foreach (var (entity,count) in item.FoundEntities)
                {
                    if (count <= 0) continue;
                    
                    var faction = MySession.Static.Factions.TryGetFactionById(entity);
                    if (faction != null)
                    {
                        sb.AppendLine($"FactionLimit for {faction.Tag} = {item.Limit + count}/{item.Limit}");
                        continue;
                    }

                    var player = MySession.Static.Players.TryGetIdentity(entity);
                    if (player != null)
                    {
                        sb.AppendLine($"PlayerLimit for {player.DisplayName} = {item.Limit + count}/{item.Limit}");
                        continue;
                    }
                    
                    if(!GridCache.TryGetGridById(entity, out var grid)|| !(grid is MyCubeGrid))continue;
                    sb.AppendLine($"GridLimit for {grid.DisplayName} =  {item.Limit + count}/{item.Limit}");
                }
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
            var sb = new StringBuilder();
            
            if (long.TryParse(id, out var identityId))
            {
               sb = Utilities.GetLimit(identityId);
            }

            else
            {
                var player = MySession.Static.Players.GetPlayerByName(id);

                if (player == null)
                {
                    Context.Respond("Player not found");
                    return;
                }
                var playerId = player.Identity.IdentityId;
                
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

        [Command("factionlimit", "gets the current limits of targeted faction")]
        public void ListFactionLimit(string factionTag)
        {
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
                        
                    sb.AppendLine($"-->{itemName} = {fCount + item.Limit}/{item.Limit}");
                }
            }
            
            
            if (Context.Player == null || Context.Player.IdentityId == 0)
            {
                Context.Respond(sb.ToString());
                return;
            }

            ModCommunication.SendMessageTo(new DialogMessage(BlockLimiterConfig.Instance.ServerName,"Faction Limits",sb.ToString()),Context.Player.SteamUserId);
        }

        

        
        
        [Command("pairnames", "gets the list of all pairnames possible. BlockType is case sensitive")]
        public void ListPairNames(string blockType=null)
        {
            var sb = new StringBuilder();

            var allDef = MyDefinitionManager.Static.GetAllDefinitions();

            var def = new List<MyDefinitionBase>();

            if (!string.IsNullOrEmpty(blockType))
            {
                foreach (var defBase in allDef)
                {
                    if (!defBase.Id.TypeId.ToString().Substring(16).Equals(blockType,StringComparison.OrdinalIgnoreCase))
                        continue;
                    def.Add(defBase);
                }

                if (!def.Any())
                {
                    Context.Respond($"Can't find any definition for {blockType}");
                    return;
                }

            }

            else
            {
                def.AddRange(allDef);
            }

            if (!def.Any())
            {
                Context.Respond("Na Bruh!");
                return;
            }

            sb.AppendLine($"Total of {def.Count} definitions found on server");
            foreach (var myDefinitionId in def)
            {
                var modId = "0";
                var modName = "Vanilla";
                if (myDefinitionId.Context?.IsBaseGame == false)
                {
                    modId = myDefinitionId.Context?.ModId;
                    modName = myDefinitionId.Context?.ModName;
                }
                if (!MyDefinitionManager.Static.TryGetCubeBlockDefinition(myDefinitionId.Id, out var x))continue;
                sb.AppendLine($"{x.BlockPairName} [{modName} - {modId}]");
            }
            if (Context.Player == null || Context.Player.IdentityId == 0)
            {
                Context.Respond(sb.ToString());
                return;
            }

            ModCommunication.SendMessageTo(new DialogMessage(BlockLimiterConfig.Instance.ServerName,"List of Limits",sb.ToString()),Context.Player.SteamUserId);
        }


    }
}
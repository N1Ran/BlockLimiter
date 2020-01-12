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
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Game;
using VRage.ObjectBuilders;

namespace BlockLimiter.Commands
{
    public partial class Player
    {
        [Command("violations", "gets the list of violations per limit")]
        public void GetViolations()
        {
            var limitItems = BlockLimiterConfig.Instance.AllLimits;

            if (!limitItems.Any(x=>x.ViolatingEntities.Any()))
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
                if (!item.BlockPairName.Any() || !item.ViolatingEntities.Any()) continue;
                var limitName = string.IsNullOrEmpty(item.Name) ? item.BlockPairName.FirstOrDefault() : item.Name;
                var violatingEntities = item.ViolatingEntities.Select(y=>y.Key).ToList();
                var factions = violatingEntities.Select(de => MySession.Static.Factions.TryGetFactionById(de)).Where(x=>x!=null).ToList();
                var players = violatingEntities.Select(de => MySession.Static.Players.TryGetIdentity(de)).Where(x=>x!=null).ToList();
                var grids = violatingEntities.Select(de => MyEntities.GetEntityById(de)).OfType<MyCubeGrid>().ToList();
                sb.AppendLine();
                sb.AppendLine($"{limitName}");
                if (factions.Any())
                {
                    sb.AppendLine("Violating Factions: ");
                    foreach (var faction in factions)
                    {
                        sb.AppendLine(
                            $"[{faction.Name} -- {faction.Tag}] -- {item.ViolatingEntities[faction.FactionId] + item.Limit}/{item.Limit}");
                    }

                }
                
                if (players.Any())
                {
                    sb.AppendLine("Violating Players: ");
                    foreach (var player in players)
                    {
                        sb.AppendLine(
                            $"[{player.DisplayName} -- {player.IdentityId}] -- {item.ViolatingEntities[player.IdentityId] + item.Limit}/{item.Limit}");
                    }
                    sb.AppendLine();
                }

                if (grids.Any())
                {
                    sb.AppendLine("Violating Grids: ");
                    foreach (var grid in grids)
                    {
                        sb.AppendLine(
                            $"[{grid.DisplayName} -- {grid.EntityId}] -- {item.ViolatingEntities[grid.EntityId] + item.Limit}/{item.Limit}");
                    }
                }


            }


            if (Context.Player == null || Context.Player.IdentityId == 0)
            {
                Context.Respond(sb.ToString());
                return;
            }

            ModCommunication.SendMessageTo(new DialogMessage(BlockLimiterConfig.Instance.ServerName,"List of Violatons",sb.ToString()),Context.Player.SteamUserId);

        }

        [Command("playerlimit", "gets the current limits of targeted player")]
        public void GetPlayerLimit(string id)
        {
            var sb = new StringBuilder();
            
            if (long.TryParse(id, out var identityId))
            {
               sb = GetLimit(identityId);
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
                sb = GetLimit(playerId);
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
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using BlockLimiter.ProcessHandlers;
using BlockLimiter.Settings;
using BlockLimiter.Utility;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Torch;
using Torch.Commands;
using Torch.Commands.Permissions;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Dedicated.RemoteAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;

namespace BlockLimiter.Commands
{
    [Category("blocklimit")]
    public partial class Player:CommandModule
    {
        [Command("mylimit", "list current player status")]
        [Permission(MyPromoteLevel.None)]
        public void MyLimit()
        {
            if (Context.Player == null || Context.Player.IdentityId == 0)
            {
                Context.Respond("Command can only be run in-game by players");
                return;
            }

            var playerId = Context.Player.IdentityId;

            var newList = BlockLimiterConfig.Instance.AllLimits;

            if (!newList.Any())
            {
                Context.Respond("No limit item found");
                return;
            }
            
            var sb = new StringBuilder();

            sb = GetLimit(playerId);

            ModCommunication.SendMessageTo(new DialogMessage(BlockLimiterConfig.Instance.ServerName,"PlayerLimit",sb.ToString()),Context.Player.SteamUserId);

        }

        [Command("limits", "gets list of limits and there settings")]
        [Permission(MyPromoteLevel.None)]
        public void GetLimits()
        {
            var sb = new StringBuilder();
            var limiterLimits = BlockLimiterConfig.Instance.AllLimits.ToList();
            if (!limiterLimits.Any())
            {
                Context.Respond("No limit item found");
                return;
            }

            sb.AppendLine($"Found {limiterLimits.Where(x=>x.BlockPairName.Any()).ToList().Count()} items");
            foreach (var item in limiterLimits.Where(x=>x.BlockPairName.Any()))
            {
                var name = string.IsNullOrEmpty(item.Name) ? "No Name" : item.Name;
                sb.AppendLine();
                sb.AppendLine(name);
                item.BlockPairName.ForEach(x=>sb.Append($"[{x}] "));
                sb.AppendLine();
                sb.AppendLine($"Limits:       {item.Limit}");
                sb.AppendLine($"PlayerLimit:  {item.LimitPlayers}");
                sb.AppendLine($"FactionLimit: {item.LimitFaction}");
                sb.AppendLine($"GridLimit:    {item.LimitGrids}");
            }

            if (Context.Player == null || Context.Player.IdentityId == 0)
            {
                Context.Respond(sb.ToString());
                return;
            }

            ModCommunication.SendMessageTo(new DialogMessage(BlockLimiterConfig.Instance.ServerName,"List of Limits",sb.ToString()),Context.Player.SteamUserId);
        }


        public StringBuilder GetLimit(long playerId)
        {
            
            var sb = new StringBuilder();
            if (playerId == 0)
            {
                sb.AppendLine("Player not found");
                return sb;
            }
            var newList = BlockLimiterConfig.Instance.AllLimits;

            if (!newList.Any())
            {
                sb.AppendLine("No limit found");
                return sb;
            }
            
            var grids = MyEntities.GetEntities().OfType<MyCubeGrid>().ToList();

            var playerFaction = MySession.Static.Factions.GetPlayerFaction(playerId);

            foreach (var item in newList)
            {
                if (!item.BlockPairName.Any()) continue;
                var itemName = string.IsNullOrEmpty(item.Name)?item.BlockPairName.FirstOrDefault():item.Name;
                sb.AppendLine(itemName);
                var count = 0;
                if (item.LimitPlayers)
                {
                    foreach (var block in grids.SelectMany(x=>x.CubeBlocks))
                    {
                        if (!Utilities.IsOwner(item.BlockOwnerState, block, playerId)) continue;
                        if (!Utilities.IsMatch(block.BlockDefinition,item)) continue;
                        count++;
                    }
                    sb.AppendLine($"Player Limit = {count}/{item.Limit}");
                }

                if (item.LimitGrids)
                {
                    sb.AppendLine("Grid Limits");
                    foreach (var grid in grids.Where(x=>x.BigOwners.Contains(playerId)))
                    {
                        count = 0;
                        var gridBlocks = grid.CubeBlocks;
                        foreach (var block in gridBlocks)
                        {
                            if (!Utilities.IsMatch(block.BlockDefinition,item)) continue;
                            count++;
                        }
                        sb.AppendLine($"->{grid.DisplayName} = {count}/{item.Limit}");
                    }
                }
                
                if (playerFaction == null || !item.LimitFaction) continue;
                count = 0;
                foreach (var block in grids.SelectMany(g=>g.CubeBlocks))
                {
                    if (!block.FatBlock.GetOwnerFactionTag().Equals(playerFaction.Tag)) continue;
                    if (!Utilities.IsMatch(block.BlockDefinition, item)) continue;
                    count++;
                    sb.AppendLine($"Faction Limit [{playerFaction.Tag}] = {count}/{item.Limit}");
                }

                sb.AppendLine();

            }

            return sb;

        }
    }
}
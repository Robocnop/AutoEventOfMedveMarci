using System;
using System.Linq;
using AutoEvent.API;
using AutoEvent.Interfaces;
using CommandSystem;
using LabApi.Features.Permissions;
using LabApi.Features.Wrappers;
using MEC;

namespace AutoEvent.Commands;

internal class Run : ICommand, IUsageProvider
{
    public string Command => nameof(Run);
    public string Description => "Run the event. Arguments: <CommandName> [MapName]";
    public string[] Aliases => ["start", "play", "begin"];

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (!sender.HasPermissions("ev.run"))
        {
            response = "<color=red>You do not have permission to use this command!</color>";
            return false;
        }

        if (AutoEvent.EventManager.CurrentEvent != null)
        {
            response = $"The mini-game {AutoEvent.EventManager.CurrentEvent.Name} is already running!";
            return false;
        }

        if (arguments.Count < 1)
        {
            response = "You need to specify the name of the event, optionally the name of the map!";
            return false;
        }

        var ev = AutoEvent.EventManager.GetEvent(arguments.At(0));
        if (ev == null)
        {
            response = $"The mini-game '{arguments.At(0)}' was not found.";
            return false;
        }

        if (ev is IEventMap map && !string.IsNullOrEmpty(map.MapInfo.MapName) &&
            !string.Equals(map.MapInfo.MapName, "none", StringComparison.OrdinalIgnoreCase))
            if (!Extensions.IsExistsMap(map.MapInfo.MapName, out response))
                return false;

        var readyPlayers = Player.ReadyList;
        var ignoredRoles = AutoEvent.Singleton.Config?.IgnoredRoles;
        if (ignoredRoles is { Count: > 0 })
            readyPlayers = readyPlayers.Where(p => !ignoredRoles.Contains(p.Role));

        var players = readyPlayers.ToList();
        if (!players.Any())
        {
            response = "There are no eligible players on the server!";
            return false;
        }

        if (ev is IPlayerCountLimited limited && players.Count > limited.MaxPlayers)
        {
            response = $"Too many players! The maximum for '{ev.Name}' is {limited.MaxPlayers}.";
            return false;
        }

        var mapName = arguments.Count >= 2 ? arguments.At(1) : string.Empty;

        Round.IsLocked = true;
        if (!Round.IsRoundStarted)
        {
            Round.Start();
            Timing.CallDelayed(1f, () =>
            {
                foreach (var player in Player.ReadyList)
                    player.ClearInventory();
                ev.StartEvent(mapName);
            });
        }
        else
        {
            ev.StartEvent(mapName);
        }

        response = $"The mini-game {ev.Name} has started!";
        return true;
    }

    public string[] Usage => ["Event Name", "Map Name (Optional)"];
}
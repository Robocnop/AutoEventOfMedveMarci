using System;
using System.Linq;
using CommandSystem;
using LabApi.Features.Wrappers;

namespace AutoEvent.Games.AmongUs;

[CommandHandler(typeof(ClientCommandHandler))]
internal class Vote : ICommand, IUsageProvider
{
    public string Command => "vote";
    public string Description => "Vote command for the Among Us event.";
    public string[] Aliases => [];

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (AutoEvent.EventManager.CurrentEvent.CommandName != "amongus")
        {
            response = "The Among Us event is not running.";
            return false;
        }

        if (!Plugin.Instance.MeetingCalled)
        {
            response = "No meeting is currently in progress.";
            return false;
        }

        if (arguments.Count != 1)
        {
            response = "Usage: .vote <color>";
            return false;
        }

        var player = Player.Get(sender);

        if (player == null)
        {
            response = "Player not found.";
            return false;
        }

        if (!Plugin.Instance.Impostors.Contains(player) && !Plugin.Instance.Crewmates.Contains(player))
        {
            response = "You are not part of the Among Us event.";
            return false;
        }

        var colorName = arguments.At(0);

        if (!Enum.TryParse(colorName, true, out Misc.PlayerInfoColorTypes colorType) ||
            !Misc.AllowedColors.TryGetValue(colorType, out var colorHex))
        {
            response = "Invalid color.";
            return false;
        }

        var votedPlayer = Player.Get(Plugin.Instance.PlayerColors.First(p => p.Value == colorHex).Key);
        if (votedPlayer == null)
        {
            response = "The player with that color was not found.";
            return false;
        }

        if (votedPlayer == player)
        {
            response = "You cannot vote for yourself.";
            return false;
        }

        Plugin.Instance.PlayerVotes[player.NetworkId] = votedPlayer.NetworkId;
        response = $"You voted for {votedPlayer.Nickname} ({votedPlayer.GroupColor}).";
        return true;
    }

    public string[] Usage => ["Player's Color"];
}
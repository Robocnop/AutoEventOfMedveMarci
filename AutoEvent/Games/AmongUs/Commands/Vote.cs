using System;
using System.Linq;
using AutoEvent.ApiFeatures;
using CommandSystem;
using LabApi.Features.Wrappers;
using Utils;

namespace AutoEvent.Games.AmongUs;

[CommandHandler(typeof(ClientCommandHandler))]
internal class Vote : ICommand, IUsageProvider
{
    public string Command => "vote";
    public string Description => "Vote command for the Among Us event.";
    public string[] Aliases => [];

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (AutoEvent.EventManager.CurrentEvent?.CommandName != "amongus")
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
            response = "Usage: .vote <color/name/skip>";
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

        if (!player.IsAlive)
        {
            response = "You cannot vote because you are dead.";
            return false;
        }

        var colorName = arguments.At(0);

        if (string.Equals(colorName, "none", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(colorName, "skip", StringComparison.OrdinalIgnoreCase))
        {
            Plugin.Instance.PlayerVotes[player.NetworkId] = 0;
            response = "You voted for no one.";
            return true;
        }

        if (!Enum.TryParse(colorName, true, out Misc.PlayerInfoColorTypes colorType) ||
            !Misc.AllowedColors.TryGetValue(colorType, out var colorHex))
        {
            var referenceHubList = RAUtils.ProcessPlayerIdOrNamesList(arguments, 0, out _);

            if (referenceHubList.Count == 0)
            {
                response = "Invalid player name.";
                return false;
            }

            var targetPlayer = Player.Get(referenceHubList[0]);
            if (targetPlayer == null)
            {
                response = "Player or color not found.";
                return false;
            }

            if (!Plugin.Instance.PlayerColors.TryGetValue(targetPlayer.NetworkId, out var targetColorHex))
            {
                response = "The player with that name was not found.";
                return false;
            }

            colorHex = targetColorHex;
        }

        LogManager.Debug(colorHex);

        var valueColor =
            (from playerColor in Plugin.Instance.PlayerColors
                where playerColor.Value.Equals(colorHex, StringComparison.OrdinalIgnoreCase)
                select playerColor.Key).FirstOrDefault();

        if (valueColor == 0)
        {
            response = "The player with that color was not found.";
            return false;
        }

        var votedPlayer = Player.Get(valueColor);
        if (votedPlayer == null)
        {
            response = "The player with that color was not found.";
            return false;
        }

        if (!votedPlayer.IsAlive)
        {
            response = "You cannot vote for a dead player.";
            return false;
        }

        if (votedPlayer == player)
        {
            response = "You cannot vote for yourself.";
            return false;
        }

        Plugin.Instance.PlayerVotes[player.NetworkId] = votedPlayer.NetworkId;
        response = $"You voted for {votedPlayer.Nickname} ({Plugin.GetColorTypeByHex(colorHex)}).";
        return true;
    }

    public string[] Usage => ["Player's Color"];
}
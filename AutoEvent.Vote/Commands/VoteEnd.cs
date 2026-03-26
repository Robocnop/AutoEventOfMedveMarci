using System;
using System.Linq;
using CommandSystem;
using LabApi.Features.Permissions;

namespace AutoEvent.Vote.Commands;

internal class VoteEnd : ICommand
{
    public string Command => "end";
    public string Description => "Ends the currently running vote.";
    public string[] Aliases => [];

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (!sender.HasPermissions("ev.vote"))
        {
            response = "<color=red>You do not have permission to use this command!</color>";
            return false;
        }

        if (!VoteManager.IsRunning)
        {
            response = "There is no vote currently running.";
            return false;
        }

        if (!bool.TryParse(arguments.First(), out var runEvent))
        {
            response = "The first argument must be 'true' or 'false' to indicate whether to run the winning event.";
            return false;
        }


        VoteManager.EndVote(runEvent);
        response = "The vote has been ended.";
        return true;
    }
}
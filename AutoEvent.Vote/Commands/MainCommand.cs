using System;
using AutoEvent.Vote.ApiFeatures;
using CommandSystem;

namespace AutoEvent.Vote.Commands;

[CommandHandler(typeof(RemoteAdminCommandHandler))]
public class MainCommand : ParentCommand
{
    public MainCommand()
    {
        LoadGeneratedCommands();
    }

    public override string Command => "vote";
    public override string Description => "Main command for AutoEvent Vote Module";
    public override string[] Aliases => [];

    public sealed override void LoadGeneratedCommands()
    {
        try
        {
        }
        catch (Exception e)
        {
            LogManager.Error($"Caught an exception while registering commands.\n{e}");
        }
    }

    protected override bool ExecuteParent(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        response = "Please enter a valid subcommand: \n";
        foreach (var x in Commands)
        {
            var args = "";
            if (x.Value is IUsageProvider usage)
                foreach (var arg in usage.Usage)
                    args += $"[{arg}] ";

            if (sender is not ServerConsoleSender)
                response += $"<color=yellow> {x.Key} {args}<color=white>-> {x.Value.Description}. \n";
            else
                response += $" {x.Key} {args} -> {x.Value.Description}. \n";
        }

        return false;
    }
}
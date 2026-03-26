using System;
using LabApi.Events.Handlers;
using LabApi.Features;
using LabApi.Loader.Features.Plugins;
using MEC;

namespace AutoEvent.Vote;

public class AutoEventVote : Plugin<Config>
{
    public override string Name => "AutoEvent.Vote";
    public override string Description => "VoteSystem module for AutoEvent";
    public override string Author => "MedveMarci";
    public override Version Version { get; } = new(1, 0, 0);
    public override Version RequiredApiVersion { get; } = new(LabApiProperties.CompiledVersion);
    internal static AutoEventVote Singleton { get; private set; }

    public override void Enable()
    {
        Singleton = this;
        ServerEvents.RoundRestarted += () => Timing.KillCoroutines("VoteTimer");
    }

    public override void Disable()
    {
        ServerEvents.RoundRestarted -= () => Timing.KillCoroutines("VoteTimer");
        Singleton = null;
    }
}
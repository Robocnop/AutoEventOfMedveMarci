using System;
using System.Collections.Generic;
using System.Linq;
using AutoEvent.API;
using AutoEvent.Interfaces;
using AutoEvent.Vote.ApiFeatures;
using LabApi.Features.Wrappers;
using MEC;
using RadioMenuAPI;
using RadioMenuAPI.Extensions;
using UnityEngine;

namespace AutoEvent.Vote;

public static class VoteManager
{
    internal static bool IsRunning;
    private static List<Player> _players;

    internal static void StartVote(List<Event> events, int duration)
    {
        IsRunning = true;
        try
        {
            var ready = Player.List.Where(x => x.IsDummy || (x.IsPlayer && x.IsReady));

            var ignored = AutoEvent.Singleton.Config.IgnoredRoles;
            if (EventManager.CurrentEvent != null && ignored is { Count: > 0 })
                ready = ready.Where(x => !ignored.Contains(x.Role));
            _players = ready.ToList();
            var menu = new RadioMenu
            {
                Tag = "AutoEventVoteMenu",
                Title = "Vote for a minigame!",
                Items = events.Select(e => new RadioMenuItem(e.Name, e.Description)).ToList()
            };
            foreach (var player in _players)
                player.GiveRadioMenu(menu);
            Server.SendBroadcast($"Vote for a minigame! Check your radio menu. {duration} seconds remaining!", 5,
                Broadcast.BroadcastFlags.Normal, true);
            Timing.RunCoroutine(VoteTimer(duration), "VoteTimer");
        }
        catch (Exception e)
        {
            LogManager.Error($"An error occurred while starting the vote: {e.Message}");
            IsRunning = false;
        }
    }

    private static IEnumerator<float> VoteTimer(float duration)
    {
        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            yield return Timing.WaitForSeconds(1f);
            Server.SendBroadcast(
                $"Vote for a minigame! You can change with Right Mouse Click. {Math.Ceiling(duration - elapsed)} seconds remaining!",
                5, Broadcast.BroadcastFlags.Normal, true);
        }

        EndVote();
    }

    internal static void EndVote(bool runEvent = true)
    {
        var votes = new Dictionary<string, int>();
        foreach (var player in _players)
        {
            if (!player.IsReady) continue;
            var label = player.GetSelectedRadioMenuItem().Label;
            if (votes.TryGetValue(label, out var count))
                votes[label] = count + 1;
            else
                votes[label] = 1;
        }

        if (votes.Count == 0)
        {
            Server.SendBroadcast("The vote has ended with no votes cast.", 5, Broadcast.BroadcastFlags.Normal, true);
            return;
        }

        //If Draw
        var maxVotes = votes.Values.Max();
        var topEvents = votes.Where(kv => kv.Value == maxVotes).Select(kv => kv.Key).ToList();
        if (topEvents.Count > 1)
        {
            Server.SendBroadcast("The vote has ended in a tie between: " + string.Join(", ", topEvents), 5,
                Broadcast.BroadcastFlags.Normal, true);
            _players = null;
            IsRunning = false;
            return;
        }

        var winningEvent = votes.OrderByDescending(kv => kv.Value).First().Key;
        if (runEvent)
        {
            if (!EventManager.TryGetEvent(winningEvent, out var @event))
            {
                Server.SendBroadcast($"The vote has ended, but the winning event '{winningEvent}' could not be found.",
                    1);
                return;
            }

            Server.SendBroadcast($"The vote has ended! The winning minigame is: {@event.Name}", 5,
                Broadcast.BroadcastFlags.Normal, true);
            @event.StartEvent();
        }
        else
        {
            Server.SendBroadcast("The vote has ended by a Staff!", 5, Broadcast.BroadcastFlags.Normal, true);
        }

        _players = null;
        IsRunning = false;
    }
}
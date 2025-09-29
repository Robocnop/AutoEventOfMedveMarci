using System;
using System.Collections.Generic;
using System.Linq;
using AutoEvent.API;
using AutoEvent.Games.AmongUs.Features;
using CustomPlayerEffects;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;
using LabApiExtensions.FakeExtension;
using MEC;
using Mirror;
using ProjectMER.Features.Extensions;
using ProjectMER.Features.Serializable.Schematics;
using UnityEngine;
using PrimitiveObjectToy = AdminToys.PrimitiveObjectToy;

namespace AutoEvent.Games.AmongUs;

public class EventHandler(Plugin plugin)
{
    private static readonly List<Player> VentedPlayers = [];

    internal static bool TryParseToyName(string fullName, out string room, out string task)
    {
        room = null;
        task = null;
        if (string.IsNullOrEmpty(fullName)) return false;
        var parts = fullName.Split('_');
        if (parts.Length < 2 || parts[0] != "Task") return false;
        room = parts[1];
        task = parts.Length >= 3 ? parts[2] : null;
        return true;
    }

    //todo: Sabotages, make admin work
    public static void OnPlayerSearchedToy(PlayerSearchedToyEventArgs ev)
    {
        var name = ev.Interactable.GameObject.name;
        LogManager.Debug($"[OnPlayerSearchedToy] Player='{ev.Player.Nickname}' interacted with '{name}'");

        if (!TryParseToyName(name, out var room, out var tName))
        {
            LogManager.Debug("[OnPlayerSearchedToy] Name parse failed");
            return;
        }

        LogManager.Debug($"[OnPlayerSearchedToy] Parsed room='{room}' taskName='{tName ?? "null"}'");

        if (!TaskManager.TryGet(ev.Player, out var taskManager))
        {
            LogManager.Debug("[OnPlayerSearchedToy] TaskManager not found for player");
            return;
        }

        var task = taskManager.Tasks.FirstOrDefault(t =>
            (string.IsNullOrEmpty(tName) || t.Name.ToString() == tName) && t.RoomName.ToString() == room && !t.IsDone);

        if (task is not null)
        {
            LogManager.Debug(
                $"[OnPlayerSearchedToy] Found regular task '{task.Name}' in '{task.RoomName}' (isDone={task.IsDone})");
            task.IsDone = true;
            LogManager.Debug("[OnPlayerSearchedToy] Marked task done. Searching for next regular task...");
            var nextTask = taskManager.Tasks.FirstOrDefault(t =>
                (string.IsNullOrEmpty(tName) || t.Name.ToString() == tName) && t.RoomName.ToString() == room &&
                !t.IsDone);
            if (nextTask is not null)
            {
                LogManager.Debug(
                    $"[OnPlayerSearchedToy] Next regular task '{nextTask.Name}' found. Setting interactable.");
                ev.Interactable.Base.SetFakeInteractionDuration(ev.Player, TaskManager.GetLength(task));
                ev.Interactable.Base.SetInteractableToy(ev.Player, TaskManager.GetLength(task));
            }
            else
            {
                LogManager.Debug("[OnPlayerSearchedToy] No more regular tasks for this room/name.");
                ev.Interactable.Base.SetFakeIsLocked(ev.Player, true);
            }

            return;
        }

        LogManager.Debug("[OnPlayerSearchedToy] No regular task matched. Checking stage tasks...");
        var stageTask = TaskManager.GetPlayerStageTasks(ev.Player).FirstOrDefault(st =>
            (string.IsNullOrEmpty(tName) || st.Name.ToString() == tName) && st.RoomName.ToString() == room &&
            !st.IsDone);

        if (stageTask is null)
        {
            LogManager.Debug("[OnPlayerSearchedToy] No stage task found.");
            return;
        }

        LogManager.Debug(
            $"[OnPlayerSearchedToy] Found stage task '{stageTask.Name}' in '{stageTask.RoomName}' (isDone={stageTask.IsDone})");
        var nextStageTask = TaskManager.GetPlayerStageTasks(ev.Player).FirstOrDefault(st =>
            (string.IsNullOrEmpty(tName) || st.Name.ToString() == tName) && st.RoomName.ToString() == room &&
            !st.IsDone);

        if (nextStageTask is not null)
        {
            LogManager.Debug(
                $"[OnPlayerSearchedToy] Next stage task '{nextStageTask.Name}' found. Setting interactable.");
            ev.Interactable.Base.SetFakeInteractionDuration(ev.Player, TaskManager.GetLength(nextStageTask));
            ev.Interactable.Base.SetInteractableToy(ev.Player, TaskManager.GetLength(nextStageTask));
        }
        else
        {
            LogManager.Debug("[OnPlayerSearchedToy] No further stage tasks.");
            ev.Interactable.Base.SetFakeIsLocked(ev.Player, true);
        }

        stageTask.IsDone = true;
        LogManager.Debug("[OnPlayerSearchedToy] Marked stage task done.");
    }

    internal void OnPlayerChangingItem(PlayerChangingItemEventArgs ev)
    {
        if (Plugin.Instance.MeetingCalled)
        {
            ev.IsAllowed = false;
            return;
        }

        if (!plugin.KillCooldowns.TryGetValue(ev.Player, out var time)) return;
        if (time <= DateTime.UtcNow) return;
        ev.Player.SendHint(
            plugin.Translation.KillCooldown.Replace("{time}", (time - DateTime.UtcNow).Seconds.ToString()));
    }

    internal void OnPlayerHurting(PlayerHurtingEventArgs ev)
    {
        if (ev.Attacker == null || !plugin.Impostors.Contains(ev.Attacker)) return;
        if (plugin.Impostors.Contains(ev.Player)) return;
        ev.IsAllowed = false;
        if (Vector3.Distance(ev.Player.Position, ev.Attacker.Position) > plugin.Config.KillDistance)
        {
            ev.Attacker.SendHint(plugin.Translation.TooFar);
            return;
        }

        if (plugin.PlayerSkins.TryGetValue(ev.Player.NetworkId, out var skin))
        {
            var deathSkin = new SerializableSchematic
            {
                SchematicName = "DeathSkin",
                Position = ev.Player.Position,
                Rotation = ev.Player.Rotation.eulerAngles + new Vector3(0, -90, 0)
            }.LoadSchematic();

            foreach (var obj in deathSkin.AdminToyBases)
            {
                if (obj.name == "Bones") continue;
                if (!obj.TryGetComponent<PrimitiveObjectToy>(out var toy)) continue;
                if (!plugin.PlayerColors.TryGetValue(ev.Player.NetworkId, out var color)) continue;
                toy.NetworkMaterialColor = color.GetColorFromString();
            }

            NetworkServer.Destroy(skin);
            plugin.PlayerSkins[ev.Player.NetworkId] = deathSkin.gameObject;
        }
        
        if (plugin.Crewmates.Contains(ev.Player))
            plugin.Crewmates.Remove(ev.Player);
        
        if (plugin.Impostors.Contains(ev.Player))
            plugin.Impostors.Remove(ev.Player);
        
        ev.Player.Kill(plugin.Translation.KilledByImpostor);
        TaskManager.ClearForPlayers([ev.Player]);
        plugin.KillCooldowns[ev.Attacker] = DateTime.UtcNow.AddSeconds(plugin.Config.KillCooldown);
    }

    internal void OnShooting(PlayerShootingWeaponEventArgs ev)
    {
        if (!plugin.KillCooldowns.TryGetValue(ev.Player, out var time)) return;
        if (time <= DateTime.UtcNow) return;
        ev.IsAllowed = false;
        ev.Player.SendHint(
            plugin.Translation.KillCooldown.Replace("{time}", (time - DateTime.UtcNow).Seconds.ToString()));
    }

    internal void OnPlayerInteractedToy(PlayerInteractedToyEventArgs ev)
    {
        LogManager.Debug("Interacted with toy: " + ev.Interactable.GameObject.name);
        if (ev.Interactable.GameObject.name.StartsWith("Vent") && plugin.Impostors.Contains(ev.Player))
        {
            var parent = ev.Interactable.Parent?.parent.parent;
            if (parent is null) return;
            if (!parent.gameObject.TryGetComponent<Animator>(out var animator)) return;

            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (!state.IsName("Vent_Idle")) return;

            var vented = VentedPlayers.Contains(ev.Player);
            animator.Play(vented ? "Vent_Exit" : "Vent_Enter");

            if (vented)
            {
                VentedPlayers.Remove(ev.Player);
                ev.Player.DisableEffect<SilentWalk>();
            }
            else
            {
                VentedPlayers.Add(ev.Player);
                ev.Player.EnableEffect<SilentWalk>(255);
            }

            LogManager.Debug("PlayerPos_" + (vented ? "Exit" : "Enter"));
            var posTf = parent.Find("PlayerPos_" + (vented ? "Exit" : "Enter"));
            LogManager.Debug(posTf?.name ?? "null");
            if (posTf == null) return;

            Timing.KillCoroutines(ev.Player.NetworkId.ToString());
            Timing.RunCoroutine(VentCoroutine(ev.Player, animator, posTf.gameObject), ev.Player.NetworkId.ToString());
            return;
        }

        if (ev.Interactable.GameObject.name.StartsWith("Meeting"))
        {
            if (Plugin.Instance.MeetingCalled) return;

            if (plugin.PlayerMeetings.TryGetValue(ev.Player, out var meetings) &&
                meetings >= plugin.Config.EmergencyMeetings)
            {
                ev.Player.SendHint(plugin.Translation.EmergencyMeetingsReached);
                return;
            }

            if (plugin.MeetingCooldown > 0)
            {
                ev.Player.SendHint(
                    plugin.Translation.MeetingCooldown.Replace("{time}", plugin.MeetingCooldown.ToString()));
                return;
            }

            if (plugin.Impostors.Contains(ev.Player) || plugin.Crewmates.Contains(ev.Player))
            {
                if (!plugin.PlayerMeetings.ContainsKey(ev.Player))
                    plugin.PlayerMeetings.Add(ev.Player, 0);
                plugin.PlayerMeetings[ev.Player] += 1;
            }

            Plugin.Instance.MeetingCalled = true;
            foreach (var player in Player.ReadyList)
            {
                player.EnableEffect<Ensnared>();
                player.ClearInventory();
            }

            var ready = Player.ReadyList.ToList();
            var spawnCount = plugin.SpawnList.Count;
            for (var i = 0; i < ready.Count; i++)
            {
                var player = ready[i];
                player.DisableEffect<Ensnared>();
                player.Position = plugin.SpawnList[i % spawnCount].transform.position;
                player.EnableEffect<Ensnared>();

                if (Plugin.Instance.MeetingButton == null) continue;
                var direction = Plugin.Instance.MeetingButton.transform.position - player.Position;
                player.Rotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
            }

            Timing.RunCoroutine(plugin.BroadcastVotingCountdown(plugin.Translation.MeetingCalled), "BroadcastVotingCountdown");
        }

        if (ev.Interactable.GameObject.name == "ReportBody")
        {
            LogManager.Debug("ReportBody interacted");
           if (!plugin.PlayerSkins.ContainsValue(ev.Interactable.Parent?.parent.gameObject)) return;
           LogManager.Debug("Reported body is a player skin");
           if (!plugin.Impostors.Contains(ev.Player) && !plugin.Crewmates.Contains(ev.Player)) return;
           LogManager.Debug("Reporter is a valid player");
            var playerId = plugin.PlayerSkins.First(x => x.Value == ev.Interactable.Parent?.parent.gameObject).Key;
            var deadPlayer = Player.Get(playerId);
            if (deadPlayer is null) return;
            LogManager.Debug("Reported PlayerId: " + deadPlayer.Nickname);
            
            if (!plugin.PlayerColors.TryGetValue(deadPlayer.NetworkId, out var color)) return;
            if (!plugin.PlayerColors.TryGetValue(ev.Player.NetworkId, out var reportedColor)) return;
            if (Plugin.Instance.MeetingCalled) return;

            Plugin.Instance.MeetingCalled = true;
            foreach (var player in Player.ReadyList)
            {
                player.EnableEffect<Ensnared>();
                player.ClearInventory();
            }
            
            var ready = Player.ReadyList.ToList();
            var spawnCount = plugin.SpawnList.Count;
            for (var i = 0; i < ready.Count; i++)
            {
                var player = ready[i];
                player.DisableEffect<Ensnared>();
                player.Position = plugin.SpawnList[i % spawnCount].transform.position;
                player.EnableEffect<Ensnared>();

                if (Plugin.Instance.MeetingButton == null) continue;
                var direction = Plugin.Instance.MeetingButton.transform.position - player.Position;
                player.Rotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
            }
            Timing.RunCoroutine(plugin.BroadcastVotingCountdown(plugin.Translation.DeathBodyReported.Replace("{deadPlayer}", $"<color={color}>{deadPlayer.Nickname} {Plugin.GetColorTypeByHex(color)}</color>").Replace("{reportedPlayer}", $"<color={reportedColor}>{ev.Player.Nickname} {Plugin.GetColorTypeByHex(reportedColor)}</color>")), "BroadcastVotingCountdown");
        }
    }

    private static IEnumerator<float> VentCoroutine(Player player, Animator animator, GameObject playerPos)
    {
        yield return Timing.WaitForSeconds(0.05f);
        player.EnableEffect<Ensnared>();
        var initialClipName = animator.GetCurrentAnimatorClipInfo(0)[0].clip.name;

        while (true)
        {
            yield return Timing.WaitForOneFrame;
            var clipInfo = animator.GetCurrentAnimatorClipInfo(0);
            if (clipInfo[0].clip.name != initialClipName)
            {
                player.DisableEffect<Ensnared>();
                yield break;
            }

            player.Position = playerPos.transform.position;
        }
    }

    public void OnPlayerLeft(PlayerLeftEventArgs ev)
    {
        if (plugin.Crewmates.Contains(ev.Player))
            plugin.Crewmates.Remove(ev.Player);
        
        else if (plugin.Impostors.Contains(ev.Player))
        {
            plugin.Impostors.Remove(ev.Player);
            plugin.KillCooldowns.Remove(ev.Player);
            VentedPlayers.Remove(ev.Player);
        }

        TaskManager.ClearForPlayers([ev.Player]);
        if (plugin.PlayerSkins.TryGetValue(ev.Player.NetworkId, out var skin))
            NetworkServer.Destroy(skin);
        plugin.PlayerSkins.Remove(ev.Player.NetworkId);
    }
}
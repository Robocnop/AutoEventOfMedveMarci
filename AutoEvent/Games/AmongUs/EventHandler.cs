using System;
using System.Collections.Generic;
using System.Linq;
using AutoEvent.API;
using AutoEvent.ApiFeatures;
using AutoEvent.Games.AmongUs.Features;
using CustomPlayerEffects;
using InventorySystem.Items.Scp1509;
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
    internal static bool TryParseToyName(string fullName, out string room, out string task, out bool isTask,
        out bool isSabotage, out bool isTeleport)
    {
        room = null;
        task = null;
        isTask = false;
        isSabotage = false;
        isTeleport = false;
        if (string.IsNullOrEmpty(fullName)) return false;
        var parts = fullName.Split('_');
        if (parts.Length < 2) return false;
        if (parts[0] != "Task" && parts[0] != "Sabotage" && parts[0] != "Teleport") return false;
        isSabotage = parts[0] == "Sabotage";
        isTeleport = parts[0] == "Teleport";
        isTask = parts[0] == "Task";
        room = parts[1];
        if (parts.Length < 3) return true;
        task = parts[2];
        return true;
    }

    public void OnPlayerSearchingToy(PlayerSearchingToyEventArgs ev)
    {
        var name = ev.Interactable.GameObject.name;
        LogManager.Debug($"[OnPlayerSearchedToy] Player='{ev.Player.Nickname}' interacted with '{name}'");

        if (!TryParseToyName(name, out var room, out var tName, out var isTask, out var isSabotage, out var isTeleport))
        {
            LogManager.Debug("[OnPlayerSearchedToy] Name parse failed");
            return;
        }

        LogManager.Debug(
            $"[OnPlayerSearchingToy] Parsed room='{room}' taskName='{tName ?? "null"}' isSabotage='{isSabotage}' isTask='{isTask}' isTeleport='{isTeleport}'");

        if (isSabotage && plugin.CurrentSabotage == null)
        {
            ev.IsAllowed = false;
            return;
        }

        if (!isTask) return;

        if (!TaskManager.TryGet(ev.Player, out var tm))
        {
            ev.IsAllowed = false;
            return;
        }

        var hasMainTask = tm.Tasks.Any(t =>
            (string.IsNullOrEmpty(tName) || t.Name.ToString() == tName) &&
            t.RoomName.ToString() == room && !t.IsDone);

        var hasStageTask = TaskManager.GetPlayerStageTasks(ev.Player).Any(st =>
            (string.IsNullOrEmpty(tName) || st.Name.ToString() == tName) &&
            st.RoomName.ToString() == room && !st.IsDone);

        if (!hasMainTask && !hasStageTask)
            ev.IsAllowed = false;
    }

    public void OnPlayerSearchedToy(PlayerSearchedToyEventArgs ev)
    {
        var name = ev.Interactable.GameObject.name;
        LogManager.Debug($"[OnPlayerSearchedToy] Player='{ev.Player.Nickname}' interacted with '{name}'");

        if (!TryParseToyName(name, out var room, out var tName, out var isTask, out var isSabotage, out var isTeleport))
        {
            LogManager.Debug("[OnPlayerSearchedToy] Name parse failed");
            return;
        }

        LogManager.Debug(
            $"[OnPlayerSearchedToy] Parsed room='{room}' taskName='{tName ?? "null"}' isSabotage='{isSabotage}' isTask='{isTask}' isTeleport='{isTeleport}'");

        if (isSabotage)
            switch (room)
            {
                case "Lights":
                    foreach (var crewmate in plugin.Crewmates) crewmate.GetEffect<FogControl>()!.Intensity = 2;
                    plugin.CurrentSabotage?.Deactivate(plugin);
                    return;
                default:
                    LogManager.Debug($"[OnPlayerSearchedToy] {room} sabotage resolved.");
                    plugin.CurrentSabotage?.Deactivate(plugin);
                    return;
            }

        if (!isTask)
        {
            LogManager.Debug("[OnPlayerSearchedToy] Not a task, exiting.");
            return;
        }

        if (!TaskManager.TryGet(ev.Player, out var taskManager))
        {
            LogManager.Debug("[OnPlayerSearchedToy] TaskManager not found for player");
            return;
        }

        var regularTasks = taskManager.Tasks.Select(t =>
            $"{t.Name} (Room: {t.RoomName}, isDone: {t.IsDone}, isVisual: {t.IsVisual})");
        var stageTasks = TaskManager.GetPlayerStageTasks(ev.Player, true)
            .Select(st => $"{st.Name} (Room: {st.RoomName}, isDone: {st.IsDone}, Stage)");
        LogManager.Debug("Player tasks: " + string.Join("\n", regularTasks.Concat(stageTasks)));

        var task = taskManager.Tasks.FirstOrDefault(t =>
            (string.IsNullOrEmpty(tName) || t.Name.ToString() == tName) && t.RoomName.ToString() == room && !t.IsDone);

        if (task is not null)
        {
            LogManager.Debug(
                $"[OnPlayerSearchedToy] Found regular task '{task.Name}' in '{task.RoomName}' (isDone={task.IsDone}, isVisual={task.IsVisual})");
            task.IsDone = true;
            if (task.IsVisual)
            {
                var animator = ev.Interactable.Parent?.GetComponent<Animator>();
                if (animator != null)
                    animator.Play($"{task.Name}Task");
            }

            if (task.StageTasks is { Count: > 0 } && plugin.TaskToyList != null)
            {
                foreach (var st in task.StageTasks)
                {
                    foreach (var stageToy in plugin.TaskToyList)
                    {
                        if (!TryParseToyName(stageToy.name, out var stRoom, out var stName, out var stIsTask, out _, out _))
                            continue;
                        if (!stIsTask) continue;
                        if ((!string.IsNullOrEmpty(stName) && st.Name.ToString() != stName) ||
                            st.RoomName.ToString() != stRoom)
                            continue;
                        LogManager.Debug($"[OnPlayerSearchedToy] Unlocking stage toy '{stageToy.name}' for player '{ev.Player.Nickname}'");
                        stageToy.SetFakeIsLocked(ev.Player, false);
                    }
                }
            }

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

        stageTask.IsDone = true;
        LogManager.Debug("[OnPlayerSearchedToy] Marked stage task done.");

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
        }

        var hasMoreRegularTasks = taskManager.Tasks.Any(t =>
            (string.IsNullOrEmpty(tName) || t.Name.ToString() == tName) && t.RoomName.ToString() == room && !t.IsDone);
        var hasMoreStageTasks = TaskManager.GetPlayerStageTasks(ev.Player, true).Any(st =>
            (string.IsNullOrEmpty(tName) || st.Name.ToString() == tName) && st.RoomName.ToString() == room &&
            !st.IsDone);
        if (hasMoreRegularTasks || hasMoreStageTasks) return;
        LogManager.Debug("[OnPlayerSearchedToy] No more tasks (regular or stage) for this interactable. Locking it.");
        ev.Interactable.Base.SetFakeIsLocked(ev.Player, true);
    }

    internal void OnPlayerChangingItem(PlayerChangingItemEventArgs ev)
    {
        if (Plugin.Instance.MeetingCalled)
        {
            ev.IsAllowed = false;
            return;
        }

        LogManager.Debug("PlayerChangingItem: " + ev.Player.Nickname);
        if (ev.NewItem != null && plugin.ImpostorRadioItems.Contains(ev.NewItem.Serial) &&
            plugin.Impostors.Contains(ev.Player))
        {
            LogManager.Debug("Player switched to impostor radio item.");
            if (!plugin.Radios.ContainsKey(ev.Player))
                plugin.Radios[ev.Player] = 0;
            if (!plugin.Radios.TryGetValue(ev.Player, out var index)) return;
            var sabotage = plugin.CurrentSabotages[index % plugin.CurrentSabotages.Count];
            LogManager.Debug("Current sabotage: " + (sabotage != null ? sabotage.Type.ToString() : "null"));
            return;
        }

        if (ev.OldItem != null && plugin.ImpostorRadioItems.Contains(ev.OldItem.Serial) &&
            plugin.Impostors.Contains(ev.Player))
        {
            LogManager.Debug("Player switched from impostor radio item.");
            plugin.Radios.Remove(ev.Player);
            ev.Player.SendHint("");
            return;
        }

        if (!plugin.KillCooldowns.TryGetValue(ev.Player, out var time)) return;
        if (time <= DateTime.UtcNow) return;
        ev.Player.SendHint(
            plugin.Translation.KillCooldown.Replace("{time}", (time - DateTime.UtcNow).Seconds.ToString()));
    }

    internal void OnPlayerHurting(PlayerHurtingEventArgs ev)
    {
        if (ev.Attacker == null) return;
        if (ev.DamageHandler is not Scp1509DamageHandler) return;
        ev.IsAllowed = false;
        if (plugin.KillCooldowns.TryGetValue(ev.Attacker, out var time) && time > DateTime.UtcNow)
        {
            ev.IsAllowed = false;
            ev.Attacker.SendHint(
                plugin.Translation.KillCooldown.Replace("{time}", (time - DateTime.UtcNow).Seconds.ToString()));
            return;
        }

        if (plugin.Impostors.Contains(ev.Player)) return;
        if (!plugin.Impostors.Contains(ev.Attacker)) return;

        if (plugin.PlayerSkins.TryGetValue(ev.Player.NetworkId, out var skin) && skin != null)
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

    internal void OnPlayerInteractedToy(PlayerInteractedToyEventArgs ev)
    {
        if (ev?.Interactable == null || ev.Interactable.GameObject == null)
        {
            LogManager.Debug("OnPlayerInteractedToy called with null Interactable or GameObject");
            return;
        }

        LogManager.Debug("Interacted with toy: " + ev.Interactable.GameObject.name);
        if (ev.Interactable.GameObject.name.StartsWith("Vent") && plugin.Impostors.Contains(ev.Player))
        {
            var parent = ev.Interactable.Parent?.parent.parent;
            if (parent is null) return;
            if (!parent.gameObject.TryGetComponent<Animator>(out var animator)) return;

            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (!state.IsName("Vent_Idle")) return;

            var vented = plugin.VentedPlayers.Contains(ev.Player);
            animator.Play(vented ? "Vent_Exit" : "Vent_Enter");

            if (vented)
            {
                plugin.VentedPlayers.Remove(ev.Player);
                ev.Player.DisableEffect<SilentWalk>();
                ev.Player.DisableEffect<Lightweight>();
                ev.Player.DisableEffect<MovementBoost>();
            }
            else
            {
                plugin.VentedPlayers.Add(ev.Player);
                ev.Player.EnableEffect<SilentWalk>(255);
                ev.Player.EnableEffect<Lightweight>(100);
                ev.Player.EnableEffect<MovementBoost>(50);
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

            if (plugin.CurrentSabotage is { EnabledMeetings: false })
            {
                ev.Player.SendHint(plugin.Translation.CannotCallMeetingDuringSabotage);
                return;
            }

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
            var ready = Player.ReadyList.ToList();
            var spawnCount = plugin.SpawnList.Count;
            for (var i = 0; i < ready.Count; i++)
            {
                var player = ready[i];
                if (Plugin.Instance.MeetingButton == null) continue;

                var spawnPos = plugin.SpawnList[i % spawnCount].transform.position;
                var meetingPos = Plugin.Instance.MeetingButton.transform.position;

                player.ClearInventory();
                player.Position = spawnPos;
                player.EnableEffect<Ensnared>();

                if (!player.IsAlive)
                    if (plugin.PlayerSkins.TryGetValue(player.NetworkId, out var skin) && skin != null &&
                        skin.name.Contains("Death"))
                    {
                        skin.transform.position = spawnPos;
                        var skinDirection = meetingPos - skin.transform.position;
                        skin.transform.rotation =
                            Quaternion.LookRotation(new Vector3(skinDirection.x, 0, skinDirection.z));
                        continue;
                    }

                var direction = meetingPos - player.Position;
                player.Rotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
            }

            if (!plugin.PlayerColors.TryGetValue(ev.Player.NetworkId, out var color)) return;
            Timing.RunCoroutine(
                plugin.BroadcastVotingCountdown(
                    plugin.Translation.MeetingCalled.Replace("{player}",
                        $"<color={color}>{ev.Player.Nickname} {Plugin.GetColorTypeByHex(color)}</color>"), ev.Player),
                "BroadcastVotingCountdown");
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

            var ready = Player.ReadyList.ToList();
            var spawnCount = plugin.SpawnList.Count;
            for (var i = 0; i < ready.Count; i++)
            {
                var player = ready[i];
                if (Plugin.Instance.MeetingButton == null) continue;

                var spawnPos = plugin.SpawnList[i % spawnCount].transform.position;
                var meetingPos = Plugin.Instance.MeetingButton.transform.position;

                if (!player.IsAlive)
                {
                    if (plugin.PlayerSkins.TryGetValue(player.NetworkId, out var skin) && skin != null &&
                        skin.name.Contains("Death"))
                    {
                        skin.transform.position = spawnPos;
                        var skinDirection = meetingPos - skin.transform.position;
                        skin.transform.rotation =
                            Quaternion.LookRotation(new Vector3(skinDirection.x, 0, skinDirection.z));
                    }

                    continue;
                }

                player.ClearInventory();
                player.Position = spawnPos;
                player.EnableEffect<Ensnared>();
                player.DisableEffect<Lightweight>();

                var direction = meetingPos - player.Position;
                player.Rotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
            }

            Timing.RunCoroutine(
                plugin.BroadcastVotingCountdown(
                    plugin.Translation.DeathBodyReported
                        .Replace("{deadPlayer}",
                            $"<color={color}>{deadPlayer.Nickname} {Plugin.GetColorTypeByHex(color)}</color>")
                        .Replace("{reportedPlayer}",
                            $"<color={reportedColor}>{ev.Player.Nickname} {Plugin.GetColorTypeByHex(reportedColor)}</color>"),
                    ev.Player), "BroadcastVotingCountdown");
        }

        if (ev.Interactable.GameObject.name.StartsWith("Teleport") && plugin.Impostors.Contains(ev.Player))
        {
            var room = ev.Interactable.GameObject.name.Split('_')[1];
            if (plugin.TeleportOutList.TryGetValue(room, out var position))
            {
                ev.Player.Position = position;
                LogManager.Debug($"[OnPlayerSearchedToy] Teleported player to '{room}' at position {position}.");
            }
            else
            {
                LogManager.Debug($"[OnPlayerSearchedToy] Teleport position for room '{room}' not found.");
            }
        }
    }

    public void OnPlayerTogglingRadioEventArgs(PlayerTogglingRadioEventArgs ev)
    {
        LogManager.Debug("PlayerUsingRadio: " + ev.Player.Nickname);
        if (!plugin.ImpostorRadioItems.Contains(ev.RadioItem.Serial)) return;
        LogManager.Debug("Player is using impostor radio item.");
        if (!plugin.Radios.TryGetValue(ev.Player, out var index)) return;
        LogManager.Debug("Current radio index: " + index);
        var sabotage = plugin.CurrentSabotages[index % plugin.CurrentSabotages.Count];
        LogManager.Debug("Current sabotage: " + (sabotage != null ? sabotage.Type.ToString() : "null"));
        if (sabotage == null)
        {
            LogManager.Debug("Sabotage is null, not activating.");
            return;
        }

        var success = sabotage.TryActivate(ev.Player, plugin, out var reason);
        if (!success)
            ev.Player.SendBroadcast(reason, 2, shouldClearPrevious: true);
        ev.IsAllowed = false;
    }

    public static void OnPlayerUsingRadioEventArgs(PlayerUsingRadioEventArgs ev)
    {
        ev.IsAllowed = false;
    }

    public void OnPlayerChangingRadioRange(PlayerChangingRadioRangeEventArgs ev)
    {
        LogManager.Debug("PlayerChangingRadioRange: " + ev.Player.Nickname);
        if (!plugin.ImpostorRadioItems.Contains(ev.RadioItem.Serial)) return;
        LogManager.Debug("Player is using impostor radio item.");
        if (!plugin.Radios.TryGetValue(ev.Player, out var index)) return;
        LogManager.Debug("Current radio index: " + index);
        index = (index + 1) % plugin.CurrentSabotages.Count;
        plugin.Radios[ev.Player] = index;
        LogManager.Debug($"Changed radio index to {index} for player {ev.Player.Nickname}");
        var sabotage = plugin.CurrentSabotages[index];
        LogManager.Debug("Current sabotage: " + (sabotage != null ? sabotage.Type.ToString() : "null"));
        ev.IsAllowed = false;
    }

    private static IEnumerator<float> VentCoroutine(Player player, Animator animator, GameObject playerPos)
    {
        yield return Timing.WaitForSeconds(0.05f);
        player.EnableEffect<Ensnared>();

        var clips = animator.GetCurrentAnimatorClipInfo(0);
        if (clips.Length == 0)
        {
            player.DisableEffect<Ensnared>();
            yield break;
        }

        var initialClipName = clips[0].clip.name;

        while (true)
        {
            yield return Timing.WaitForOneFrame;
            var clipInfo = animator.GetCurrentAnimatorClipInfo(0);
            if (clipInfo.Length == 0 || clipInfo[0].clip.name != initialClipName)
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

        if (plugin.Muted.Contains(ev.Player))
        {
            ev.Player.Unmute(true);
            plugin.Muted.Remove(ev.Player);
        }

        if (plugin.Impostors.Contains(ev.Player))
        {
            plugin.Impostors.Remove(ev.Player);
            plugin.KillCooldowns.Remove(ev.Player);
            plugin.VentedPlayers.Remove(ev.Player);
        }

        if (plugin.PlayerTextToys.TryGetValue(ev.Player.NetworkId, out var textToy))
        {
            textToy.Destroy();
            plugin.PlayerTextToys.Remove(ev.Player.NetworkId);
        }

        TaskManager.ClearForPlayers([ev.Player]);
        if (plugin.PlayerSkins.TryGetValue(ev.Player.NetworkId, out var skin))
            NetworkServer.Destroy(skin);
        plugin.PlayerSkins.Remove(ev.Player.NetworkId);
    }
}
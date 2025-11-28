using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AdminToys;
using AutoEvent.API;
using AutoEvent.API.Enums;
using AutoEvent.Games.AmongUs.Configs;
using AutoEvent.Games.AmongUs.Enums;
using AutoEvent.Games.AmongUs.Features;
using AutoEvent.Games.AmongUs.Skeld;
using AutoEvent.Interfaces;
using CustomPlayerEffects;
using LabApi.Events.Handlers;
using LabApi.Features.Wrappers;
using LabApiExtensions.FakeExtension;
using MEC;
using Mirror;
using NorthwoodLib.Pools;
using ProjectMER.Features.Extensions;
using ProjectMER.Features.Serializable.Schematics;
using UnityEngine;
using Extensions = AutoEvent.API.Extensions;
using LightSourceToy = AdminToys.LightSourceToy;
using PrimitiveObjectToy = AdminToys.PrimitiveObjectToy;
using Random = System.Random;
using TextToy = LabApi.Features.Wrappers.TextToy;

namespace AutoEvent.Games.AmongUs;

public class Plugin : Event<Configs.Config, Translation>, IEventMap
{
    internal readonly List<Player> Muted = [];
    private EventHandler _eventHandler;
    internal List<Player> Crewmates = [];
    internal List<Player> Impostors = [];
    internal bool MeetingCalled;
    internal int MeetingCooldown;
    public override string Name { get; set; } = "Among Us";
    public override string Description { get; set; } = "The Impostor is among us.";
    public override string Author { get; set; } = "MedveMarci";
    public override string CommandName { get; set; } = "amongus";

    public override EventFlags EventHandlerSettings { get; set; } =
        EventFlags.IgnoreBulletHole | EventFlags.IgnoreRagdoll |
        EventFlags.IgnoreDroppingAmmo | EventFlags.IgnoreDroppingItem | EventFlags.IgnoreHandcuffing;

    protected override FriendlyFireSettings ForceEnableFriendlyFire { get; set; } = FriendlyFireSettings.Enable;
    internal List<GameObject> SpawnList { get; private set; }
    private AdminToyBase VentObject { get; set; }
    internal List<LightSourceToy> LightToys { get; private set; } = [];
    internal List<PrimitiveObjectToy> DoorList { get; private set; }
    private List<InvisibleInteractableToy> TaskToyList { get; set; }
    internal ConcurrentDictionary<string, Vector3> TeleportOutList { get; private set; }
    internal InvisibleInteractableToy MeetingButton { get; private set; }
    internal Dictionary<uint, GameObject> PlayerSkins { get; private set; }
    internal Dictionary<uint, uint> PlayerVotes { get; private set; }
    internal Dictionary<uint, string> PlayerColors { get; private set; }
    internal Dictionary<uint, TextToy> PlayerTextToys { get; private set; }
    
    private Dictionary<string, List<Task>> GeneratedTasks { get; set; }
    private Dictionary<string, List<Sabotage>> GeneratedSabotages { get; set; }
    internal List<Sabotage> CurrentSabotages { get; private set; }
    internal Dictionary<Player, DateTime> KillCooldowns { get; private set; } = new();
    internal Dictionary<Player, int> PlayerMeetings { get; private set; } = new();
    internal List<ushort> ImpostorRadioItems { get; private set; } = [];
    
    internal List<Player> VentedPlayers = [];
    internal readonly Dictionary<Player, int> Radios = [];
    internal Sabotage CurrentSabotage { get; set; }
    internal DateTime LastActivated { get; set; }

    private string Result { get; set; } = string.Empty;
    internal static Plugin Instance { get; private set; }

    public MapInfo MapInfo { get; set; } = new()
    {
        MapName = "Skeld",
        Position = new Vector3(0, 30, 30)
    };

    protected override void RegisterEvents()
    {
        _eventHandler = new EventHandler(this);
        PlayerEvents.SearchedToy += _eventHandler.OnPlayerSearchedToy;
        PlayerEvents.Hurting += _eventHandler.OnPlayerHurting;
        PlayerEvents.ChangingItem += _eventHandler.OnPlayerChangingItem;
        PlayerEvents.InteractedToy += _eventHandler.OnPlayerInteractedToy;
        PlayerEvents.Left += _eventHandler.OnPlayerLeft;
        PlayerEvents.ChangingRadioRange += _eventHandler.OnPlayerChangingRadioRange;
        PlayerEvents.TogglingRadio += _eventHandler.OnPlayerTogglingRadioEventArgs;
        PlayerEvents.UsingRadio += EventHandler.OnPlayerUsingRadioEventArgs;
        PlayerEvents.SearchingToy += _eventHandler.OnPlayerSearchingToy;
    }

    protected override void UnregisterEvents()
    {
        PlayerEvents.SearchedToy -= _eventHandler.OnPlayerSearchedToy;
        PlayerEvents.Hurting -= _eventHandler.OnPlayerHurting;
        PlayerEvents.ChangingItem -= _eventHandler.OnPlayerChangingItem;
        PlayerEvents.InteractedToy -= _eventHandler.OnPlayerInteractedToy;
        PlayerEvents.Left -= _eventHandler.OnPlayerLeft;
        PlayerEvents.ChangingRadioRange -= _eventHandler.OnPlayerChangingRadioRange;
        PlayerEvents.TogglingRadio -= _eventHandler.OnPlayerTogglingRadioEventArgs;
        PlayerEvents.UsingRadio -= EventHandler.OnPlayerUsingRadioEventArgs;
        PlayerEvents.SearchingToy -= _eventHandler.OnPlayerSearchingToy;
        _eventHandler = null;
    }

    protected override void OnStart()
    {
        Instance = this;
        GenerateTasks();
        GenerateSabotages();
        Impostors.Clear();
        Crewmates.Clear();
        TaskManager.ClearForPlayers(Player.ReadyList);
        KillCooldowns = new Dictionary<Player, DateTime>();
        PlayerMeetings = new Dictionary<Player, int>();
        ImpostorRadioItems = [];
        SpawnList = [];
        DoorList = [];
        PlayerSkins = new Dictionary<uint, GameObject>();
        PlayerVotes = new Dictionary<uint, uint>();
        PlayerColors = new Dictionary<uint, string>();
        PlayerTextToys = new Dictionary<uint, TextToy>();
        MeetingCalled = false;
        MeetingCooldown = Config.EmergencyCooldown;
        VentedPlayers = [];
        LastActivated = DateTime.MinValue;

        foreach (var adminToyBase in MapInfo.Map.AdminToyBases)
        {
            if (adminToyBase.name.Contains("Spawnpoint"))
            {
                SpawnList.Add(adminToyBase.gameObject);
            }
            else if (adminToyBase.name.Contains("Door_") &&
                     adminToyBase.TryGetComponent<PrimitiveObjectToy>(out var door))
            {
                DoorList.Add(door);
            }
            else if (adminToyBase.name.Contains("MeetingButton") &&
                     adminToyBase.TryGetComponent<InvisibleInteractableToy>(out var meetingButton))
            {
                MeetingButton = meetingButton;
            }
            else if (adminToyBase.name == "Task_MedBay_Scan")
            {
                adminToyBase.gameObject.AddComponent<ScanComponent>();
            }
            else if (adminToyBase.name.Contains("Task_") &&
                     adminToyBase.TryGetComponent<InvisibleInteractableToy>(out var invisibleInteractableToy))
            {
                TaskToyList ??= [];
                TaskToyList.Add(invisibleInteractableToy);
            }
            else if (adminToyBase.name == "VentObject")
            {
                VentObject = adminToyBase;
            }
            else if (adminToyBase is LightSourceToy lightSourceToy)
            {

                LightToys ??= [];
                LightToys.Add(lightSourceToy);
            }
            else if (adminToyBase.name.Contains("Teleport_") && adminToyBase.name.Contains("_Out"))
            {
                var parts = adminToyBase.name.Split('_');
                var key = parts[1];
                TeleportOutList ??= new ConcurrentDictionary<string, Vector3>();
                TeleportOutList[key] = adminToyBase.transform.position;
            }
        }

        Impostors = Config.Impostors.GetPlayers();
        var ready = Player.ReadyList.ToList();
        Crewmates = ready.Except(Impostors).ToList();

        var spawnCount = SpawnList.Count;
        for (var i = 0; i < ready.Count; i++)
        {
            var player = ready[i];

            var hex = "#" + Misc.AcceptedColours[i % Misc.AcceptedColours.Length];
            var color = hex.GetColorFromString();
            player.GiveLoadout(Config.Loadout);
            player.EnableEffect<HeavyFooted>(255);
            player.EnableEffect<Ensnared>(255);
            player.Position = SpawnList[i % spawnCount].transform.position;

            var skin = new SerializableSchematic
            {
                SchematicName = "PlayerSkin",
                Position = player.Position,
                Scale = Vector3.one
            }.LoadSchematic();
            player.DestroySchematic(skin);

            foreach (var obj in skin.AdminToyBases)
            {
                if (obj.name == "Eyes") continue;
                if (!obj.TryGetComponent<PrimitiveObjectToy>(out var toy)) continue;
                toy.syncInterval = 0;
                toy.NetworkMaterialColor = color;
            }

            skin.gameObject.transform.parent = player.GameObject?.transform;
            skin.gameObject.transform.localRotation = Quaternion.Euler(0, -90, 0);

            if (MeetingButton != null)
            {
                var direction = MeetingButton.transform.position - player.Position;
                player.Rotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
            }

            var colorType = GetColorTypeByHex(hex);
            if (colorType != null) player.DisplayName = player.Nickname + " " + colorType;
            LogManager.Debug(hex);
            PlayerColors[player.NetworkId] = hex;
            PlayerSkins[player.NetworkId] = skin.gameObject;
        }
    }

    protected override IEnumerator<float> BroadcastStartCountdown()
    {
        for (float time = 5; time > 0; time--)
        {
            Extensions.ServerBroadcast(
                Translation.StartingEvent.Replace("{name}", Name).Replace("{time}", $"{time}"), 1);
            yield return Timing.WaitForSeconds(1f);
        }
    }

    protected override void CountdownFinished()
    {
        foreach (var impostor in Impostors)
        {
            impostor.Broadcast(Translation.YouAreImpostor);
            impostor.DisableEffect<Ensnared>();
            impostor.DisableEffect<HeavyFooted>();
            impostor.GetEffect<FogControl>()!.Intensity = 3;
            impostor.AddItem(ItemType.SCP1509);
            var radio = impostor.AddItem(ItemType.Radio);
            if (radio != null)
                ImpostorRadioItems.Add(radio.Serial);
            impostor.DestroyNetworkIdentity(VentObject.netIdentity);
            foreach (var invisibleInteractable in TaskToyList) invisibleInteractable.SetFakeIsLocked(impostor, true);
        }


        foreach (var crewmate in Crewmates)
        {
            crewmate.Broadcast(Translation.YouAreCrewmate);
            crewmate.DisableEffect<Ensnared>();
            crewmate.DisableEffect<HeavyFooted>();
        }

        CreateTasksForPlayers(Crewmates);
        CurrentSabotages = GeneratedSabotages[MapInfo.MapName];
    }

    internal IEnumerator<float> BroadcastVotingCountdown(string reason = "", Player starter = null)
    {
        var time = Config.VotingTime;
        var discussionTime = Config.DiscussionTime;
        var shortened = false;
        LogManager.Debug("reason: " + reason);
        foreach (var player in Player.ReadyList.Where(p => Impostors.Contains(p) || Crewmates.Contains(p)))
        {
            if (player == starter)
                continue;

            if (Muted.Contains(player)) continue;
            Muted.Add(player);
            player.Mute();
        }

        while (time > 0)
        {
            if (discussionTime > 0)
            {
                foreach (var player in Player.ReadyList.Where(p => Impostors.Contains(p) || Crewmates.Contains(p)))
                    player.Broadcast(
                        Translation.DiscussionInfo.Replace("{reason}", reason).Replace("{time}",
                            discussionTime.ToString(CultureInfo.InvariantCulture)));
                yield return Timing.WaitForSeconds(1f);
                discussionTime--;
                continue;
            }
            
            if (discussionTime == 0)
            {
                LogManager.Debug("Unmuting players");
                foreach (var player in Muted)
                {
                    player.Unmute(true);
                    player.Unmute(false);
                }
                Muted.Clear();
                discussionTime--;
            }

            var playersCount = Impostors.Count(p => p.IsAlive) + Crewmates.Count(p => p.IsAlive);

            if (!shortened && playersCount > 0 && PlayerVotes.Count >= playersCount && time > 5)
            {
                time = 5;
                shortened = true;
            }

            foreach (var player in Player.ReadyList.Where(p => Impostors.Contains(p) || Crewmates.Contains(p)))
                player.Broadcast(
                    Translation.VotingInfo.Replace("{reason}", reason).Replace("{time}",
                        time.ToString(CultureInfo.InvariantCulture)));

            yield return Timing.WaitForSeconds(1f);
            time--;
        }

        MeetingCalled = false;
        var maxVotes = PlayerVotes.Values
            .GroupBy(v => v)
            .OrderByDescending(g => g.Count())
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToList();
        LogManager.Debug(
            $"Max votes count: {maxVotes.Count}, max votes: {(maxVotes.Count > 0 ? maxVotes[0].Count : 0)}");

        switch (maxVotes.Count)
        {
            case 0:
                Extensions.ServerBroadcast($"{Translation.NoOneVotedOut}", 5);
                break;
            case > 1 when maxVotes[0].Count == maxVotes[1].Count:
                Extensions.ServerBroadcast($"{Translation.ItsATie} {Translation.NoOneVotedOut}", 5);
                break;
            default:
            {
                if (maxVotes[0].Id == 0)
                {
                    Extensions.ServerBroadcast($"{Translation.NoOneVotedOut}", 5);
                    break;
                }

                var votedOut = Player.Get(maxVotes[0].Id);
                if (votedOut != null)
                {
                    votedOut.Kill($"{Translation.DeathMessage}");
                    votedOut.DisplayName = string.Empty;
                    TaskManager.ClearForPlayers([votedOut]);

                    PlayerColors.Remove(votedOut.NetworkId);
                    Impostors.Remove(votedOut);
                    Crewmates.Remove(votedOut);
                    KillCooldowns.Remove(votedOut);

                    if (Config.ConfirmEjects)
                    {
                        Extensions.ServerBroadcast(
                            Impostors.Contains(votedOut)
                                ? $"{Translation.WasAnImpostor.Replace("{player}", votedOut.Nickname)}"
                                : $"{Translation.WasNotAnImpostor.Replace("{player}", votedOut.Nickname)}", 5);
                    }
                    else
                    {
                        Extensions.ServerBroadcast(
                            $"{Translation.VotedOut.Replace("{player}", votedOut.Nickname)}", 5);
                    }
                }

                break;
            }
        }

        PlayerVotes.Clear();
        foreach (var textToy in PlayerTextToys.Values)
            textToy.Destroy();
        PlayerTextToys.Clear();
        LogManager.Debug("Voting ended, cleared votes and text toys");
        ImpostorRadioItems.Clear();
        foreach (var player in Player.ReadyList)
        {
            if (Impostors.Contains(player))
            {
                player.AddItem(ItemType.SCP1509);
                var radio = player.AddItem(ItemType.Radio);
                if (radio != null)
                    ImpostorRadioItems.Add(radio.Serial);
            }

            player.DisableEffect<Ensnared>();
        }

        foreach (var pair in PlayerSkins.Where(skin => skin.Value.name.Contains("DeathSkin")))
        {
            var skin = pair.Value;
            if (skin == null) continue;
            LogManager.Debug($"Hiding death skin for {pair.Key}");
            skin.transform.position = new Vector3(0, -1000, 0);
        }
    }

    protected override void ProcessFrame()
    {
        if (CurrentSabotage is { IsCritical: true })
        {
            CurrentSabotage.Timer--;
        }
        
        if (MeetingCalled)
        {
            var participants = Crewmates.Concat(Impostors);
            foreach (var player in participants)
            {
                if (!PlayerTextToys.TryGetValue(player.NetworkId, out var textToy))
                {
                    LogManager.Debug($"Creating text toy for {player.Nickname}");
                    textToy = TextToy.Create(player.GameObject?.transform);
                    textToy.GameObject.transform.localPosition += new Vector3(0, 2, 0);
                    textToy.Rotation = Quaternion.Euler(0, 180, 0);
                    PlayerTextToys[player.NetworkId] = textToy;
                }

                var votes = PlayerVotes.Where(v => v.Value == player.NetworkId).Select(v => v.Key).ToList();
                string text;
                if (votes.Count > 0)
                {
                    text = Config.AnonymousVotes
                        ? $"{votes.Count} {Translation.Vote}"
                        : $"{string.Join(", ", votes.Select(id => Player.Get(id)?.Nickname ?? id.ToString()))}\n{votes.Count} {Translation.Vote}";
                }
                else
                    text = Translation.NoVotes;

                var didntVote = Impostors.Concat(Crewmates)
                    .Where(p => !PlayerVotes.ContainsKey(p.NetworkId) || PlayerVotes[p.NetworkId] == 0)
                    .ToList();
                var hintText = text;
                if (didntVote.Count > 0)
                {
                    hintText += $"\n{Translation.DidntVote}:\n";
                    hintText += string.Join(", ", didntVote.Select(p => PlayerColors.TryGetValue(p.NetworkId, out var hex) ? (
                        $"<color={hex}>{p.DisplayName}</color>").Replace("*", "") : p.DisplayName.Replace("*", "")));
                }
                
                textToy.TextFormat = $"<size=10>{text}</size>";
                player.SendHint(hintText, 1.25f);
            }
            
            MeetingCooldown = Config.EmergencyCooldown;
            return;
        }

        if (MeetingCooldown != 0)
            MeetingCooldown -= 1;

        var sb = StringBuilderPool.Shared.Rent();
        foreach (var player in Player.ReadyList)
        {
            sb.Clear();
            if (TaskManager.TryGet(player, out var tm) && Crewmates.Contains(player))
            {
                sb.AppendLine($"{Translation.Tasks}:");
                if (CurrentSabotage is { Type: SabotageType.CommsSabotage })
                {
                    var flashColor = Time.time % 1f < 0.5f ? "red" : "yellow";
                    sb.AppendLine($"<color={flashColor}>{Translation.CommsSabotaged}</color>");
                    player.SendHint(sb.ToString(), 1.25f);
                    continue;
                }
                foreach (var mt in tm.Tasks)
                {
                    var hasStages = mt.StageTasks is { Count: > 0 };
                    var isCompleted = TaskManager.IsTaskDone(mt);

                    if (hasStages)
                    {
                        var max = mt.StageTasks.Count + 1;
                        var done = mt.StageTasks.Count(s => s.IsDone);
                        if (mt.IsDone) done++;
                        var currentStage = mt.StageTasks.FirstOrDefault(s => !s.IsDone) ?? mt.StageTasks.Last();
                        var currentIndex = isCompleted ? max : done;

                        var description = mt.Description.Replace("{roomName}", mt.RoomName.ToString());
                        var mainLine = $"{mt.RoomName} ({currentIndex}/{max}): {description}";
                        var stageLine = $"{currentStage.RoomName} ({currentIndex}/{max}): {currentStage.Description}";

                        if (!mt.IsDone)
                        {
                            sb.AppendLine(mainLine);
                            continue;
                        }

                        if (TaskManager.IsTaskDone(mt))
                        {
                            sb.AppendLine($"<color=green>{stageLine}</color>");
                            continue;
                        }

                        sb.AppendLine(currentIndex > 0 ? $"<color=yellow>{stageLine}</color>" : stageLine);
                    }
                    else
                    {
                        var description = mt.Description.Replace("{roomName}", mt.RoomName.ToString());
                        var line = $"{mt.RoomName}: {description}";
                        if (isCompleted)
                            line = $"<color=green>{line}</color>";
                        sb.AppendLine(line);
                    }
                }

                player.SendHint(sb.ToString(), 1.25f);
            } else if (Impostors.Contains(player))
            {
                if (!Radios.TryGetValue(player, out var index)) continue;
                var sabotage = CurrentSabotages[index % CurrentSabotages.Count];
                sb.AppendLine($"Sabotage: <color=red>{sabotage.Name}</color>");
                var cooldown = Math.Max(0, Config.SabotageCooldown - (DateTime.UtcNow - LastActivated).TotalSeconds);
                if (cooldown > 0)
                    sb.AppendLine($"Cooldown: {cooldown:0} sec");
                sb.AppendLine($"Currently Active: {(CurrentSabotage != null ? "<color=red>" + CurrentSabotage.Name + "</color>" : "None")}");
                player.SendHint(sb.ToString(), 1.25f);
            }
        }
        StringBuilderPool.Shared.Return(sb);
    }

    protected override bool IsRoundDone()
    {
        if (CurrentSabotage is { IsCritical: true, Timer: <= 0 })
        {
            Result = Translation.ImpostorWin;
            return true;
        }
        
        var impostorAlive = Impostors.Any(i => i.IsAlive);
        var crewmateAlive = Crewmates.Any(c => c.IsAlive);

        var allTasksDone = Crewmates.Count > 0 && Crewmates.All(p =>
            TaskManager.TryGet(p, out var tm) &&
            tm.Tasks.Count > 0 &&
            tm.Tasks.All(TaskManager.IsTaskDone));

        if (!impostorAlive || allTasksDone)
        {
            Result = Translation.CrewmateWin;
            return true;
        }

        var aliveCrewmates = Crewmates.Count(c => c.IsAlive);
        var aliveImpostors = Impostors.Count(i => i.IsAlive);
        if (crewmateAlive && aliveImpostors < aliveCrewmates) return false;
        Result = Translation.ImpostorWin;
        return true;
    }

    protected override void OnFinished()
    {
        Extensions.ServerBroadcast(Result, 10);
    }

    protected override void OnCleanup()
    {
        foreach (var skin in PlayerSkins.Values)
            NetworkServer.Destroy(skin);

        foreach (var textToy in PlayerTextToys.Values.Where(textToy => textToy != null))
            textToy.Destroy();

        PlayerSkins.Clear();
        PlayerVotes.Clear();
        TaskToyList.Clear();
        PlayerColors.Clear();
        PlayerTextToys.Clear();
        SpawnList.Clear();
        DoorList.Clear();
        Impostors.Clear();
        Crewmates.Clear();
        KillCooldowns.Clear();
        PlayerMeetings.Clear();
        GeneratedTasks.Clear();
        ImpostorRadioItems.Clear();
        GeneratedSabotages.Clear();
        VentedPlayers.Clear();
        CurrentSabotage = null;
        LastActivated = DateTime.MinValue;
        foreach (var player in Muted.Where(player => player != null))
        {
            player.Unmute(true);
            player.Unmute(false);
        }
        Muted.Clear();
        TaskManager.ClearForPlayers(Player.ReadyList);
        Server.ClearBroadcasts();
        Timing.KillCoroutines("BroadcastVotingCountdown");
        foreach (var player in Player.ReadyList) player.DisplayName = string.Empty;
        Instance = null;
    }

    private void CreateTasksForPlayers(List<Player> players)
    {
        var maxShort = Config.ShortTasks;
        var maxCommon = Config.CommonTasks;
        var maxLong = Config.LongTasks;
        var isVisual = Config.VisualTasks;
        var tasks = GeneratedTasks[MapInfo.MapName];
        if (tasks == null || tasks.Count == 0) return;

        foreach (var player in players)
        {
            var availableTasks = tasks.OrderBy(_ => Guid.NewGuid()).ToList();
            if (!isVisual)
                availableTasks = tasks.Where(t => !t.IsVisual).ToList();
            if (availableTasks.Count == 0) continue;
            var random = new Random();
            var tm = new TaskManager(player)
            {
                Tasks = []
            };
            var playerShortTask = TaskManager.CountByType(player, TaskType.Short);
            var playerCommonTask = TaskManager.CountByType(player, TaskType.Common);
            var playerLongTask = TaskManager.CountByType(player, TaskType.Long);

            var assignedToys = new HashSet<InvisibleInteractableToy>();
            while (playerShortTask < maxShort || playerCommonTask < maxCommon || playerLongTask < maxLong)
            {
                var task = availableTasks[random.Next(availableTasks.Count)];
                switch (task.Type)
                {
                    case TaskType.Short when playerShortTask < maxShort:
                        TaskManager.AddTask(tm, task);
                        LogManager.Debug($"Added short task {task.Name} to {player.Nickname}");
                        playerShortTask++;
                        break;
                    case TaskType.Common when playerCommonTask < maxCommon:
                        TaskManager.AddTask(tm, task);
                        LogManager.Debug($"Added common task {task.Name} to {player.Nickname}");
                        playerCommonTask++;
                        break;
                    case TaskType.Long when playerLongTask < maxLong:
                        TaskManager.AddTask(tm, task);
                        LogManager.Debug($"Added long task {task.Name} to {player.Nickname}");
                        playerLongTask++;
                        break;
                }

                LogManager.Debug($"Trying to assign toys for task {task.Name}");
                foreach (var taskToy in TaskToyList.Where(taskToy => !assignedToys.Contains(taskToy)))
                {
                    if (!EventHandler.TryParseToyName(taskToy.name, out var room, out var tName, out var isTask, out _, out _)) continue;
                    if (!isTask) continue;
                    if ((!string.IsNullOrEmpty(tName) && task.Name.ToString() != tName) ||
                        task.RoomName.ToString() != room)
                        continue;
                    LogManager.Debug(
                        $"Assigned toy {taskToy.name} to task {task.Name} for player {player.Nickname} lenght: {TaskManager.GetLength(task)}");
                    taskToy.SetFakeInteractionDuration(player, TaskManager.GetLength(task));
                    taskToy.SetInteractableToy(player, TaskManager.GetLength(task));
                    assignedToys.Add(taskToy);
                }

                availableTasks.Remove(task);
                if (availableTasks.Count == 0) break;
            }
        }
    }

    internal static Misc.PlayerInfoColorTypes? GetColorTypeByHex(string hex)
    {
        foreach (var pair in
                 Misc.AllowedColors.Where(pair => pair.Value.Equals(hex, StringComparison.OrdinalIgnoreCase)))
            return pair.Key;
        return null;
    }

    private void GenerateTasks()
    {
        GeneratedTasks = new Dictionary<string, List<Task>>
        {
            {
                "Skeld",
                [
                    new Task
                    {
                        Name = TaskName.CalibrateDistributor, RoomName = RoomName.Electrical, Type = TaskType.Short,
                        Description = Instance.Translation.CalibrateDistributor
                    },
                    new Task
                    {
                        Name = TaskName.ChartCourse, RoomName = RoomName.Navigation, Type = TaskType.Short,
                        Description = Instance.Translation.CharCourse
                    },
                    new Task
                    {
                        Name = TaskName.CleanO2Filter, RoomName = RoomName.O2, Type = TaskType.Short,
                        Description = Instance.Translation.CleanO2Filter
                    },
                    new Task
                    {
                        Name = TaskName.StartReactor, RoomName = RoomName.Reactor, Type = TaskType.Long,
                        Description = Instance.Translation.StartReactor
                    },
                    new Task
                    {
                        Name = TaskName.EmptyChute, RoomName = RoomName.O2, Type = TaskType.Common, IsVisual = true,
                        Description = Instance.Translation.EmptyChute
                    },
                    new Task
                    {
                        Name = TaskName.EmptyChute, RoomName = RoomName.Storage, Type = TaskType.Common, IsVisual = true,
                        Description = Instance.Translation.EmptyChute
                    },
                    new Task
                    {
                        Name = TaskName.FixWiring,
                        RoomName = RoomName.Electrical,
                        Type = TaskType.Common,
                        Description = Instance.Translation.FixWiring,
                        MaxStageTask = 3,
                        StageTasks =
                        [
                            new StageTask
                            {
                                Name = TaskName.FixWiring, RoomName = RoomName.Storage, Type = TaskType.Common,
                                Description = Instance.Translation.FixWiring
                            },
                            new StageTask
                            {
                                Name = TaskName.FixWiring, RoomName = RoomName.Admin, Type = TaskType.Common,
                                Description = Instance.Translation.FixWiring
                            },
                            new StageTask
                            {
                                Name = TaskName.FixWiring, RoomName = RoomName.Navigation, Type = TaskType.Common,
                                Description = Instance.Translation.FixWiring
                            },
                            new StageTask
                            {
                                Name = TaskName.FixWiring, RoomName = RoomName.Cafeteria, Type = TaskType.Common,
                                Description = Instance.Translation.FixWiring
                            },
                            new StageTask
                            {
                                Name = TaskName.FixWiring, RoomName = RoomName.Security, Type = TaskType.Common,
                                Description = Instance.Translation.FixWiring
                            }
                        ]
                    },
                    new Task
                    {
                        Name = TaskName.AlignEngineOutput,
                        RoomName = RoomName.UpperEngine,
                        Type = TaskType.Long,
                        Description = Instance.Translation.AlignEngineOutput,
                        StageTasks =
                        [
                            new StageTask
                            {
                                Name = TaskName.AlignEngineOutput, RoomName = RoomName.LowerEngine,
                                Type = TaskType.Long,
                                Description = Instance.Translation.AlignEngineOutput
                            }
                        ]
                    },
                    new Task
                    {
                        Name = TaskName.AlignEngineOutput,
                        RoomName = RoomName.LowerEngine,
                        Type = TaskType.Long,
                        Description = Instance.Translation.AlignEngineOutput,
                        StageTasks =
                        [
                            new StageTask
                            {
                                Name = TaskName.AlignEngineOutput, RoomName = RoomName.UpperEngine,
                                Type = TaskType.Long,
                                Description = Instance.Translation.AlignEngineOutput
                            }
                        ]
                    },
                    new Task
                    {
                        Name = TaskName.DivertPower,
                        RoomName = RoomName.Electrical,
                        Type = TaskType.Short,
                        Description = Instance.Translation.DivertPowerTo,
                        StageTasks =
                        [
                            new StageTask
                            {
                                Name = TaskName.AcceptDivertedPower, RoomName = RoomName.Communications,
                                Type = TaskType.Short, Description = Instance.Translation.AcceptDivertPower
                            },
                            new StageTask
                            {
                                Name = TaskName.AcceptDivertedPower, RoomName = RoomName.LowerEngine,
                                Type = TaskType.Short,
                                Description = Instance.Translation.AcceptDivertPower
                            },
                            new StageTask
                            {
                                Name = TaskName.AcceptDivertedPower, RoomName = RoomName.Navigation,
                                Type = TaskType.Short,
                                Description = Instance.Translation.AcceptDivertPower
                            },
                            new StageTask
                            {
                                Name = TaskName.AcceptDivertedPower, RoomName = RoomName.O2, Type = TaskType.Short,
                                Description = Instance.Translation.AcceptDivertPower
                            },
                            new StageTask
                            {
                                Name = TaskName.AcceptDivertedPower, RoomName = RoomName.Security,
                                Type = TaskType.Short,
                                Description = Instance.Translation.AcceptDivertPower
                            },
                            new StageTask
                            {
                                Name = TaskName.AcceptDivertedPower, RoomName = RoomName.Shields, Type = TaskType.Short,
                                Description = Instance.Translation.AcceptDivertPower
                            },
                            new StageTask
                            {
                                Name = TaskName.AcceptDivertedPower, RoomName = RoomName.UpperEngine,
                                Type = TaskType.Short,
                                Description = Instance.Translation.AcceptDivertPower
                            },
                            new StageTask
                            {
                                Name = TaskName.AcceptDivertedPower, RoomName = RoomName.Weapons, Type = TaskType.Short,
                                Description = Instance.Translation.AcceptDivertPower
                            }
                        ]
                    },
                    new Task
                    {
                        Name = TaskName.UploadData,
                        RoomName = RoomName.Cafeteria,
                        Type = TaskType.Long,
                        Description = Instance.Translation.DownloadData,
                        StageTasks =
                        [
                            new StageTask
                            {
                                Name = TaskName.UploadData, RoomName = RoomName.Admin, Type = TaskType.Long,
                                Description = Instance.Translation.UploadData
                            }
                        ]
                    },
                    new Task
                    {
                        Name = TaskName.UploadData,
                        RoomName = RoomName.Communications,
                        Type = TaskType.Long,
                        Description = Instance.Translation.DownloadData,
                        StageTasks =
                        [
                            new StageTask
                            {
                                Name = TaskName.UploadData, RoomName = RoomName.Admin, Type = TaskType.Long,
                                Description = Instance.Translation.UploadData
                            }
                        ]
                    },
                    new Task
                    {
                        Name = TaskName.UploadData,
                        RoomName = RoomName.Electrical,
                        Type = TaskType.Long,
                        Description = Instance.Translation.DownloadData,
                        StageTasks =
                        [
                            new StageTask
                            {
                                Name = TaskName.UploadData, RoomName = RoomName.Admin, Type = TaskType.Long,
                                Description = Instance.Translation.UploadData
                            }
                        ]
                    },
                    new Task
                    {
                        Name = TaskName.UploadData,
                        RoomName = RoomName.Navigation,
                        Type = TaskType.Long,
                        Description = Instance.Translation.DownloadData,
                        StageTasks =
                        [
                            new StageTask
                            {
                                Name = TaskName.UploadData, RoomName = RoomName.Admin, Type = TaskType.Long,
                                Description = Instance.Translation.UploadData
                            }
                        ]
                    },
                    new Task
                    {
                        Name = TaskName.AcceptDivertedPower,
                        RoomName = RoomName.Weapons,
                        Type = TaskType.Long,
                        Description = Instance.Translation.DownloadData,
                        StageTasks =
                        [
                            new StageTask
                            {
                                Name = TaskName.UploadData, RoomName = RoomName.Admin, Type = TaskType.Long,
                                Description = Instance.Translation.UploadData
                            }
                        ]
                    }
                ]
            }
        };
    }

    private void GenerateSabotages()
    {
        GeneratedSabotages = new Dictionary<string, List<Sabotage>>
        {
            {
                "Skeld",
                [
                    new Sabotage
                    {
                        Name = "Communications", Type = SabotageType.CommsSabotage, EnabledMeetings = false,
                        IsCritical = false
                    },
                    new Sabotage
                    {
                        Name = "Lights", Type = SabotageType.FixLights, EnabledMeetings = false,
                        IsCritical = false
                    },
                    new Sabotage
                    {
                        Name = "Door Lockdown", Type = SabotageType.DoorLockdown, EnabledMeetings = true,
                        IsCritical = false
                    }/*,
                    new Sabotage
                    {
                        Name = "O2", Type = SabotageType.OxygenDepleted, Duration = 30f, EnabledMeetings = false,
                        IsCritical = true
                    },
                    new Sabotage
                    {
                        Name = "Reactor", Type = SabotageType.ReactorMeltdown, Duration = 30f, EnabledMeetings = false,
                        IsCritical = true
                    }*/
                ]
            }
        };
    }
}


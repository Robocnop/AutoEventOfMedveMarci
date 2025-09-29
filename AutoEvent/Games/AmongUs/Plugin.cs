using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using AdminToys;
using AutoEvent.API;
using AutoEvent.API.Enums;
using AutoEvent.Games.AmongUs.Configs;
using AutoEvent.Games.AmongUs.Features;
using AutoEvent.Interfaces;
using CustomPlayerEffects;
using LabApi.Events.Handlers;
using LabApi.Features.Wrappers;
using LabApiExtensions.FakeExtension;
using MEC;
using Mirror;
using ProjectMER.Features.Extensions;
using ProjectMER.Features.Serializable.Schematics;
using UnityEngine;
using Extensions = AutoEvent.API.Extensions;
using PrimitiveObjectToy = AdminToys.PrimitiveObjectToy;
using Random = System.Random;
using TextToy = LabApi.Features.Wrappers.TextToy;

namespace AutoEvent.Games.AmongUs;

public class Plugin : Event<Configs.Config, Translation>, IEventMap
{
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
    private Dictionary<string, List<PrimitiveObjectToy>> DoorList { get; set; }
    private List<InvisibleInteractableToy> TaskToyList { get; set; }
    internal Dictionary<uint, GameObject> PlayerSkins { get; private set; }
    internal Dictionary<uint, uint> PlayerVotes { get; private set; }
    internal Dictionary<uint, string> PlayerColors { get; private set; }
    private Dictionary<uint, TextToy> PlayerTextToys { get; set; }
    internal InvisibleInteractableToy MeetingButton { get; private set; }
    internal Dictionary<Player, DateTime> KillCooldowns { get; set; } = new();
    internal Dictionary<Player, int> PlayerMeetings { get; set; } = new();
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
        PlayerEvents.SearchedToy += EventHandler.OnPlayerSearchedToy;
        PlayerEvents.Hurting += _eventHandler.OnPlayerHurting;
        PlayerEvents.ShootingWeapon += _eventHandler.OnShooting;
        PlayerEvents.ChangingItem += _eventHandler.OnPlayerChangingItem;
        PlayerEvents.InteractedToy += _eventHandler.OnPlayerInteractedToy;
        PlayerEvents.Left += _eventHandler.OnPlayerLeft;
    }

    protected override void UnregisterEvents()
    {
        PlayerEvents.SearchedToy -= EventHandler.OnPlayerSearchedToy;
        PlayerEvents.Hurting -= _eventHandler.OnPlayerHurting;
        PlayerEvents.ShootingWeapon -= _eventHandler.OnShooting;
        PlayerEvents.ChangingItem -= _eventHandler.OnPlayerChangingItem;
        PlayerEvents.InteractedToy -= _eventHandler.OnPlayerInteractedToy;
        PlayerEvents.Left -= _eventHandler.OnPlayerLeft;
        _eventHandler = null;
    }

    protected override void OnStart()
    {
        Instance = this;
        Impostors.Clear();
        Crewmates.Clear();
        TaskManager.ClearForPlayers(Player.ReadyList);
        KillCooldowns.Clear();
        PlayerMeetings.Clear();
        SpawnList = [];
        DoorList = new Dictionary<string, List<PrimitiveObjectToy>>();
        PlayerSkins = new Dictionary<uint, GameObject>();
        PlayerVotes = new Dictionary<uint, uint>();
        PlayerColors = new Dictionary<uint, string>();
        PlayerTextToys = new Dictionary<uint, TextToy>();
        Instance.MeetingCalled = false;
        MeetingCooldown = Config.EmergencyCooldown;

        foreach (var adminToyBase in MapInfo.Map.AdminToyBases)
            if (adminToyBase.name.Contains("Spawnpoint"))
            {
                SpawnList.Add(adminToyBase.gameObject);
            }
            else if (adminToyBase.name.Contains("Door_") &&
                     adminToyBase.TryGetComponent<PrimitiveObjectToy>(out var door))
            {
                var roomName = adminToyBase.name.Split('_')[1];
                if (!DoorList.ContainsKey(roomName))
                    DoorList.Add(roomName, []);
                DoorList[roomName].Add(door);
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
            } else if (adminToyBase.name == "VentObject")
                VentObject = adminToyBase;

        Impostors = Player.ReadyList.Where(p => !p.IsDummy).ToList();//Config.Impostors.GetPlayers();
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
            impostor.AddItem(ItemType.GunCOM18);
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
    }

    internal IEnumerator<float> BroadcastVotingCountdown(string reason = "")
    {
        var time = Config.VotingTime;
        var shortened = false;
        LogManager.Debug("reason: " + reason);
        while (time > 0)
        {
            var playersCount = Impostors.Count(p => p.IsAlive) + Crewmates.Count(p => p.IsAlive);

            if (!shortened && playersCount > 0 && PlayerVotes.Count >= playersCount && time > 5)
            {
                time = 5;
                shortened = true;
            }
            
            foreach (var player in Player.ReadyList.Where(p => Impostors.Contains(p) || Crewmates.Contains(p)))
            {
                player.Broadcast(
                    Translation.VotingInfo.Replace("{reason}", reason).Replace("{time}",
                        time.ToString(CultureInfo.InvariantCulture)));
            }

            yield return Timing.WaitForSeconds(1f);
            time--;
        }

        Instance.MeetingCalled = false;
        Extensions.ServerBroadcast("Voting ended", 5);
        var maxVotes = Instance.PlayerVotes.Values
            .GroupBy(v => v)
            .OrderByDescending(g => g.Count())
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToList();
        LogManager.Debug(
            $"Max votes count: {maxVotes.Count}, max votes: {(maxVotes.Count > 0 ? maxVotes[0].Count : 0)}");
        switch (maxVotes.Count)
        {
            case 0:
                Extensions.ServerBroadcast("No one was voted out.", 5);
                break;
            case > 1 when maxVotes[0].Count == maxVotes[1].Count:
                Extensions.ServerBroadcast("It's a tie! No one was voted out.", 5);
                break;
            default:
            {
                var votedOut = Player.Get(maxVotes[0].Id);
                if (votedOut != null)
                {
                    votedOut.Kill("Voted out");
                    votedOut.DisplayName = string.Empty;
                    TaskManager.ClearForPlayers([votedOut]);

                    if (PlayerSkins.TryGetValue(votedOut.NetworkId, out var skin))
                        NetworkServer.Destroy(skin);
                    PlayerSkins.Remove(votedOut.NetworkId);
                    PlayerColors.Remove(votedOut.NetworkId);
                    Impostors.Remove(votedOut);
                    Crewmates.Remove(votedOut);
                    KillCooldowns.Remove(votedOut);
                    Extensions.ServerBroadcast($"{votedOut.Nickname} was voted out.", 5);
                }

                break;
            }
        }

        PlayerVotes.Clear();
        foreach (var textToy in PlayerTextToys.Values)
            textToy.Destroy();
        PlayerTextToys.Clear();

        foreach (var skin in PlayerSkins.Values.Where(skin => skin.name == "DeathSkin"))
            NetworkServer.Destroy(skin);

        foreach (var player in Player.ReadyList)
        {
            if (Impostors.Contains(player)) 
                player.AddItem(ItemType.GunCOM18);

            player.DisableEffect<Ensnared>();
        }
    }

    protected override void ProcessFrame()
    {
        if (Instance.MeetingCalled)
        {
            foreach (var player in Crewmates)
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
                var text = votes.Count > 0
                    ? Config.AnonymousVotes
                        ? $"{votes.Count} vote{(votes.Count > 1 ? "s" : "")}"
                        : $"{string.Join(", ", votes.Select(id => Player.Get(id)?.Nickname ?? id.ToString()))}\n{votes.Count} vote{(votes.Count > 1 ? "s" : "")}"
                    : "No votes";
                textToy.TextFormat = $"<size=10>{text}</size>";
                player.SendHint(text, 1f);
            }

            foreach (var player in Impostors)
            {
                if (!PlayerTextToys.TryGetValue(player.NetworkId, out var textToy))
                {
                    LogManager.Debug($"Creating text toy for {player.Nickname}");
                    textToy = TextToy.Create(player.GameObject?.transform);
                    textToy.GameObject.transform.localPosition += new Vector3(0, 3, 0);
                    textToy.Rotation = Quaternion.Euler(0, 180, 0);
                    PlayerTextToys[player.NetworkId] = textToy;
                }

                var votes = PlayerVotes.Where(v => v.Value == player.NetworkId).Select(v => v.Key).ToList();
                var text = votes.Count > 0
                    ? Config.AnonymousVotes
                        ? $"{votes.Count} vote{(votes.Count > 1 ? "s" : "")}"
                        : $"{string.Join(", ", votes.Select(id => Player.Get(id)?.Nickname ?? id.ToString()))}\n{votes.Count} vote{(votes.Count > 1 ? "s" : "")}"
                    : "No votes";
                textToy.TextFormat = $"<size=10>{text}</size>";
                player.SendHint(text, 1f);
                MeetingCooldown = Config.EmergencyCooldown;
            }

            return;
        }

        if (MeetingCooldown != 0)
            MeetingCooldown -= 1;

        foreach (var player in Crewmates)
        {
            if (!TaskManager.TryGet(player, out var tm)) continue;
            var sb = new StringBuilder();
            sb.AppendLine("Tasks:");
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

                    var description = mt.Description.Replace("%roomName%", mt.RoomName.ToString());
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
                    var description = mt.Description.Replace("%roomName%", mt.RoomName.ToString());
                    var line = $"{mt.RoomName}: {description}";
                    if (isCompleted)
                        line = $"<color=green>{line}</color>";
                    sb.AppendLine(line);
                }
            }

            player.SendHint(sb.ToString(), 1f);
        }
    }

    protected override bool IsRoundDone()
    {
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
        var tasks = Config.Tasks[MapInfo.MapName];
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
                    if (!EventHandler.TryParseToyName(taskToy.name, out var room, out var tName)) continue;
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
}
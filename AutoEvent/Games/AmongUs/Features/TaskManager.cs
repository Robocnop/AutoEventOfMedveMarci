using System;
using System.Collections.Generic;
using System.Linq;
using LabApi.Features.Wrappers;

namespace AutoEvent.Games.AmongUs.Features;

internal class TaskManager
{
    private static readonly Dictionary<Player, TaskManager> PlayerTasks = new();

    internal TaskManager(Player player)
    {
        PlayerTasks[player] = this;
    }

    internal List<Task> Tasks { get; init; } = [];

    internal static bool TryGet(Player player, out TaskManager tm)
    {
        return PlayerTasks.TryGetValue(player, out tm);
    }

    internal static void AddTask(TaskManager tm, Task template)
    {
        if (tm == null || template == null) return;
        tm.Tasks.Add(CloneTask(template));
    }

    private static Task CloneTask(Task original)
    {
        var rand = new Random();
        var clone = new Task
        {
            Name = original.Name,
            RoomName = original.RoomName,
            Type = original.Type,
            Description = original.Description,
            IsVisual = original.IsVisual,
            MaxStageTask = original.MaxStageTask,
            StageTasks = []
        };

        if (original.StageTasks is not { Count: > 0 }) return clone;
        var list = original.StageTasks;
        if (original.MaxStageTask > 0 && list.Count > original.MaxStageTask)
            list = list.OrderBy(_ => rand.Next()).Take(original.MaxStageTask).ToList();

        foreach (var st in list)
            clone.StageTasks.Add(new StageTask
            {
                Name = st.Name,
                RoomName = st.RoomName,
                Type = st.Type,
                Description = st.Description,
                IsDone = false
            });
        return clone;
    }

    internal static int CountByType(Player player, TaskType type)
    {
        return !TryGet(player, out var tm) ? 0 : tm.Tasks.Count(t => t.Type == type);
    }

    internal static List<StageTask> GetPlayerStageTasks(Player player)
    {
        if (!TryGet(player, out var tm)) return [];
        return tm.Tasks
            .Where(t => t.IsDone && t.StageTasks.Count > 0)
            .SelectMany(t => t.StageTasks)
            .Where(st => !st.IsDone)
            .ToList();
    }

    internal static bool IsTaskDone(Task task)
    {
        if (task == null) return false;
        if (task.StageTasks.Count == 0) return task.IsDone;
        return task.IsDone && task.StageTasks.All(s => s.IsDone);
    }

    internal static int GetLength(Task task)
    {
        return task.Type switch
        {
            TaskType.Short => 5,
            TaskType.Common => 10,
            TaskType.Long => 15,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    internal static int GetLength(StageTask task)
    {
        return task.Type switch
        {
            TaskType.Short => 5,
            TaskType.Common => 10,
            TaskType.Long => 15,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    internal static void ClearForPlayers(IEnumerable<Player> players)
    {
        foreach (var p in players)
        {
            if (!TryGet(p, out var tm)) continue;
            tm.Tasks.Clear();
            PlayerTasks.Remove(p);
        }
    }
}
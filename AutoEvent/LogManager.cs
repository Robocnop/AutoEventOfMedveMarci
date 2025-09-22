using System;
using LabApi.Features.Console;

namespace AutoEvent;

internal abstract class LogManager
{
    public static bool DebugEnabled => AutoEvent.Singleton?.Config?.Debug == true;

    public static void Debug(string message)
    {
        if (!DebugEnabled)
            return;

        Logger.Raw($"[DEBUG] [{AutoEvent.Singleton?.Name ?? "AutoEvent"}] {message}", ConsoleColor.Green);
    }

    public static void Info(string message, ConsoleColor color = ConsoleColor.Cyan)
    {
        Logger.Raw($"[INFO] [{AutoEvent.Singleton?.Name ?? "AutoEvent"}] {message}", color);
    }

    public static void Warn(string message)
    {
        Logger.Warn(message);
    }

    public static void Error(string message)
    {
        var plugin = AutoEvent.Singleton;
        var eventManager = AutoEvent.EventManager;

        var name = plugin?.Name ?? "AutoEvent";
        var version = plugin?.Version.ToString() ?? "Unknown";

        var eventInfo = "No Event active.";
        var currentEvent = eventManager?.CurrentEvent;
        if (currentEvent != null && !string.IsNullOrWhiteSpace(currentEvent.Name))
            eventInfo = $"Current Event: {currentEvent.Name}";

        var merInfo = eventManager?.IsMerLoaded == true && ProjectMER.ProjectMER.Singleton != null
            ? $"ProjectMER Version: {ProjectMER.ProjectMER.Singleton.Version}"
            : "ProjectMER is not loaded.";

        Logger.Raw(
            $"[ERROR] [{name}] Details:\nVersion: {version}\n{eventInfo}\n{merInfo}\n{message}",
            ConsoleColor.Red);
    }
}
using System;
using LabApi.Features.Console;

namespace AutoEvent;

internal abstract class LogManager
{
    public static bool DebugEnabled => AutoEvent.Singleton.Config.Debug;

    public static void Debug(string message)
    {
        if (!DebugEnabled)
            return;

        Logger.Raw($"[DEBUG] [{AutoEvent.Singleton.Name}] {message}", ConsoleColor.Green);
    }

    public static void Info(string message, ConsoleColor color = ConsoleColor.Cyan)
    {
        Logger.Raw($"[INFO] [{AutoEvent.Singleton.Name}] {message}", color);
    }

    public static void Warn(string message)
    {
        Logger.Warn(message);
    }

    public static void Error(string message)
    {
        Logger.Raw($"[ERROR] [{AutoEvent.Singleton.Name}] Details:\nVersion: {AutoEvent.Singleton.Version}\n{(AutoEvent.EventManager != null && AutoEvent.EventManager.CurrentEvent != null && AutoEvent.EventManager.CurrentEvent.Name == null ? $"Current Event: {AutoEvent.EventManager.CurrentEvent.Name}" : "No Event active.")}\n{(AutoEvent.EventManager.IsMerLoaded ? $"ProjectMER Version: {ProjectMER.ProjectMER.Singleton.Version}" : "ProjectMER is not loaded.")}\n{message}", ConsoleColor.Red);
    }
}
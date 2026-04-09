using System;
using System.IO;
using AutoEvent.API;
using AutoEvent.ApiFeatures;
using AutoEvent.Commands;
using AutoEvent.Integrations;
using AutoEvent.Integrations.Audio;
using AutoEvent.Integrations.MapEditor;
using AutoEvent.Loader;
using AutoEvent.Patches;
using AutoEvent.Updater;
using HarmonyLib;
using LabApi.Events.CustomHandlers;
using LabApi.Features;
using LabApi.Features.Wrappers;
using LabApi.Loader.Features.Paths;
using LabApi.Loader.Features.Plugins;
using EventManager = AutoEvent.Loader.EventManager;

namespace AutoEvent;

public class AutoEvent : Plugin<Config>
{
    public static AutoEvent Singleton;
    private static Harmony _harmonyPatch;
    internal static EventManager InternalEventManager;
    private static EventHandler _eventHandler;
    internal static float MusicVolume;
    public static readonly MainCommand MainCommand = new();
    public override string Name => "AutoEvent";

    public override string Author =>
        "Created by a large community of programmers, map builders and just ordinary people, under the leadership of RisottoMan. MapEditorReborn for 14.1 port by Sakred_. LabApi port by MedveMarci.";

    public override string Description =>
        "A plugin that allows you to play mini-games in SCP:SL. It includes a variety of games such as Spleef, Lava, Hide and Seek, Knives, and more. Each game has its own unique mechanics and rules, providing a fun and engaging experience for players.";

    public override Version Version => new(10, 0, 0);
    public override Version RequiredApiVersion => new(LabApiProperties.CompiledVersion);

    public static string BaseConfigPath { get; private set; }

    public override void Enable()
    {
        BaseConfigPath = Path.Combine(PathManager.Configs.FullName, "AutoEvent");
        try
        {
            Singleton = this;

            if (Singleton.Config.CreditTagSystem)
                ApiManager.LoadCreditTags();


            if (Config.IgnoredRoles.Contains(Config.LobbyRole))
            {
                LogManager.Error(
                    "The Lobby Role is also in ignored roles. This will break the game if not changed. The plugin will remove the lobby role from ignored roles.");
                Config.IgnoredRoles.Remove(Config.LobbyRole);
            }

            FriendlyFireSystem.IsFriendlyFireEnabledByDefault = Server.FriendlyFire;

            MapSystemIntegration.Detect();
            AudioSystemIntegration.Detect();
            RadioMenuIntegration.Detect();

            try
            {
                _harmonyPatch = new Harmony("autoevent");
                _harmonyPatch.PatchAll();

                if (MapSystemIntegration.IsProjectMerLoaded)
                    SchematicMerPatch.ApplyPatch(_harmonyPatch);
            }
            catch (Exception e)
            {
                LogManager.Error($"Could not patch harmony methods.\n{e}");
            }

            try
            {
                LogManager.Debug($"Base Config Path: {BaseConfigPath}");
                LogManager.Debug($"Configs paths: \n" +
                                 $"{Config.SchematicsDirectoryPath}\n" +
                                 $"{Config.MusicDirectoryPath}\n");
                CreateDirectoryIfNotExists(BaseConfigPath);
                CreateDirectoryIfNotExists(Config.SchematicsDirectoryPath);
                CreateDirectoryIfNotExists(Config.MusicDirectoryPath);
            }
            catch (Exception e)
            {
                LogManager.Error($"An error has occured while trying to initialize directories.\n{e}");
            }

            InternalEventManager = new EventManager();
            InternalEventManager.RegisterInternalEvents();
            _eventHandler = new EventHandler();
            CustomHandlersManager.RegisterEventsHandler(_eventHandler);
            ConfigManager.LoadConfigsAndTranslations();
            MusicVolume = Config.Volume;
            SchematicUpdater.Check();
            LogManager.Info("The mini-games are loaded.");
        }
        catch (Exception e)
        {
            LogManager.Error($"Caught an exception while starting plugin.\n{e}");
        }
    }

    private static void CreateDirectoryIfNotExists(string path)
    {
        try
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        }
        catch (Exception e)
        {
            LogManager.Error($"An error has occured while trying to create a new directory.\nPath: {path}\n{e}");
        }
    }

    public override void Disable()
    {
        _harmonyPatch.UnpatchAll();
        InternalEventManager = null;
        Singleton = null;
        CustomHandlersManager.UnregisterEventsHandler(_eventHandler);
        _eventHandler = null;
    }
}
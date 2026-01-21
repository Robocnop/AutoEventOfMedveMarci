using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using AutoEvent.ApiFeatures;
using AutoEvent.Interfaces;
using LabApi.Features.Wrappers;
using LabApi.Loader.Features.Yaml;

namespace AutoEvent.Loader;

public static class ConfigManager
{
    private static string ConfigPath { get; } = Path.Combine(AutoEvent.BaseConfigPath, "configs.yml");

    private static string TranslationPath { get; } = Path.Combine(AutoEvent.BaseConfigPath, "translation.yml");

    internal static Dictionary<string, string> LanguageByCountryCodeDictionary { get; private set; } = new();

    public static void LoadConfigsAndTranslations()
    {
        LoadConfigs();
        LoadTranslations();
    }

    private static void LoadConfigs()
    {
        try
        {
            Dictionary<string, object> configs;

            if (!File.Exists(ConfigPath))
            {
                configs = new Dictionary<string, object>();
                foreach (var ev in AutoEvent.EventManager.Events.OrderBy(r => r.InternalName))
                    configs[ev.InternalName] = ev.InternalConfig;
                File.WriteAllText(ConfigPath, YamlConfigParser.Serializer.Serialize(configs));
                return;
            }

            configs =
                YamlConfigParser.Deserializer.Deserialize<Dictionary<string, object>>(
                    File.ReadAllText(ConfigPath));

            foreach (var ev in AutoEvent.EventManager.Events)
            {
                if (configs is null)
                    continue;

                if (!configs.TryGetValue(ev.InternalName, out var rawDeserializedConfig))
                {
                    LogManager.Warn($"[ConfigManager] {ev.InternalName} doesn't have configs");
                    continue;
                }

                var loadedConfig = (EventConfig)YamlConfigParser.Deserializer.Deserialize(
                    YamlConfigParser.Serializer.Serialize(rawDeserializedConfig),
                    ev.InternalConfig.GetType());

                ev.InternalConfig.CopyProperties(loadedConfig);
            }

            var updatedConfigs = new Dictionary<string, object>();
            foreach (var ev in AutoEvent.EventManager.Events.OrderBy(r => r.InternalName))
                updatedConfigs[ev.InternalName] = ev.InternalConfig;

            File.WriteAllText(ConfigPath, YamlConfigParser.Serializer.Serialize(updatedConfigs));

            LogManager.Info("[ConfigManager] The configs of the mini-games are loaded and updated.");
        }
        catch (Exception ex)
        {
            LogManager.Error($"[ConfigManager] cannot read from the config.\n{ex}");
        }
    }


    internal static void LoadTranslations()
    {
        try
        {
            if (ApiManager.TryGetLanguages(out var languages))
            {
                LanguageByCountryCodeDictionary = languages;
            }
            else
            {
                LogManager.Warn("[ConfigManager] Could not retrieve languages from API. Defaulting to English only.");
                LanguageByCountryCodeDictionary["en"] = "English";
            }

            Dictionary<string, object> translations;

            if (!File.Exists(TranslationPath))
            {
                var countryCode = "en";
                try
                {
                    countryCode = HttpQuery.Get($"http://ipinfo.io/{Server.IpAddress}/country");
                    countryCode = countryCode?.Trim().ToLowerInvariant();
                }
                catch (WebException)
                {
                    LogManager.Warn("Couldn't verify the server country. Providing default translation.");
                }

                LogManager.Warn(
                    $"[ConfigManager] The translation.yml file was not found. Creating a new translation for {countryCode} language...");
                translations = LoadTranslationFromAssembly(countryCode);
            }

            // Otherwise, check language of the translation with the language of the config.
            else
            {
                translations =
                    YamlConfigParser.Deserializer.Deserialize<Dictionary<string, object>>(
                        File.ReadAllText(TranslationPath));
            }

            // Move translations to each mini-games
            foreach (var ev in AutoEvent.EventManager.Events.Where(_ => translations is not null))
            {
                if (!translations.TryGetValue(ev.InternalName, out var rawDeserializedTranslation))
                {
                    LogManager.Warn($"[ConfigManager] {ev.InternalName} doesn't have translations");
                    continue;
                }

                var obj = YamlConfigParser.Deserializer.Deserialize(
                    YamlConfigParser.Serializer.Serialize(rawDeserializedTranslation),
                    ev.InternalTranslation.GetType());
                if (obj is not EventTranslation translation)
                {
                    LogManager.Warn($"[ConfigManager] {ev.InternalName} malformed translation.");
                    continue;
                }

                ev.InternalTranslation.CopyProperties(translation);

                ev.Name = translation.Name;
                ev.Description = translation.Description;
                ev.CommandName = translation.CommandName;
            }

            LogManager.Info("[ConfigManager] The translations of the mini-games are loaded.");
        }
        catch (Exception ex)
        {
            LogManager.Error($"[ConfigManager] Cannot read from the translation.\n{ex}");
        }
    }

    internal static Dictionary<string, object> LoadTranslationFromAssembly(string countryCode)
    {
        // Try to get a translation from an assembly
        if (!TryGetTranslationFromApi(countryCode, TranslationPath, out Dictionary<string, object> translations))
            translations = GenerateDefaultTranslations();

        return translations;
    }

    private static Dictionary<string, object> GenerateDefaultTranslations()
    {
        // Otherwise, create default translations from all mini-games.
        var translations = new Dictionary<string, object>();

        foreach (var ev in AutoEvent.EventManager.Events.OrderBy(r => r.Name))
        {
            ev.InternalTranslation.Name = ev.Name;
            ev.InternalTranslation.Description = ev.Description;
            ev.InternalTranslation.CommandName = ev.CommandName;

            translations.Add(ev.Name, ev.InternalTranslation);
        }

        // Save the translation file
        File.WriteAllText(TranslationPath, YamlConfigParser.Serializer.Serialize(translations));
        return translations;
    }

    private static bool TryGetTranslationFromApi<T>(string countryCode, string path, out T translationFile)
    {
        countryCode = countryCode?.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(countryCode))
        {
            LogManager.Debug($"[ConfigManager] Country code is empty after normalization ('{countryCode}')");
            translationFile = default;
            return false;
        }

        try
        {
            if (!ApiManager.TryGetPluginTranslations(countryCode, out var result) || result is null)
            {
                LogManager.Debug($"[ConfigManager] No translations found from API for country code '{countryCode}'");
                translationFile = default;
                return false;
            }

            // Save as YAML to the requested path so rest of the code can load it
            try
            {
                File.WriteAllText(path, YamlConfigParser.Serializer.Serialize(result));
            }
            catch
            {
                // If saving fails, continue and still return the translations in-memory
            }

            if (typeof(T) == typeof(Dictionary<string, object>))
            {
                translationFile = (T)result;
                return true;
            }

            translationFile = default;
            return false;
        }
        catch (Exception ex)
        {
            LogManager.Error(
                $"[ConfigManager] Failed to get translations from API for '{countryCode}' ({countryCode}): {ex}");
            translationFile = default;
            return false;
        }
    }

    private static void CopyProperties(this object target, object source)
    {
        var type = target.GetType();
        if (type != source.GetType())
        {
            LogManager.Error(
                $"[ConfigManager] Cannot copy properties from different types: {type.FullName} and {source.GetType().FullName}");
            return;
        }

        foreach (var property in type.GetProperties())
            type.GetProperty(property.Name)?.SetValue(target, property.GetValue(source, null), null);
    }
}
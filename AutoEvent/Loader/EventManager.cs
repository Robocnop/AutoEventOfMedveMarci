using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AutoEvent.ApiFeatures;
using AutoEvent.Interfaces;

namespace AutoEvent.Loader;

public class EventManager
{
    private readonly Dictionary<string, Event> _events = new(StringComparer.OrdinalIgnoreCase);

    public Event CurrentEvent { get; set; }

    public List<Event> Events => _events.Values.ToList();

    public bool IsMerLoaded { get; private set; }

    public void RegisterInternalEvents()
    {
        IsMerLoaded = AppDomain.CurrentDomain.GetAssemblies()
            .Any(x => x.FullName.ToLower().Contains("projectmer"));

        if (!IsMerLoaded)
            LogManager.Error(
                "ProjectMER was not detected. Map-based mini-games will not be available until you install ProjectMER.");

        var types = Assembly.GetCallingAssembly().GetTypes();

        foreach (var type in types)
            try
            {
                if (type.IsAbstract || type.IsEnum || type.IsInterface ||
                    type.GetInterfaces().All(x => x != typeof(IEvent)))
                    continue;

                if (Activator.CreateInstance(type) is not Event ev)
                    continue;

                if (!ev.AutoLoad)
                    continue;

                if (ev is IEventMap && !IsMerLoaded)
                    continue;

                ev.Id = _events.Count;
                _events[ev.InternalName] = ev;
            }
            catch (MissingMethodException)
            {
                // No parameterless constructor — skip silently.
            }
            catch (Exception ex)
            {
                LogManager.Error($"[EventLoader] Failed to register event from type '{type.FullName}'.\n{ex}");
            }
    }

    /// <summary>Returns an event matching <paramref name="query" /> by numeric ID, command name, or display name.</summary>
    public Event GetEvent(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return null;

        return TryGetEvent(query, out var ev) ? ev : null;
    }

    /// <summary>
    ///     Tries to find an event by numeric ID, command name, or display name.
    /// </summary>
    public bool TryGetEvent(string query, out Event ev)
    {
        ev = null;
        if (string.IsNullOrWhiteSpace(query))
            return false;

        // 1. By ID
        if (int.TryParse(query, out var id))
        {
            ev = _events.Values.FirstOrDefault(x => x.Id == id);
            return ev != null;
        }

        // 2. By command name (exact, case-insensitive)
        ev = _events.Values.FirstOrDefault(x =>
            string.Equals(x.CommandName, query, StringComparison.OrdinalIgnoreCase));
        if (ev != null) return true;

        // 3. By display name (exact, case-insensitive)
        ev = _events.Values.FirstOrDefault(x =>
            string.Equals(x.Name, query, StringComparison.OrdinalIgnoreCase));
        return ev != null;
    }
}
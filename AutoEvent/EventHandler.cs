using System;
using System.Threading.Tasks;
using AutoEvent.API;
using AutoEvent.API.Enums;
using CustomPlayerEffects;
using InventorySystem.Items;
using InventorySystem.Items.Firearms.Modules;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.ServerEvents;
using LabApi.Events.CustomHandlers;
using LabApi.Features.Wrappers;

namespace AutoEvent;

internal class EventHandler : CustomEventsHandler
{
    public override void OnServerWaveRespawning(WaveRespawningEventArgs ev)
    {
        if (AutoEvent.EventManager.CurrentEvent is not { } activeEvent) return;
        if (activeEvent.EventHandlerSettings.HasFlag(EventFlags.Default))
            ev.IsAllowed = false;
        base.OnServerWaveRespawning(ev);
    }

    public override void OnServerWaveTeamSelecting(WaveTeamSelectingEventArgs ev)
    {
        if (AutoEvent.EventManager.CurrentEvent is not { } activeEvent) return;
        if (activeEvent.EventHandlerSettings.HasFlag(EventFlags.Default))
            ev.IsAllowed = false;
        base.OnServerWaveTeamSelecting(ev);
    }

    public override void OnServerLczDecontaminationStarting(LczDecontaminationStartingEventArgs ev)
    {
        if (AutoEvent.EventManager.CurrentEvent is not { } activeEvent) return;
        if (activeEvent.EventHandlerSettings.HasFlag(EventFlags.Default))
            ev.IsAllowed = false;
        base.OnServerLczDecontaminationStarting(ev);
    }

    public override void OnPlayerPlacingBulletHole(PlayerPlacingBulletHoleEventArgs ev)
    {
        if (AutoEvent.EventManager.CurrentEvent is not { } activeEvent) return;
        if (activeEvent.EventHandlerSettings.HasFlag(EventFlags.IgnoreBulletHole))
            ev.IsAllowed = false;
        base.OnPlayerPlacingBulletHole(ev);
    }

    public override void OnPlayerSpawningRagdoll(PlayerSpawningRagdollEventArgs ev)
    {
        if (AutoEvent.EventManager.CurrentEvent is not { } activeEvent) return;
        if (activeEvent.EventHandlerSettings.HasFlag(EventFlags.IgnoreRagdoll))
            ev.IsAllowed = false;
        base.OnPlayerSpawningRagdoll(ev);
    }

    public override void OnPlayerPlacingBlood(PlayerPlacingBloodEventArgs ev)
    {
        if (AutoEvent.EventManager.CurrentEvent is not { } activeEvent) return;
        if (activeEvent.EventHandlerSettings.HasFlag(EventFlags.IgnoreBloodDecal))
            ev.IsAllowed = false;
        base.OnPlayerPlacingBlood(ev);
    }

    public override void OnServerPickupCreated(PickupCreatedEventArgs ev)
    {
        if (AutoEvent.EventManager.CurrentEvent is not { } activeEvent) return;
        if (activeEvent.EventHandlerSettings.HasFlag(EventFlags.IgnoreDroppingAmmo) &&
            ev.Pickup.Type.GetName().Contains("Ammo"))
            ev.Pickup.Destroy();
        base.OnServerPickupCreated(ev);
    }

    public override void OnPlayerShootingWeapon(PlayerShootingWeaponEventArgs ev)
    {
        if (AutoEvent.EventManager.CurrentEvent is null) return;

        if (!Extensions.InfiniteAmmoList.TryGetValue(ev.Player.NetworkId, out var ammoMode))
            return;

        if (ev.FirearmItem.Base.TryGetModule<MagazineModule>(out var module))
        {
            if (ev.FirearmItem is ParticleDisruptorItem particleDisruptor)
            {
                particleDisruptor.StoredAmmo = module.AmmoMax;
                return;
            }

            var playersAmmo = module.AmmoMax - module.AmmoStored;
            if (ammoMode == AmmoMode.NoReloadInfiniteAmmo)
            {
                module.AmmoStored = playersAmmo;
                return;
            }

            ev.Player.SetAmmo(module.AmmoType, (ushort)playersAmmo);
        }

        if (ev.FirearmItem.Base.TryGetModule<CylinderAmmoModule>(out var revModule))
        {
            var playersAmmo = revModule.AmmoMax - revModule.AmmoStored;
            ev.Player.SetAmmo(revModule.AmmoType, (ushort)playersAmmo);
        }

        base.OnPlayerShootingWeapon(ev);
    }

    public override void OnPlayerDroppingAmmo(PlayerDroppingAmmoEventArgs ev)
    {
        if (AutoEvent.EventManager.CurrentEvent is not { } activeEvent) return;
        if (activeEvent.EventHandlerSettings.HasFlag(EventFlags.IgnoreDroppingAmmo))
            ev.IsAllowed = false;
        base.OnPlayerDroppingAmmo(ev);
    }

    public override void OnPlayerDroppingItem(PlayerDroppingItemEventArgs ev)

    {
        if (AutoEvent.EventManager.CurrentEvent is not { } activeEvent) return;
        if (activeEvent.EventHandlerSettings.HasFlag(EventFlags.IgnoreDroppingItem))
            ev.IsAllowed = false;
        base.OnPlayerDroppingItem(ev);
    }

    public override void OnPlayerCuffing(PlayerCuffingEventArgs ev)
    {
        if (AutoEvent.EventManager.CurrentEvent is not { } activeEvent) return;
        if (activeEvent.EventHandlerSettings.HasFlag(EventFlags.IgnoreHandcuffing))
            ev.IsAllowed = false;
        base.OnPlayerCuffing(ev);
    }

    public override void OnPlayerDeath(PlayerDeathEventArgs ev)
    {
        if (AutoEvent.EventManager.CurrentEvent is null)
            return;

        LogManager.Debug(
            $"Player {ev.Player.Nickname} ({ev.Player.UserId}, {ev.Player.NetworkId}) died. Cleaning up event data.");
        Extensions.InfinityStaminaList.Remove(ev.Player.NetworkId);
        ev.Player.GiveInfiniteAmmo(AmmoMode.None);
        base.OnPlayerDeath(ev);
    }

    public override void OnPlayerChangedSpectator(PlayerChangedSpectatorEventArgs ev)
    {
        if (AutoEvent.EventManager.CurrentEvent is null) return;
        if (ev.NewTarget is null)
        {
            ev.Player.DisableEffect<FogControl>();
            base.OnPlayerChangedSpectator(ev);
            return;
        }

        if (ev.NewTarget.TryGetEffect<FogControl>(out var effect))
            ev.Player.EnableEffect<FogControl>(effect.Intensity);
        else
            ev.Player.DisableEffect<FogControl>();
        base.OnPlayerChangedSpectator(ev);
    }

    public override void OnServerWaitingForPlayers()
    {
        try
        {
            var currentVersion = AutoEvent.Singleton.Version;
            _ = Task.Run(() => AutoEvent.CheckForUpdatesAsync(currentVersion));
        }
        catch (Exception ex)
        {
            LogManager.Error($"Version check could not be started.\n{ex}");
        }

        base.OnServerWaitingForPlayers();
    }

    public override void OnServerRoundRestarted()
    {
        if (AutoEvent.EventManager.CurrentEvent is null) return;
        AutoEvent.EventManager.CurrentEvent.StopEvent();
        base.OnServerRoundRestarted();
    }
}
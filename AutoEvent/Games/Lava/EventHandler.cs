using InventorySystem.Items.Firearms.Modules;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;
using PlayerStatsSystem;

namespace AutoEvent.Games.Lava;

public class EventHandler(Plugin plugin)
{
    public static void OnHurting(PlayerHurtingEventArgs ev)
    {
        if (ev.DamageHandler.DeathScreenText == DeathTranslations.Falldown.DeathscreenTranslation)
        {
            ev.IsAllowed = false;
            return;
        }

        if (ev.Attacker == null || ev.Player == null) return;
        ev.Attacker.SendHitMarker();
        if (ev.DamageHandler is StandardDamageHandler damageHandler)
            damageHandler.Damage = 10;
    }

    public void OnPickedUpItem(PlayerPickedUpItemEventArgs ev)
    {
        if (ev.Item is not FirearmItem firearm2) return;
        if (!firearm2.Base.TryGetModule<MagazineModule>(out var module)) return;
        ev.Player.SendHint(plugin.Translation.Reload, 5);
        ev.Player.AddAmmo(module.AmmoType, (ushort)module.AmmoMax);
        ev.Player.CurrentItem = ev.Item;
    }
}
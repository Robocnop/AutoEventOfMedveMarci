using InventorySystem.Items.Jailbird;
using LabApi.Events.Arguments.PlayerEvents;

namespace AutoEvent.Games.Versus;

public class EventHandler(Plugin plugin)
{
    public void OnDying(PlayerDyingEventArgs ev)

    {
        ev.Player.ClearInventory();

        if (ev.Player == plugin.ClassD)
        {
            plugin.ClassD = null;
            plugin.Scientist.CurrentItem = null;
            plugin.Scientist.RemoveItem(ItemType.Jailbird);
            return;
        }

        if (ev.Player == plugin.Scientist)
        {
            plugin.Scientist = null;
            plugin.ClassD.CurrentItem = null;
            plugin.ClassD.RemoveItem(ItemType.Jailbird);
        }
    }

    public void OnProcessingJailbirdMessage(PlayerProcessingJailbirdMessageEventArgs ev)
    {
        if (ev.Message == JailbirdMessageType.ChargeLoadTriggered)
            ev.IsAllowed = plugin.Config.JailbirdCanCharge;
    }
}
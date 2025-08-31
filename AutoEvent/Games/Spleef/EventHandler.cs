using System.Linq;
using InventorySystem.Items.Firearms.Modules;
using LabApi.Events.Arguments.PlayerEvents;
using UnityEngine;

namespace AutoEvent.Games.Spleef;

public class EventHandler(Plugin plugin)
{
    public void OnShot(PlayerShotWeaponEventArgs ev)

    {
        if (plugin.Config.PlatformHealth < 0) return;

        if (!ev.FirearmItem.Base.TryGetModule<HitscanHitregModuleBase>(out var hitreg))
            return;

        foreach (var platform in hitreg.ResultNonAlloc.Obstacles.FirstOrDefault().Hit.transform
                     .GetComponentsInParent<FallPlatformComponent>()) Object.Destroy(platform);
    }
}
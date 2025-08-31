using UnityEngine;

namespace AutoEvent.Games.Spleef;

public class FallPlatformComponent : MonoBehaviour
{
    private void OnDestroy()
    {
        Destroy(gameObject);
    }
}
using UnityEngine;

namespace Bloodrush.Player
{
public class PlayerPickup : MonoBehaviour
{
    PlayerShoot shoot;

    void Awake()
    {
        // PlayerShoot player root'ta veya herhangi bir child'da olabilir
        shoot = GetComponent<PlayerShoot>()
             ?? GetComponentInChildren<PlayerShoot>(true)
             ?? FindFirstObjectByType<PlayerShoot>();
    }

    void Update()
    {
        if (shoot == null) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, 1.5f);
        foreach (var c in hits)
        {
            var pickup = c.GetComponent<AmmoPickup>()
                      ?? c.GetComponentInParent<AmmoPickup>()
                      ?? c.GetComponentInChildren<AmmoPickup>(true);
            if (pickup != null) { pickup.Collect(shoot); break; }
        }
    }
}
}

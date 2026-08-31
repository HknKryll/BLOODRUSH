using UnityEngine;
using Bloodrush.Shared;

namespace Bloodrush.Player
{
public class PlayerPickup : MonoBehaviour
{
    PlayerShoot shoot;
    Health      health;

    void Awake()
    {
        // PlayerShoot player root'ta veya herhangi bir child'da olabilir
        shoot = GetComponent<PlayerShoot>()
             ?? GetComponentInChildren<PlayerShoot>(true)
             ?? FindFirstObjectByType<PlayerShoot>();
        // Sadece oyuncunun kendi Health'i (FindFirstObjectByType düşmanınkini yakalayabilir)
        health = GetComponent<Health>() ?? GetComponentInParent<Health>();
    }

    void Update()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, 1.5f);
        foreach (var c in hits)
        {
            if (shoot != null)
            {
                var ammo = c.GetComponent<AmmoPickup>()
                        ?? c.GetComponentInParent<AmmoPickup>()
                        ?? c.GetComponentInChildren<AmmoPickup>(true);
                if (ammo != null) { ammo.Collect(shoot); continue; }

                var weapon = c.GetComponent<WeaponPickup>()
                          ?? c.GetComponentInParent<WeaponPickup>()
                          ?? c.GetComponentInChildren<WeaponPickup>(true);
                if (weapon != null) { weapon.Collect(shoot); continue; }
            }
            if (health != null)
            {
                var hp = c.GetComponent<HealthPickup>()
                      ?? c.GetComponentInParent<HealthPickup>()
                      ?? c.GetComponentInChildren<HealthPickup>(true);
                if (hp != null) { hp.Collect(health); }
            }
        }
    }
}
}

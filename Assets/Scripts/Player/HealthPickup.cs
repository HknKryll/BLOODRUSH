using UnityEngine;
using Bloodrush.Shared;
using Bloodrush.Shared.Audio;

namespace Bloodrush.Player
{
// Can paketi — üzerine gelince otomatik toplanır (PlayerPickup çağırır), oyuncuyu
// iyileştirir. AmmoPickup ile aynı desen. Düşmanlar bazen düşürür (EnemyAI) ve/veya
// odalara elle koyulur.
public class HealthPickup : MonoBehaviour
{
    [SerializeField] float     healAmount = 30f;
    [SerializeField] AudioClip pickupClip;

    bool collected;

    public void Collect(Health health)
    {
        if (collected || health == null) return;
        if (health.Current >= health.Max) return;   // tam canda ziyan olmasın — bırak

        collected = true;
        health.Heal(healAmount);
        SfxPlayer.PlayAtPoint(pickupClip, transform.position);
        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 1.5f);
    }
}
}

using System.Collections;
using UnityEngine;
using Bloodrush.Shared;

// EnemyExplosive'in bıraktığı zehirli/asit alanı: içinde duran her Health'e
// periyodik hasar verir, süresi dolunca kendini yok eder. Görsel/VFX ayrı
// eklenebilir (bu obje sadece hasar mantığını taşır).
namespace Bloodrush.Enemy
{
public class HazardZone : MonoBehaviour
{
    float radius;
    float tickDamage;
    float tickInterval;
    LayerMask damageMask;

    public void Init(float radius, float duration, float tickDamage, float tickInterval, LayerMask damageMask)
    {
        this.radius       = radius;
        this.tickDamage   = tickDamage;
        this.tickInterval = tickInterval;
        this.damageMask   = damageMask;
        StartCoroutine(Run(duration));
    }

    IEnumerator Run(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            foreach (var col in Physics.OverlapSphere(transform.position, radius, damageMask))
                col.GetComponentInParent<Health>()?.TakeDamage(tickDamage);

            yield return new WaitForSeconds(tickInterval);
            elapsed += tickInterval;
        }
        Destroy(gameObject);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.2f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
#endif
}
}

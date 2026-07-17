using UnityEngine;
using Bloodrush.Shared;
using Bloodrush.Shared.Audio;
using Bloodrush.FX;

// Şişkin/Patlayıcı tip (STORY_DESIGN.md Bölüm 8, Tip 4): oyuncu yaklaşınca
// telegraph süresi sonunda patlar; öldürülürse de patlar. Alan hasarı verir
// ve isteğe bağlı olarak yerde bir süre hasar veren zehirli/asit alanı bırakır.
//
// Kullanım: EnemyAI + Health olan bir düşman prefabına ekle (yavaş hareket
// etmesi için EnemyAI'da NavMeshAgent hızını düşür). Model/animasyon
// gelene kadar mevcut Enemy/Enemy2 mesh'i placeholder olarak kullanılabilir —
// bu bileşen tamamen görselden bağımsız çalışır.
namespace Bloodrush.Enemy
{
[RequireComponent(typeof(Health))]
public class EnemyExplosive : MonoBehaviour
{
    [Header("Yaklaşınca Patlama")]
    [SerializeField] float proximityRadius = 3.5f;
    [SerializeField] float fuseTime        = 1f;   // telegraph süresi — kaçış penceresi

    [Header("Patlama")]
    [SerializeField] float blastRadius  = 5f;
    [SerializeField] float blastDamage  = 45f;
    [SerializeField] LayerMask damageMask = ~0;
    [SerializeField] GameObject explosionVFX;

    [Header("Zehirli/Asit Alanı (opsiyonel)")]
    [SerializeField] bool  leaveHazardZone    = true;
    [SerializeField] float hazardRadius       = 3f;
    [SerializeField] float hazardDuration     = 4f;
    [SerializeField] float hazardTickDamage   = 6f;
    [SerializeField] float hazardTickInterval = 0.5f;

    [Header("Telegraph Görsel (opsiyonel)")]
    [SerializeField] GameObject fuseIndicator;   // yanıp sönen ışık vs.

    [Header("Ses")]
    [SerializeField] AudioClip primeClip;
    [SerializeField] [Range(0f,1f)] float primeVolume = 0.8f;
    [SerializeField] AudioClip blastClip;
    [SerializeField] [Range(0f,1f)] float blastVolume = 1f;

    Health    health;
    Transform player;
    SfxPlayer sfx;
    bool  priming;
    bool  detonated;
    float primeStart;

    void Awake()
    {
        health = GetComponent<Health>();
        var pgo = GameObject.FindGameObjectWithTag("Player");
        player = pgo ? pgo.transform : null;
        sfx    = SfxPlayer.Create(gameObject, spatialBlend: 1f);
        health.onDeath.AddListener(Detonate);
    }

    void Update()
    {
        if (detonated || player == null) return;

        float dist = Vector3.Distance(transform.position, player.position);

        if (!priming)
        {
            if (dist <= proximityRadius) StartPriming();
        }
        else if (Time.time >= primeStart + fuseTime)
        {
            Detonate();
        }
    }

    void StartPriming()
    {
        priming    = true;
        primeStart = Time.time;
        if (fuseIndicator) fuseIndicator.SetActive(true);
        sfx.Play(primeClip, primeVolume);
    }

    public void Detonate()
    {
        if (detonated) return;
        detonated = true;

        if (fuseIndicator) fuseIndicator.SetActive(false);

        if (explosionVFX) Instantiate(explosionVFX, transform.position, Quaternion.identity);
        SfxPlayer.PlayDetached(blastClip, transform.position, blastVolume);
        CameraShake.Shake(0.18f, 0.22f);

        foreach (var col in Physics.OverlapSphere(transform.position, blastRadius, damageMask))
        {
            var h = col.GetComponentInParent<Health>();
            if (h == null || h == health) continue;
            float d   = Vector3.Distance(transform.position, col.transform.position);
            float dmg = Mathf.Lerp(blastDamage, blastDamage * 0.25f, Mathf.Clamp01(d / blastRadius));
            h.TakeDamage(dmg);
        }

        if (leaveHazardZone)
        {
            var go = new GameObject("HazardZone");
            go.transform.position = transform.position;
            go.AddComponent<HazardZone>().Init(hazardRadius, hazardDuration, hazardTickDamage, hazardTickInterval, damageMask);
        }

        // Kendi ölüm akışı (VFX/ses/drop) EnemyAI.OnDeath'te işleniyor; zaten
        // ölmüşse (onDeath üzerinden geldiyse) tekrar tetiklemez.
        if (health.IsAlive) health.TakeDamage(float.MaxValue);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, proximityRadius);
        Gizmos.color = new Color(1f, 0.2f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, blastRadius);
        if (leaveHazardZone)
        {
            Gizmos.color = new Color(0.4f, 1f, 0.2f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, hazardRadius);
        }
    }
#endif
}
}

using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using Bloodrush.Shared;
using Bloodrush.Shared.Audio;
using Bloodrush.Shared.Pooling;
using Bloodrush.FX;
using Bloodrush.Player;
using Bloodrush.UI;

// FINAL BOSS — kaçmış deneyin nihai evrimi (STORY_DESIGN.md Bölüm 5/9).
// Ch2'nin insan "denetçi" boss'undan (BossAI: pompalı+karanlık) bilinçli olarak
// farklı: bu bir yaratık — fiziksel sıçrayıp yere çakılır (alan hasarı + asit
// bırakır), menzilden mermi salvosu tükürür, yakında parry'lenebilir pençe
// savurur. Faz geçişlerinde çevresine patlama dalgası + asit halkası yayar.
//
// Görsel: model gelene kadar boss.fbx / büyütülmüş Enemy2 mesh'i placeholder.
// Ölünce onDefeated tetiklenir — ara sahne buraya bağlanır (ara sahne hazır
// olana kadar doğrudan EndingSequence.Begin bağlanabilir).
namespace Bloodrush.Enemy
{
[RequireComponent(typeof(Rigidbody))]
public class ExperimentBossAI : BossAIBase
{
    enum State { Chase, Volley, Leaping, MeleeTelegraph, Stunned, PhaseBurst }

    [Header("Hareket")]
    [SerializeField] float moveSpeed = 4f;
    [SerializeField] float phaseSpeedStep = 0.2f;   // her fazda hız çarpanı +bu kadar

    [Header("Sıçrama Saldırısı (yere çakılma)")]
    [SerializeField] float leapRangeMin  = 6f;
    [SerializeField] float leapRangeMax  = 18f;
    [SerializeField] float leapCooldown  = 5f;
    [SerializeField] float leapSpeed     = 16f;
    [SerializeField] float leapArcHeight = 7f;
    [SerializeField] float slamRadius    = 4.5f;
    [SerializeField] float slamDamage    = 30f;

    [Header("Asit Alanı (sıçrama inişi, faz 2+; faz geçişi patlaması)")]
    [SerializeField] float hazardRadius       = 3f;
    [SerializeField] float hazardDuration     = 4f;
    [SerializeField] float hazardTickDamage   = 6f;
    [SerializeField] float hazardTickInterval = 0.5f;

    [Header("Salvo (menzilli)")]
    [SerializeField] EnemyProjectile projectilePrefab;
    [SerializeField] Transform muzzle;
    [SerializeField] int   volleyCount      = 6;
    [SerializeField] float volleyShotDelay  = 0.18f;
    [SerializeField] float volleySpread     = 3f;
    [SerializeField] float projectileSpeed  = 24f;
    [SerializeField] float projectileDamage = 7f;
    [SerializeField] float[] volleyInterval = { 4f, 3f, 2.2f };   // faz başına salvo arası

    [Header("Pençe (melee, parry'lenebilir)")]
    [SerializeField] float meleeRange     = 3f;
    [SerializeField] float meleeDamage    = 30f;
    [SerializeField] float meleeKnockback = 14f;
    [SerializeField] float meleeTelegraph = 0.6f;
    [SerializeField] float meleeCooldown  = 2f;
    [SerializeField] float parryStunTime  = 2.5f;

    [Header("Faz Geçişi Patlaması")]
    [SerializeField] float burstWindup   = 0.8f;
    [SerializeField] float burstRadius   = 6f;
    [SerializeField] float burstDamage   = 25f;
    [SerializeField] int   burstHazards  = 4;    // patlamayla çevreye saçılan asit alanı sayısı
    [SerializeField] float burstHazardDist = 5f; // asit alanlarının merkeze uzaklığı

    [Header("Telegraph Göstergeleri (opsiyonel)")]
    [SerializeField] GameObject meleeIndicator;
    [SerializeField] GameObject burstIndicator;

    [Header("Efekt / Ses")]
    [SerializeField] GameObject slamVFX;         // iniş/patlama görseli (opsiyonel)
    [SerializeField] AudioClip roarClip;
    [SerializeField] [Range(0f,1f)] float roarVolume = 1f;
    [SerializeField] AudioClip slamClip;
    [SerializeField] [Range(0f,1f)] float slamVolume = 1f;
    [SerializeField] AudioClip meleeClip;
    [SerializeField] [Range(0f,1f)] float meleeVolume = 1f;
    [SerializeField] AudioClip volleyClip;
    [SerializeField] [Range(0f,1f)] float volleyVolume = 0.8f;

    [Header("Bitince")]
    public UnityEvent onDefeated;    // ara sahne / EndingSequence.Begin buraya bağlanır

    State state = State.Chase;
    float nextVolleyTime;
    float stunUntil;

    Rigidbody rb;

    BossMeleeAttack   meleeAttack;
    EnemyLeapBehavior leap;

    protected override void Awake()
    {
        base.Awake();

        rb     = GetComponent<Rigidbody>();
        agent.updateRotation = false;    // nişan için elle döneceğiz
        rb.isKinematic = true;           // sadece sıçrama sırasında fizik açılır
        rb.useGravity  = false;

        meleeAttack = new BossMeleeAttack(meleeRange, meleeDamage, meleeKnockback,
            meleeTelegraph, meleeCooldown, sfx, meleeClip, meleeVolume);
        leap = new EnemyLeapBehavior(leapRangeMin, leapRangeMax, leapCooldown, leapSpeed, leapArcHeight);

        Hide(meleeIndicator);
        Hide(burstIndicator);
    }

    void Update()
    {
        if (dead || player == null) return;
        if (state == State.Leaping || state == State.PhaseBurst) return;   // coroutine yönetiyor

        if (state == State.Stunned)
        {
            if (Time.time >= stunUntil) state = State.Chase;
            return;
        }

        CheckPhaseTransition();
        if (state == State.PhaseBurst) return;

        float dist = Vector3.Distance(transform.position, player.position);
        FacePlayer();
        UpdateHealthBar(dist);

        switch (state)
        {
            case State.Chase:
                DoChase(dist);
                break;

            case State.Volley:
                // Coroutine ateş ediyor; burada sadece oyuncuya dönük kal
                break;

            case State.MeleeTelegraph:
                if (meleeAttack.TickTelegraph())
                {
                    Hide(meleeIndicator);
                    meleeAttack.ResolveTelegraph(transform, player, playerHealth, playerMovement);
                    state = State.Chase;
                }
                break;
        }
    }

    // ───────── Faz ─────────

    protected override void OnPhaseAdvanced() => StartCoroutine(PhaseBurstRoutine());

    float VolleyInterval => volleyInterval[Mathf.Clamp(phase - 1, 0, volleyInterval.Length - 1)];
    float PhaseSpeedMult => 1f + (phase - 1) * phaseSpeedStep;

    // ───────── Chase ─────────

    void DoChase(float dist)
    {
        if (agent.enabled) agent.speed = moveSpeed * PhaseSpeedMult;

        // Yakında pençe
        if (dist <= meleeRange && Time.time >= meleeAttack.ReadyTime)
        {
            state = State.MeleeTelegraph;
            meleeAttack.StartTelegraph();
            if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();
            Show(meleeIndicator);
            return;
        }

        // Orta menzilde sıçrayıp çakıl
        if (leap.ReadyToLeap(dist))
        {
            StartLeap();
            return;
        }

        // Takip + zamanı gelince salvo
        if (agent.enabled && agent.isOnNavMesh) agent.SetDestination(player.position);

        if (Time.time >= nextVolleyTime && HasLineOfSight())
            StartCoroutine(VolleyRoutine());
    }

    // ───────── Sıçrama ─────────

    void StartLeap()
    {
        state = State.Leaping;
        Hide(meleeIndicator);
        sfx.Play(roarClip, roarVolume);
        StartCoroutine(leap.Run(transform, agent, rb, player.position, OnLeapLanded));
    }

    void OnLeapLanded()
    {
        // Yere çakılma: alan hasarı + (faz 2+) asit alanı
        sfx.Play(slamClip, slamVolume);
        CameraShake.Shake(0.25f, 0.25f);
        if (slamVFX) Instantiate(slamVFX, transform.position, Quaternion.identity);

        if (playerHealth != null)
        {
            float d = Vector3.Distance(transform.position, player.position);
            if (d <= slamRadius)
                playerHealth.TakeDamage(Mathf.Lerp(slamDamage, slamDamage * 0.3f, d / slamRadius));
        }

        if (phase >= 2) SpawnHazard(transform.position);

        state = State.Chase;
    }

    // ───────── Salvo ─────────

    IEnumerator VolleyRoutine()
    {
        state = State.Volley;
        nextVolleyTime = Time.time + VolleyInterval;
        if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();

        for (int i = 0; i < volleyCount; i++)
        {
            if (dead || player == null) yield break;
            if (state != State.Volley) yield break;   // faz patlaması/parry araya girdi
            FireProjectile();
            yield return new WaitForSeconds(volleyShotDelay);
        }

        if (state == State.Volley) state = State.Chase;
    }

    void FireProjectile()
    {
        if (projectilePrefab == null) return;

        // Muzzle atanmadıysa gövde DIŞINA spawn et — boss'un EnemyAI'ı olmadığı
        // için kendi collider'ına doğan mermi anında yok olur (EnemyProjectile
        // sadece EnemyAI'lı düşmanların içinden geçer)
        Vector3 dir    = (player.position + Vector3.up * 0.5f - transform.position - Vector3.up * 1.6f).normalized;
        Vector3 origin = muzzle ? muzzle.position
                                : transform.position + Vector3.up * 1.6f + dir * 2f;

        if (volleySpread > 0f)
            dir = Quaternion.Euler(Random.Range(-volleySpread, volleySpread),
                                   Random.Range(-volleySpread, volleySpread), 0f) * dir;

        var proj = PoolManager.Get(projectilePrefab, origin, Quaternion.LookRotation(dir));
        proj.Launch(dir, projectileSpeed, projectileDamage);
        sfx.Play(volleyClip, volleyVolume);
    }

    // ───────── Faz geçişi patlaması ─────────

    IEnumerator PhaseBurstRoutine()
    {
        state = State.PhaseBurst;
        Hide(meleeIndicator);
        if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();

        Show(burstIndicator);
        sfx.Play(roarClip, roarVolume);
        yield return new WaitForSeconds(burstWindup);
        Hide(burstIndicator);

        // Patlama: yakın alan hasarı
        sfx.Play(slamClip, slamVolume);
        CameraShake.Shake(0.35f, 0.3f);
        if (slamVFX) Instantiate(slamVFX, transform.position, Quaternion.identity);

        if (playerHealth != null)
        {
            float d = Vector3.Distance(transform.position, player.position);
            if (d <= burstRadius)
                playerHealth.TakeDamage(Mathf.Lerp(burstDamage, burstDamage * 0.3f, d / burstRadius));
        }

        // Çevreye asit halkası
        for (int i = 0; i < burstHazards; i++)
        {
            float ang = (float)i / burstHazards * 2f * Mathf.PI;
            Vector3 pos = transform.position + new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * burstHazardDist;
            SpawnHazard(pos);
        }

        nextVolleyTime = Time.time + 1f;   // patlama sonrası kısa nefes
        state = State.Chase;
    }

    void SpawnHazard(Vector3 pos)
    {
        var go = new GameObject("BossHazard");
        go.transform.position = pos;
        go.AddComponent<HazardZone>().Init(hazardRadius, hazardDuration,
            hazardTickDamage, hazardTickInterval, ~0, ignore: health);
    }

    // ───────── IParryable ─────────

    public override bool IsParryable => state == State.MeleeTelegraph;

    public override void Parry(float stunDuration)
    {
        Hide(meleeIndicator);
        state     = State.Stunned;
        stunUntil = Time.time + parryStunTime;
        if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();
    }

    // ───────── Yardımcılar ─────────

    // ExperimentBossAI'nin kendi kopyaladığı raycast mantığı yerine paylaşılan
    // EnemyVision servisi kullanılıyor (bkz. BLOODRUSH_YENIDEN_YAPILANDIRMA_PLANI.md, Faz 4).
    bool HasLineOfSight()
    {
        Vector3 origin = muzzle ? muzzle.position : transform.position + Vector3.up * 1.6f;
        Vector3 target = player.position + Vector3.up * 0.5f;
        return EnemyVision.Clear(origin, target, transform);
    }

    void Show(GameObject go) { if (go) go.SetActive(true); }
    void Hide(GameObject go) { if (go) go.SetActive(false); }

    protected override void OnBossDeathEffects()
    {
        Hide(meleeIndicator);
        Hide(burstIndicator);
    }

    protected override void OnBossDeathFinish()
    {
        // Obje YOK EDİLMEZ — ara sahne boss'un transform'una ihtiyaç duyabilir;
        // sahne zaten final akışıyla kapanacak.
        onDefeated?.Invoke();
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, slamRadius);
        Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, burstRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, leapRangeMax);
    }
#endif
}
}

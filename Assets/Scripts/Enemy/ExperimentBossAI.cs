using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using Bloodrush.Shared;
using Bloodrush.Shared.Audio;
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
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(Rigidbody))]
public class ExperimentBossAI : MonoBehaviour, IParryable
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

    [Header("Can Barı")]
    [SerializeField] string bossName     = "DENEY";
    [SerializeField] float  barShowRange = 35f;   // oyuncu bu mesafeye girince bar belirir

    [Header("Bitince")]
    public UnityEvent onDefeated;    // ara sahne / EndingSequence.Begin buraya bağlanır

    State state = State.Chase;
    int   phase = 1;                 // 1: >66%, 2: 66-33%, 3: <33%
    float nextVolleyTime;
    float stunUntil;
    bool  dead;
    bool  barShown;

    NavMeshAgent   agent;
    Rigidbody      rb;
    Health         health;
    Transform      player;
    Health         playerHealth;
    PlayerMovement playerMovement;
    SfxPlayer      sfx;
    Renderer[]     renderers;

    BossMeleeAttack   meleeAttack;
    EnemyLeapBehavior leap;

    void Awake()
    {
        agent  = GetComponent<NavMeshAgent>();
        rb     = GetComponent<Rigidbody>();
        health = GetComponent<Health>();
        agent.updateRotation = false;    // nişan için elle döneceğiz
        rb.isKinematic = true;           // sadece sıçrama sırasında fizik açılır
        rb.useGravity  = false;

        var pgo = GameObject.FindGameObjectWithTag("Player");
        if (pgo != null)
        {
            player         = pgo.transform;
            playerHealth   = pgo.GetComponent<Health>();
            playerMovement = pgo.GetComponent<PlayerMovement>();
        }

        sfx = SfxPlayer.Create(gameObject, spatialBlend: 1f);

        renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            smr.updateWhenOffscreen = true;   // büyük model yanlış culling ile kaybolmasın

        meleeAttack = new BossMeleeAttack(meleeRange, meleeDamage, meleeKnockback,
            meleeTelegraph, meleeCooldown, sfx, meleeClip, meleeVolume);
        leap = new EnemyLeapBehavior(leapRangeMin, leapRangeMax, leapCooldown, leapSpeed, leapArcHeight);

        health.onDeath.AddListener(OnDeath);

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

        // Oyuncu boss alanına girince can barını göster (bir kez)
        if (!barShown && dist <= barShowRange)
        {
            BossHealthUI.ShowBoss(health, bossName);
            barShown = true;
        }

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

    void CheckPhaseTransition()
    {
        float frac = health.Max > 0f ? health.Current / health.Max : 1f;
        if      (phase == 1 && frac <= 0.66f) { phase = 2; StartCoroutine(PhaseBurstRoutine()); }
        else if (phase == 2 && frac <= 0.33f) { phase = 3; StartCoroutine(PhaseBurstRoutine()); }
    }

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

        var proj = Instantiate(projectilePrefab, origin, Quaternion.LookRotation(dir));
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

    public bool IsParryable => state == State.MeleeTelegraph;

    public void Parry(float stunDuration)
    {
        Hide(meleeIndicator);
        state     = State.Stunned;
        stunUntil = Time.time + parryStunTime;
        if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();
    }

    // Büyük — kanca/yumruk işlemez (GrapplingHook/PlayerParry kontrol eder)
    public bool IsLarge => true;

    // ───────── Yardımcılar ─────────

    bool HasLineOfSight()
    {
        Vector3 origin = muzzle ? muzzle.position : transform.position + Vector3.up * 1.6f;
        Vector3 target = player.position + Vector3.up * 0.5f;
        Vector3 dir    = target - origin;

        foreach (var h in Physics.RaycastAll(origin, dir.normalized, dir.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform)) continue;                      // kendi gövden
            if (h.collider.GetComponentInParent<PlayerMovement>() != null) continue;      // oyuncu engel değil
            return false;   // duvar
        }
        return true;
    }

    void FacePlayer()
    {
        Vector3 dir = player.position - transform.position; dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 8f);
    }

    void Show(GameObject go) { if (go) go.SetActive(true); }
    void Hide(GameObject go) { if (go) go.SetActive(false); }

    void OnDeath()
    {
        if (dead) return;
        dead = true;
        StopAllCoroutines();
        Hide(meleeIndicator);
        Hide(burstIndicator);
        if (agent.enabled) agent.enabled = false;

        foreach (var r in renderers) if (r != null) r.enabled = false;
        enabled = false;

        BossHealthUI.HideBoss();

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

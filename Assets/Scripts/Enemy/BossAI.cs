using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

// Ch2 Boss: pompalı (hitscan koni), cana bağlı 3 faz, %66/%33'te ışık söndürüp
// arkaya ışınlanma, uzakta kemp yapılırsa agresif dash, yakında parry'lenebilir
// kabza vuruşu + oyuncuyu geri itme. Büyük düşman: kanca/yumruk işlemez.
using Bloodrush.Shared;
using Bloodrush.Shared.Audio;
using Bloodrush.FX;
using Bloodrush.Flow;
using Bloodrush.Player;
using Bloodrush.UI;

namespace Bloodrush.Enemy
{
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Health))]
public class BossAI : MonoBehaviour, IParryable
{
    enum State { Chase, ShotgunAim, MeleeTelegraph, Dash, Blackout, Stunned }

    [Header("Hareket")]
    [SerializeField] float moveSpeed = 4.5f;

    [Header("Pompalı (hitscan koni)")]
    [SerializeField] Transform muzzle;
    [SerializeField] float shotgunRange = 18f;
    [SerializeField] float damageNear   = 55f;
    [SerializeField] float damageFar    = 12f;
    [SerializeField] float coneAngle    = 22f;
    [SerializeField] float aimTime      = 0.6f;   // nişan telegraph süresi (kaçış penceresi)
    [SerializeField] LayerMask obstacleMask = ~0;
    [SerializeField] float[] fireInterval = { 3f, 2.5f, 2f };  // faz başına ateş aralığı

    [Header("Kabza (melee, parry'lenebilir)")]
    [SerializeField] float meleeRange     = 2.6f;
    [SerializeField] float meleeDamage    = 28f;
    [SerializeField] float meleeKnockback = 15f;
    [SerializeField] float meleeTelegraph = 0.55f;
    [SerializeField] float meleeCooldown  = 1.5f;
    [SerializeField] float parryStunTime  = 2.5f;

    [Header("Dash (anti-revolver)")]
    [SerializeField] float dashTriggerRange = 12f;
    [SerializeField] float dashTriggerTime  = 2.5f;
    [SerializeField] float dashSpeed        = 22f;

    [Header("Karanlık (faz geçişi)")]
    [SerializeField] float blackoutDuration   = 1.5f;
    [SerializeField] float teleportBehindDist = 3f;

    [Header("Telegraph Göstergeleri (opsiyonel)")]
    [SerializeField] GameObject aimIndicator;
    [SerializeField] GameObject meleeIndicator;

    [Header("Efekt / Ses")]
    [SerializeField] ParticleSystem muzzleFlash;
    [SerializeField] AudioClip fireClip;
    [SerializeField] [Range(0f,1f)] float fireVolume = 1f;
    [SerializeField] AudioClip meleeClip;
    [SerializeField] [Range(0f,1f)] float meleeVolume = 1f;

    [Header("Can Barı")]
    [SerializeField] string bossName     = "KONSEY DENETÇİSİ";
    [SerializeField] float  barShowRange = 35f;

    [Header("Ölünce Düşen Silahlar")]
    [Tooltip("Boss ölünce yere düşen silah pickup prefabları (WeaponPickup içeren). CH2: Shotgun + LMG.")]
    [SerializeField] GameObject[] weaponDropPrefabs;
    [Tooltip("Düşüş saçılma yarıçapı (m).")]
    [SerializeField] float weaponDropSpread = 1.6f;

    State state = State.Chase;
    int   phase = 1;              // 1: >66%, 2: 66-33%, 3: <33%
    float fireTimer;
    float farTimer;               // dash tetiği için "uzakta durma" süresi
    float stunUntil;
    float logTimer;
    bool  dead;
    bool  barShown;

    NavMeshAgent   agent;
    Health         health;
    Transform      player;
    Health         playerHealth;
    PlayerMovement playerMovement;
    SfxPlayer      sfx;
    Renderer[]     renderers;

    BossShotgunAttack     shotgunAttack;
    BossMeleeAttack       meleeAttack;
    BossBlackoutSequence  blackout;

    void Awake()
    {
        agent  = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        agent.updateRotation = false;   // nişan için elle döneceğiz

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

        shotgunAttack = new BossShotgunAttack(muzzle, shotgunRange, damageNear, damageFar,
            coneAngle, aimTime, obstacleMask, muzzleFlash, sfx, fireClip, fireVolume);
        meleeAttack = new BossMeleeAttack(meleeRange, meleeDamage, meleeKnockback,
            meleeTelegraph, meleeCooldown, sfx, meleeClip, meleeVolume);
        blackout = new BossBlackoutSequence(agent, teleportBehindDist, blackoutDuration, renderers);

        health.onDeath.AddListener(OnDeath);

        HideIndicator(aimIndicator);
        HideIndicator(meleeIndicator);
    }

    void Update()
    {
        if (dead || player == null) return;
        if (state == State.Blackout) return;   // coroutine yönetiyor

        if (state == State.Stunned)
        {
            if (Time.time >= stunUntil) state = State.Chase;
            return;
        }

        CheckPhaseTransition();
        if (state == State.Blackout) return;   // geçiş karanlığı başlattıysa

        float dist = Vector3.Distance(transform.position, player.position);
        FacePlayer();
        UpdateDashTimer(dist);

        // Oyuncu boss alanına girince can barını göster (bir kez)
        if (!barShown && dist <= barShowRange)
        {
            BossHealthUI.ShowBoss(health, bossName);
            barShown = true;
        }

        logTimer += Time.deltaTime;
        if (logTimer >= 1f)
        {
            logTimer = 0f;
            Debug.Log($"[Boss] state={state} dist={dist:F1} onNavMesh={agent.isOnNavMesh} " +
                      $"agentEnabled={agent.enabled} hasPath={(agent.enabled && agent.isOnNavMesh && agent.hasPath)} phase={phase}", this);
        }

        switch (state)
        {
            case State.Chase:
                DoChase(dist);
                break;

            case State.ShotgunAim:
                if (shotgunAttack.TickAim())
                {
                    HideIndicator(aimIndicator);
                    shotgunAttack.Fire(transform, player, playerHealth);
                    fireTimer = 0f;
                    state = State.Chase;
                }
                break;

            case State.MeleeTelegraph:
                if (meleeAttack.TickTelegraph())
                {
                    HideIndicator(meleeIndicator);
                    meleeAttack.ResolveTelegraph(transform, player, playerHealth, playerMovement);
                    state = State.Chase;
                }
                break;

            case State.Dash:
                DoDash(dist);
                break;
        }
    }

    // ───────── Faz ─────────

    void CheckPhaseTransition()
    {
        float frac = health.Max > 0f ? health.Current / health.Max : 1f;
        if (phase == 1 && frac <= 0.66f) { phase = 2; StartCoroutine(RunBlackout()); }
        else if (phase == 2 && frac <= 0.33f) { phase = 3; StartCoroutine(RunBlackout()); }
    }

    float FireInterval => fireInterval[Mathf.Clamp(phase - 1, 0, fireInterval.Length - 1)];
    float PhaseSpeedMult => 1f + (phase - 1) * 0.15f;

    // ───────── Chase ─────────

    void DoChase(float dist)
    {
        if (agent.enabled) agent.speed = moveSpeed * PhaseSpeedMult;

        // Yakınsa kabza
        if (dist <= meleeRange && Time.time >= meleeAttack.ReadyTime)
        {
            StartMelee();
            return;
        }

        // Pompalı menzilinde kal
        if (agent.enabled && agent.isOnNavMesh)
        {
            if (dist > shotgunRange * 0.75f) agent.SetDestination(player.position);
            else agent.ResetPath();
        }

        // Ateş zamanı
        fireTimer += Time.deltaTime;
        if (fireTimer >= FireInterval && shotgunAttack.HasLineOfSight(transform, player) && dist <= shotgunRange)
            StartShotgunAim();
    }

    // ───────── Dash (anti-revolver) ─────────

    void UpdateDashTimer(float dist)
    {
        if (state != State.Chase) { farTimer = 0f; return; }
        if (dist > dashTriggerRange)
        {
            farTimer += Time.deltaTime;
            if (farTimer >= dashTriggerTime) { StartDash(); farTimer = 0f; }
        }
        else farTimer = 0f;
    }

    void StartDash() => state = State.Dash;

    void DoDash(float dist)
    {
        if (agent.enabled)
        {
            agent.speed = dashSpeed;
            if (agent.isOnNavMesh) agent.SetDestination(player.position);
        }

        // Menzile girince pompalı bas, chase'e dön
        if (dist <= shotgunRange * 0.6f)
        {
            StartShotgunAim();
        }
        else if (dist <= meleeRange)
        {
            StartMelee();
        }
    }

    // ───────── Pompalı ─────────

    void StartShotgunAim()
    {
        state = State.ShotgunAim;
        shotgunAttack.StartAim();
        if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();
        ShowIndicator(aimIndicator);
    }

    // ───────── Kabza (melee) ─────────

    void StartMelee()
    {
        state = State.MeleeTelegraph;
        meleeAttack.StartTelegraph();
        if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();
        ShowIndicator(meleeIndicator);
    }

    // IParryable — sadece kabza telegraph'ında parry'lenir
    public bool IsParryable => state == State.MeleeTelegraph;

    public void Parry(float stunDuration)
    {
        HideIndicator(meleeIndicator);
        state     = State.Stunned;
        stunUntil = Time.time + parryStunTime;
        if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();
    }

    // ───────── Karanlık (faz geçişi) ─────────

    IEnumerator RunBlackout()
    {
        state = State.Blackout;
        HideIndicator(aimIndicator);
        HideIndicator(meleeIndicator);
        if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();

        yield return StartCoroutine(blackout.Run(transform, player, FacePlayer));

        fireTimer = 0f;
        state = State.Chase;
    }

    // ───────── Yardımcılar ─────────

    void FacePlayer()
    {
        Vector3 dir = player.position - transform.position; dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 8f);
    }

    void SetRenderers(bool visible)
    {
        foreach (var r in renderers) if (r != null) r.enabled = visible;
    }

    void ShowIndicator(GameObject go) { if (go) go.SetActive(true); }
    void HideIndicator(GameObject go) { if (go) go.SetActive(false); }

    void OnDeath()
    {
        if (dead) return;
        dead = true;
        StopAllCoroutines();
        blackout.CleanupOnDeath();                         // karanlıkta öldüyse ışıkları geri ver + overlay'i kaldır
        if (agent.enabled) agent.enabled = false;
        SetRenderers(false);
        enabled = false;
        BossHealthUI.HideBoss();
        DropWeapons();                 // silahlar yere düşer → oyuncu alır → kalıcı açılır
        Destroy(gameObject, 0.1f);
    }

    // Boss ölünce silahları yanına saçarak düşür (WeaponPickup üstüne gidince açılır)
    void DropWeapons()
    {
        if (weaponDropPrefabs == null) return;
        foreach (var prefab in weaponDropPrefabs)
        {
            if (prefab == null) continue;
            Vector2 off = Random.insideUnitCircle * weaponDropSpread;
            Vector3 pos = transform.position + new Vector3(off.x, 0.6f, off.y);
            Instantiate(prefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        }
    }

    // Boss büyük — kanca/yumruk işlemez (kancanın kontrol edeceği bilgi)
    public bool IsLarge => true;
}
}

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
public class BossAI : BossAIBase, EnemyAnimEvents.IAnimEventReceiver
{
    enum State { Chase, ShotgunAim, MeleeTelegraph, Dash, Dodge, Blackout, Stunned }

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

    [Header("Dodge (yana kayma)")]
    [Tooltip("Yana kayma mesafesi (m). Root motion KAPALI — mesafeyi kod verir, animasyon görsel.")]
    [SerializeField] float dodgeDistance = 3.5f;
    [SerializeField] float dodgeDuration = 0.35f;
    [SerializeField] float dodgeCooldown = 4f;
    [Tooltip("Oyuncu bu mesafedeyken kaçmayı dener.")]
    [SerializeField] float dodgeRange    = 14f;
    [Tooltip("Saniyede bir yapılan denemenin başarı olasılığı.")]
    [Range(0f, 1f)]
    [SerializeField] float dodgeChance   = 0.35f;

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

    [Header("Ölünce Düşen Silahlar")]
    [Tooltip("Boss ölünce yere düşen silah pickup prefabları (WeaponPickup içeren). CH2: Shotgun + LMG.")]
    [SerializeField] GameObject[] weaponDropPrefabs;
    [Tooltip("Düşüş saçılma yarıçapı (m).")]
    [SerializeField] float weaponDropSpread = 1.6f;

    State state = State.Chase;
    float fireTimer;
    float farTimer;               // dash tetiği için "uzakta durma" süresi
    float stunUntil;
    float logTimer;

    BossShotgunAttack     shotgunAttack;
    BossMeleeAttack       meleeAttack;
    BossBlackoutSequence  blackout;

    EnemyAnimator animator;
    Vector3 lastAnimPos;
    float   animSpeed;

    bool    shotPending;        // animasyonun ateş karesi bekleniyor
    float   shotPendingSince;
    const float FireEventTimeout = 0.35f;   // event gelmezse yine de ateşle (klip/rig yoksa)

    float   dodgeUntil, dodgeReadyAt, dodgeCheckTimer;
    Vector3 dodgeDir;

    protected override void Awake()
    {
        base.Awake();
        agent.updateRotation = false;   // nişan için elle döneceğiz

        var bodyAnim = GetComponentInChildren<Animator>();
        animator     = new EnemyAnimator(bodyAnim);
        lastAnimPos  = transform.position;

        // Animation Event köprüsü kendiliğinden kurulur (Unity event'i Animator'ın objesine yollar).
        if (bodyAnim != null && bodyAnim.GetComponent<EnemyAnimEvents>() == null)
            bodyAnim.gameObject.AddComponent<EnemyAnimEvents>();

        // ROOT MOTION KAPALI OLMAK ZORUNDA: hareketi NavMeshAgent veriyor. Açık kalırsa klip
        // modeli kendi içinde ileri yürütür, model collider'ından/kökünden uzaklaşır.
        if (bodyAnim != null) bodyAnim.applyRootMotion = false;

        shotgunAttack = new BossShotgunAttack(muzzle, shotgunRange, damageNear, damageFar,
            coneAngle, aimTime, obstacleMask, muzzleFlash, sfx, fireClip, fireVolume);
        meleeAttack = new BossMeleeAttack(meleeRange, meleeDamage, meleeKnockback,
            meleeTelegraph, meleeCooldown, sfx, meleeClip, meleeVolume);
        blackout = new BossBlackoutSequence(agent, teleportBehindDist, blackoutDuration, renderers);

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
        UpdateHealthBar(dist);
        UpdateAnimSpeed();

        // Üst gövde nişan layer'ı (sadece değişince yazılır).
        animator.SetAiming(state == State.ShotgunAim || shotPending);

        // Animation Event gelmediyse (klip yok / event eklenmemiş) atış yine de yapılır.
        if (shotPending && Time.time - shotPendingSince >= FireEventTimeout) FireShotgun();

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
                    // Atış anı animasyondan gelir: klipteki ateş karesindeki Animation Event
                    // (AnimFire) tetikler. Fire parametresi yoksa anında ateşlenir.
                    if (animator.Fire())
                    {
                        shotPending      = true;
                        shotPendingSince = Time.time;
                    }
                    else FireShotgun();

                    fireTimer = 0f;
                    state = State.Chase;
                }
                break;

            case State.Dodge:
                DoDodge();
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

    protected override void OnPhaseAdvanced() => StartCoroutine(RunBlackout());

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
        {
            StartShotgunAim();
            return;
        }

        // Saniyede bir yana kayma denemesi.
        dodgeCheckTimer += Time.deltaTime;
        if (dodgeCheckTimer >= 1f)
        {
            dodgeCheckTimer = 0f;
            TryStartDodge(dist);
        }
    }

    // ───────── Dodge (yana kayma) ─────────

    void TryStartDodge(float dist)
    {
        if (Time.time < dodgeReadyAt || dist > dodgeRange) return;
        if (Random.value > dodgeChance) return;

        dodgeReadyAt = Time.time + dodgeCooldown;
        dodgeUntil   = Time.time + dodgeDuration;
        dodgeDir     = Random.value < 0.5f ? -transform.right : transform.right;
        state        = State.Dodge;

        if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();
        animator.Dodge();
    }

    // Root motion KAPALI: mesafeyi kod verir, animasyon sadece görsel.
    // agent.Move kullanılıyor — NavMesh sınırlarına saygı duyar, ışınlanma yok.
    // agent.enabled/isStopped ile OYNANMIYOR: ikisi de eski "inactive agent" hatalarının kaynağıydı.
    void DoDodge()
    {
        FacePlayer();

        if (agent.enabled && agent.isOnNavMesh)
        {
            float speed = dodgeDistance / Mathf.Max(0.05f, dodgeDuration);
            agent.Move(dodgeDir * speed * Time.deltaTime);
        }

        if (Time.time >= dodgeUntil) state = State.Chase;
    }

    void FireShotgun()
    {
        shotPending = false;
        shotgunAttack.Fire(transform, player, playerHealth);
    }

    // ───────── Animation Event (EnemyAnimEvents köprüsü) ─────────

    public void AnimFire()
    {
        if (shotPending) FireShotgun();
    }

    public void AnimDodgeEnd()
    {
        if (state == State.Dodge) state = State.Chase;
    }

    // Hız agent.velocity'den DEĞİL gerçek yer değiştirmeden ölçülür — agent bir an NavMesh
    // dışına düşse bile koşma animasyonu Idle'a snap etmesin (EnemyAI'daki ile aynı yöntem).
    void UpdateAnimSpeed()
    {
        if (!animator.HasAnimator) return;

        float dt = Mathf.Max(Time.deltaTime, 1e-4f);
        float measured = (transform.position - lastAnimPos).magnitude / dt;
        lastAnimPos = transform.position;

        if (measured > 50f) measured = animSpeed;   // ışınlanma/blackout sıçraması
        animSpeed = Mathf.Lerp(animSpeed, measured, dt * 10f);
        animator.SetSpeed(animSpeed);
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
    public override bool IsParryable => state == State.MeleeTelegraph;

    public override void Parry(float stunDuration)
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

    void ShowIndicator(GameObject go) { if (go) go.SetActive(true); }
    void HideIndicator(GameObject go) { if (go) go.SetActive(false); }

    protected override void OnBossDeathEffects()
    {
        blackout.CleanupOnDeath();   // karanlıkta öldüyse ışıkları geri ver + overlay'i kaldır
    }

    protected override void OnBossDeathFinish()
    {
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
}
}

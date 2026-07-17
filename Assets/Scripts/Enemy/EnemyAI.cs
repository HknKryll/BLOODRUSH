using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Bloodrush.Shared;
using Bloodrush.Shared.Audio;
using Bloodrush.FX;
using Bloodrush.Player;

namespace Bloodrush.Enemy
{
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(Rigidbody))]
public class EnemyAI : MonoBehaviour, IParryable
{
    // ───── Durum makinesi ─────
    enum State { Patrol, Chase, Attack, Telegraphing, Stunned, RangedFire }
    State state = State.Patrol;

    // ───── Davranış ─────
    enum Behavior { Melee, Ranged }
    [Header("Davranış")]
    [SerializeField] Behavior behavior = Behavior.Melee;
    [SerializeField] float playerKnockback = 0f;   // melee vuruşunda oyuncuyu itme (büyük düşman)

    // ───── Algılama ─────
    [Header("Algılama")]
    [SerializeField] float sightRange = 20f;
    [SerializeField] LayerMask obstacleMask;          // duvar/engel katmanı

    // ───── Saldırı (melee) ─────
    [Header("Saldırı")]
    [SerializeField] float attackRange = 2f;
    [SerializeField] float attackDamage = 15f;
    [SerializeField] float attackCooldown = 1.2f;

    // ───── Menzilli (behavior = Ranged) ─────
    [Header("Menzilli")]
    [SerializeField] EnemyProjectile projectilePrefab;
    [SerializeField] Transform muzzle;
    [SerializeField] float rangedRange      = 15f;
    [SerializeField] int   magSize          = 10;
    [SerializeField] float fireRate         = 0.15f;
    [SerializeField] float reloadTime       = 2f;
    [SerializeField] float projectileSpeed  = 22f;
    [SerializeField] float projectileDamage = 5f;
    [SerializeField] float spreadAngle      = 0f;   // makineli için >0

    // ───── Devriye ─────
    [Header("Devriye")]
    [SerializeField] Transform[] patrolPoints;
    [SerializeField] float patrolWaitTime = 2f;  // noktada bekleme süresi (saniye)

    // ───── Tip ─────
    [Header("Tip")]
    [SerializeField] bool isLarge = false;

    // ───── Sıçrama (Tip 2 — Sıçrayıcı, opsiyonel) ─────
    [Header("Sıçrama (Sıçrayıcı)")]
    [SerializeField] bool  isJumper      = false;
    [SerializeField] float leapRangeMin  = 4f;
    [SerializeField] float leapRangeMax  = 10f;
    [SerializeField] float leapCooldown  = 3f;
    [SerializeField] float leapSpeed     = 14f;
    [SerializeField] float leapArcHeight = 6f;

    [Header("Drop")]
    [SerializeField] GameObject ammoPickupPrefab;

    [Header("Ses")]
    [SerializeField] AudioClip deathClip;
    [SerializeField] [Range(0f,1f)] float deathVolume = 1f;
    [SerializeField] AudioClip hurtClip;
    [SerializeField] [Range(0f,1f)] float hurtVolume = 0.7f;
    [SerializeField] AudioClip attackClip;
    [SerializeField] [Range(0f,1f)] float attackVolume = 0.8f;

    // ───── Stun ─────
    float stunUntil;

    bool beingPulled;
    bool knockedBack;
    bool physicsFalling;   // gerçek Rigidbody fiziğiyle düşüyor (fırlatma/kanca bırakışı)

    // ───── Efektler ─────
    [Header("Efektler")]
    [SerializeField] ParticleSystem stunEffect;
    [SerializeField] GameObject attackIndicator;
    [SerializeField] float telegraphDuration = 0.7f;

    // ───── Performans ─────
    float pathTimer;
    const float pathInterval = 0.2f;

    // ───── Referanslar ─────
    NavMeshAgent agent;
    Rigidbody rb;
    Transform player;
    PlayerMovement playerMovement;
    SfxPlayer sfx;

    EnemyPatrolBehavior   patrol;
    EnemyMeleeAttack      meleeAttack;
    EnemyRangedAttack     rangedAttack;
    EnemyKnockbackHandler knockbackHandler;
    EnemyLeapBehavior     leap;
    bool leaping;

    // ─────────────────────────────────────────────

    void Awake()
    {
        agent  = GetComponent<NavMeshAgent>();
        rb     = GetComponent<Rigidbody>();
        rb.isKinematic = true;   // normalde NavMeshAgent sürer; sadece fırlatma/düşüş sırasında fizik açılır
        rb.useGravity  = false;
        var pgo = GameObject.FindGameObjectWithTag("Player");
        player = pgo ? pgo.transform : null;
        playerMovement = pgo ? pgo.GetComponent<PlayerMovement>() : null;

        sfx = SfxPlayer.Create(gameObject, spatialBlend: 1f);

        patrol = new EnemyPatrolBehavior(patrolPoints, patrolWaitTime, sightRange, obstacleMask);
        meleeAttack = new EnemyMeleeAttack(attackRange, attackDamage, attackCooldown, telegraphDuration,
            playerKnockback, attackIndicator, sfx, attackClip, attackVolume);
        rangedAttack = new EnemyRangedAttack(rangedRange, magSize, fireRate, reloadTime,
            projectileSpeed, projectileDamage, spreadAngle, projectilePrefab, muzzle, sfx, attackClip, attackVolume);
        knockbackHandler = new EnemyKnockbackHandler();
        leap = new EnemyLeapBehavior(leapRangeMin, leapRangeMax, leapCooldown, leapSpeed, leapArcHeight);

        var health = GetComponent<Health>();
        health.onDeath.AddListener(OnDeath);
        health.onHealthChanged.AddListener(h => { if (h > 0f) PlayHurt(); });

        // Ölçek büyütülünce (isLarge) skinned mesh sınırları bozulup yanlış
        // frustum-culling ile görünmez olabiliyor — her kare bounds güncelle
        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            smr.updateWhenOffscreen = true;
    }

    void Start()
    {
        if (patrol.FirstPoint != null)
            agent.SetDestination(patrol.FirstPoint.position);
    }

    void Update()
    {
        if (player == null) return;

        if (beingPulled) return;
        if (knockedBack) return;
        if (physicsFalling) return;
        if (leaping) return;

        if (state == State.Stunned)
        {
            if (Time.time >= stunUntil)
            {
                state = State.Chase;
                StopStunEffect();
            }
            return;
        }

        float dist = Vector3.Distance(transform.position, player.position);

        switch (state)
        {
            case State.Patrol:       DoPatrol(dist);      break;
            case State.Chase:        DoChase(dist);       break;
            case State.Attack:       DoAttack(dist);      break;
            case State.Telegraphing: DoTelegraph(dist);   break;
            case State.RangedFire:   DoRangedFire(dist);  break;
        }
    }

    // ───────────────── Devriye ─────────────────

    void DoPatrol(float distToPlayer)
    {
        if (patrol.Tick(agent, transform, player, distToPlayer))
            state = State.Chase;
    }

    // ───────────────── Takip ─────────────────

    void DoChase(float dist)
    {
        if (!agent.isOnNavMesh) return;

        if (behavior == Behavior.Ranged)
        {
            DoRangedChase(dist);
            return;
        }

        if (isJumper && leap.ReadyToLeap(dist))
        {
            StartLeap();
            return;
        }

        pathTimer += Time.deltaTime;
        if (pathTimer >= pathInterval)
        {
            pathTimer = 0f;
            agent.SetDestination(player.position);
        }

        if (meleeAttack.InRange(dist))
        {
            state = State.Attack;
            meleeAttack.EnterAttack();
        }
        else if (dist > sightRange * 1.6f)
        {
            // Oyuncuyu kaybetti, devri yöne dön
            state = State.Patrol;
            agent.ResetPath();
        }
    }

    // ───────────────── Menzilli ─────────────────

    void DoRangedChase(float dist)
    {
        pathTimer += Time.deltaTime;
        if (pathTimer >= pathInterval)
        {
            pathTimer = 0f;
            var move = rangedAttack.GetChaseMove(transform, player, dist, out Vector3 dest);
            if (move == EnemyRangedAttack.ChaseMove.MoveTo) agent.SetDestination(dest);
            else if (move == EnemyRangedAttack.ChaseMove.Stop) agent.ResetPath();
        }

        if (dist <= rangedRange && rangedAttack.HasLineOfSight(transform, player))
        {
            state = State.RangedFire;
            rangedAttack.StartFiring();
        }
        else if (dist > sightRange * 1.8f)
        {
            state = State.Patrol;
            agent.ResetPath();
        }
    }

    void DoRangedFire(float dist)
    {
        if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();
        FacePlayer();

        if (dist > rangedRange * 1.3f || !rangedAttack.HasLineOfSight(transform, player))
        {
            state = State.Chase;
            return;
        }

        rangedAttack.TickFire(transform, player);
    }

    // ───────────────── Saldırı ─────────────────

    void DoAttack(float dist)
    {
        if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();
        FacePlayer();

        if (meleeAttack.OutOfRange(dist))
        {
            state = State.Chase;
            return;
        }

        if (meleeAttack.ReadyToTelegraph())
        {
            state = State.Telegraphing;
            meleeAttack.StartTelegraph();
        }
    }

    void DoTelegraph(float dist)
    {
        FacePlayer();

        if (meleeAttack.OutOfRange(dist))
        {
            meleeAttack.HideIndicator();
            state = State.Chase;
            return;
        }

        if (meleeAttack.TickTelegraph())
        {
            meleeAttack.HideIndicator();
            meleeAttack.ResolveTelegraph(transform, player, playerMovement);
            state = State.Attack;
        }
    }

    // ───────────────── Yardımcılar ─────────────────

    void FacePlayer()
    {
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 8f);
    }

    public bool IsLarge      => isLarge;
    public bool IsParryable  => state == State.Telegraphing;

    public void SetPatrolPoints(Transform[] points)
    {
        patrol.SetPoints(points);
        if (patrol.FirstPoint != null)
            agent.SetDestination(patrol.FirstPoint.position);
    }

    public void Parry(float stunDuration = 2f)
    {
        meleeAttack.HideIndicator();
        state = State.Stunned;
        Stun(stunDuration);
    }

    // Yumruk geri itmesi — zeminde kısa, duvar-farkında bir kayma (duvardan geçmez)
    public void Knockback(Vector3 dir, float force)
    {
        if (isLarge || beingPulled) return;
        if (!isActiveAndEnabled) return;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) return;
        meleeAttack.HideIndicator();
        StopStunEffect();          // yumruk stun'ı keser, efekt takılı kalmasın
        knockedBack = true;
        StartCoroutine(knockbackHandler.Run(transform, agent, rb, dir.normalized, force, () =>
        {
            state = State.Chase;
            knockedBack = false;
        }));
    }

    // Sıçrayıcı: chase sırasında orta menzilde oyuncuya doğru fiziksel sıçrayış
    void StartLeap()
    {
        leaping = true;
        meleeAttack.HideIndicator();
        StartCoroutine(leap.Run(transform, agent, rb, player.position, () =>
        {
            leaping = false;
            state = State.Chase;
        }));
    }

    void StopStunEffect()
    {
        if (stunEffect)
        {
            stunEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            stunEffect.gameObject.SetActive(false);
        }
    }

    public void StartBeingPulled()
    {
        beingPulled = true;
        agent.enabled = false;
    }

    public void StopBeingPulled(Vector3 momentum = default)
    {
        beingPulled = false;

        if (momentum.sqrMagnitude < 0.01f)
        {
            // Düşmanı en yakın NavMesh noktasına otur (duvar içi/dışı boşlukta kalmasın)
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                transform.position = hit.position;
            agent.enabled = true;
            state         = State.Chase;
        }
        else
        {
            StartCoroutine(PhysicsFall(momentum));   // gerçek fizikle düşüp doğal şekilde yerleşir
        }
    }

    // Fırlatma/kanca bırakışı sonrası gerçek Rigidbody fiziğiyle düşüş —
    // ani "ışınlanma" yerine PhysX doğal şekilde yere/duvara oturtur.
    IEnumerator PhysicsFall(Vector3 initialVelocity)
    {
        physicsFalling = true;
        agent.enabled  = false;
        rb.isKinematic = false;
        rb.useGravity  = true;
        rb.velocity    = initialVelocity;

        float t = 0f;
        while (t < 3f)
        {
            t += Time.deltaTime;
            bool grounded = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.3f,
                                             ~0, QueryTriggerInteraction.Ignore);
            if (grounded && rb.velocity.magnitude < 0.3f) break;
            yield return null;
        }

        rb.velocity    = Vector3.zero;
        rb.isKinematic = true;
        rb.useGravity  = false;

        if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out NavMeshHit end, 2f, NavMesh.AllAreas))
            transform.position = end.position;

        agent.enabled  = true;
        physicsFalling = false;
        state          = State.Chase;
    }

    // Flash/stun etkisi (launcher flash modu)
    public void Stun(float duration)
    {
        state     = State.Stunned;
        stunUntil = Time.time + duration;
        if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();
        if (stunEffect)
        {
            stunEffect.gameObject.SetActive(true);
            stunEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            stunEffect.Play(true);
        }
    }

    void PlayHurt()
    {
        sfx.Play(hurtClip, hurtVolume);
    }

    void OnDeath()
    {
        if (ammoPickupPrefab != null)
            Instantiate(ammoPickupPrefab, transform.position + Vector3.up * 0.3f, Quaternion.identity);

        DamageVignette.OnKill();
        CameraShake.HitPause();

        SfxPlayer.PlayDetached(deathClip, transform.position, deathVolume);

        foreach (var r in GetComponentsInChildren<Renderer>())
            r.enabled = false;

        agent.enabled = false;
        enabled = false;
        Destroy(gameObject, 0.05f);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
#endif
}
}

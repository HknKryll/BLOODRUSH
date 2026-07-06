using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Health))]
public class EnemyAI : MonoBehaviour
{
    // ───── Durum makinesi ─────
    enum State { Patrol, Chase, Attack, Telegraphing, Stunned }
    State state = State.Patrol;

    // ───── Algılama ─────
    [Header("Algılama")]
    [SerializeField] float sightRange = 20f;
    [SerializeField] LayerMask obstacleMask;          // duvar/engel katmanı

    // ───── Saldırı ─────
    [Header("Saldırı")]
    [SerializeField] float attackRange = 2f;
    [SerializeField] float attackDamage = 15f;
    [SerializeField] float attackCooldown = 1.2f;
    float lastAttackTime;

    // ───── Devriye ─────
    [Header("Devriye")]
    [SerializeField] Transform[] patrolPoints;
    [SerializeField] float patrolWaitTime = 2f;  // noktada bekleme süresi (saniye)
    int patrolIndex;
    float waitUntil = -1f;

    // ───── Tip ─────
    [Header("Tip")]
    [SerializeField] bool isLarge = false;

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
    Vector3 launchVelocity;

    // ───── Efektler ─────
    [Header("Efektler")]
    [SerializeField] ParticleSystem stunEffect;
    [SerializeField] GameObject attackIndicator;
    [SerializeField] float telegraphDuration = 0.7f;
    float telegraphStartTime;
    float activeTelegraphDuration;

    // ───── Performans ─────
    float pathTimer;
    float sightTimer;
    bool  lastSightResult;
    const float pathInterval  = 0.2f;
    const float sightInterval = 0.15f;

    // ───── Referanslar ─────
    NavMeshAgent agent;
    Transform player;
    AudioSource audioSrc;

    // ─────────────────────────────────────────────

    void Awake()
    {
        agent  = GetComponent<NavMeshAgent>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.playOnAwake  = false;
        audioSrc.spatialBlend = 1f;

        var health = GetComponent<Health>();
        health.onDeath.AddListener(OnDeath);
        health.onHealthChanged.AddListener(h => { if (h > 0f) PlayHurt(); });
    }

    void Start()
    {
        if (patrolPoints.Length > 0 && patrolPoints[0] != null)
            agent.SetDestination(patrolPoints[0].position);
    }

    void Update()
    {
        if (player == null) return;

        if (beingPulled) return;

        if (launchVelocity != Vector3.zero)
        {
            launchVelocity.y += Physics.gravity.y * Time.deltaTime;
            transform.position += launchVelocity * Time.deltaTime;

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 0.4f, NavMesh.AllAreas)
                && launchVelocity.y <= 0f)
            {
                transform.position = hit.position;
                launchVelocity     = Vector3.zero;
                agent.enabled      = true;
                state              = State.Chase;
            }
            return;
        }

        if (state == State.Stunned)
        {
            if (Time.time >= stunUntil)
            {
                state = State.Chase;
                if (stunEffect) { stunEffect.Stop(); stunEffect.gameObject.SetActive(false); }
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
        }
    }

    // ───────────────── Devriye ─────────────────

    void DoPatrol(float distToPlayer)
    {
        if (CanSeePlayer(distToPlayer))
        {
            state = State.Chase;
            return;
        }

        if (patrolPoints.Length == 0) return;
        if (!agent.isOnNavMesh) return;
        if (patrolPoints[patrolIndex] == null) return;

        // Hedefe ulaştı mı?
        if (!agent.pathPending && agent.remainingDistance < 0.4f)
        {
            if (waitUntil < 0f)
                waitUntil = Time.time + patrolWaitTime;

            if (Time.time < waitUntil) return;

            waitUntil   = -1f;
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
            if (patrolPoints[patrolIndex] != null)
                agent.SetDestination(patrolPoints[patrolIndex].position);
        }
    }

    // ───────────────── Takip ─────────────────

    void DoChase(float dist)
    {
        if (!agent.isOnNavMesh) return;

        pathTimer += Time.deltaTime;
        if (pathTimer >= pathInterval)
        {
            pathTimer = 0f;
            agent.SetDestination(player.position);
        }

        if (dist <= attackRange)
        {
            state          = State.Attack;
            lastAttackTime = Time.time;
        }
        else if (dist > sightRange * 1.6f)
        {
            // Oyuncuyu kaybetti, devri yöne dön
            state = State.Patrol;
            agent.ResetPath();
        }
    }

    // ───────────────── Saldırı ─────────────────

    void DoAttack(float dist)
    {
        agent.ResetPath();
        FacePlayer();

        if (dist > attackRange * 1.3f)
        {
            state = State.Chase;
            return;
        }

        if (Time.time >= lastAttackTime + attackCooldown)
        {
            state                   = State.Telegraphing;
            telegraphStartTime      = Time.time;
            activeTelegraphDuration = telegraphDuration;
            if (attackIndicator)
            {
                attackIndicator.SetActive(true);
                foreach (var ps in attackIndicator.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.Play(true);
                    activeTelegraphDuration = ps.main.duration; // particle'ın kendi süresiyle eşleş
                }
            }
        }
    }

    void DoTelegraph(float dist)
    {
        FacePlayer();

        if (dist > attackRange * 1.3f)
        {
            HideIndicator();
            state = State.Chase;
            return;
        }

        if (Time.time >= telegraphStartTime + activeTelegraphDuration)
        {
            HideIndicator();
            lastAttackTime = Time.time;
            player.GetComponent<Health>()?.TakeDamage(attackDamage);
            if (attackClip != null) audioSrc.PlayOneShot(attackClip, attackVolume);
            state = State.Attack;
        }
    }

    // ───────────────── Yardımcılar ─────────────────

    bool CanSeePlayer(float dist)
    {
        sightTimer += Time.deltaTime;
        if (sightTimer < sightInterval) return lastSightResult;
        sightTimer = 0f;

        if (dist > sightRange) { lastSightResult = false; return false; }

        Vector3 origin = transform.position + Vector3.up;
        Vector3 dir    = (player.position - origin).normalized;

        lastSightResult = !Physics.Raycast(origin, dir, dist, obstacleMask);
        return lastSightResult;
    }

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
        patrolPoints = points;
        patrolIndex  = 0;
        if (patrolPoints.Length > 0)
            agent.SetDestination(patrolPoints[0].position);
    }

    public void Parry(float stunDuration = 2f)
    {
        HideIndicator();
        state = State.Stunned;
        Stun(stunDuration);
    }

    void HideIndicator()
    {
        if (!attackIndicator) return;
        foreach (var ps in attackIndicator.GetComponentsInChildren<ParticleSystem>(true))
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        attackIndicator.SetActive(false);
    }

    public void StartBeingPulled()
    {
        beingPulled = true;
        agent.enabled = false;
    }

    public void StopBeingPulled(Vector3 momentum = default)
    {
        beingPulled    = false;
        launchVelocity = momentum;
        if (momentum.sqrMagnitude < 0.01f)
        {
            agent.enabled = true;
            state         = State.Chase;
        }
        // momentum varsa agent kapalı kalır, launch fazı bitince Update açar
    }

    // Flash/stun etkisi (launcher flash modu)
    public void Stun(float duration)
    {
        state     = State.Stunned;
        stunUntil = Time.time + duration;
        agent.ResetPath();
        if (stunEffect)
        {
            stunEffect.gameObject.SetActive(true);
            stunEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            stunEffect.Play(true);
        }
    }

    void PlayHurt()
    {
        if (hurtClip) audioSrc.PlayOneShot(hurtClip, hurtVolume);
    }

    void OnDeath()
    {
        if (ammoPickupPrefab != null)
            Instantiate(ammoPickupPrefab, transform.position + Vector3.up * 0.3f, Quaternion.identity);

        if (deathClip != null)
        {
            var sfxGO = new GameObject("EnemyDeathSFX");
            sfxGO.transform.position = transform.position;
            var src = sfxGO.AddComponent<AudioSource>();
            src.spatialBlend = 0f; // 2D — mesafeden bağımsız
            src.volume = deathVolume;
            src.clip = deathClip;
            src.Play();
            Destroy(sfxGO, 5f);
        }

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

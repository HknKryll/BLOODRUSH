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
    float lastAttackTime;

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
    int   magLeft;
    float nextShotTime;

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
    bool knockedBack;
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
    PlayerMovement playerMovement;
    SfxPlayer sfx;

    // ─────────────────────────────────────────────

    void Awake()
    {
        agent  = GetComponent<NavMeshAgent>();
        var pgo = GameObject.FindGameObjectWithTag("Player");
        player = pgo ? pgo.transform : null;
        playerMovement = pgo ? pgo.GetComponent<PlayerMovement>() : null;

        sfx = SfxPlayer.Create(gameObject, spatialBlend: 1f);

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
        if (patrolPoints.Length > 0 && patrolPoints[0] != null)
            agent.SetDestination(patrolPoints[0].position);
    }

    void Update()
    {
        if (player == null) return;

        if (beingPulled) return;
        if (knockedBack) return;

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

        if (behavior == Behavior.Ranged)
        {
            DoRangedChase(dist);
            return;
        }

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

    // ───────────────── Menzilli ─────────────────

    void DoRangedChase(float dist)
    {
        pathTimer += Time.deltaTime;
        if (pathTimer >= pathInterval)
        {
            pathTimer = 0f;
            if (dist > rangedRange)                       // uzak → yaklaş
                agent.SetDestination(player.position);
            else if (dist < rangedRange * 0.5f)           // çok yakın → geri çekil
            {
                Vector3 away = transform.position + (transform.position - player.position).normalized * 4f;
                if (NavMesh.SamplePosition(away, out NavMeshHit h, 4f, NavMesh.AllAreas))
                    agent.SetDestination(h.position);
            }
            else agent.ResetPath();                       // menzilde → dur
        }

        if (dist <= rangedRange && HasRangedLoS())
        {
            state        = State.RangedFire;
            magLeft      = magSize;
            nextShotTime = 0f;
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

        if (dist > rangedRange * 1.3f || !HasRangedLoS())
        {
            state = State.Chase;
            return;
        }

        if (Time.time < nextShotTime) return;

        if (magLeft <= 0) magLeft = magSize;   // reload bitti, şarjör dolu

        FireProjectile();
        magLeft--;
        nextShotTime = Time.time + (magLeft <= 0 ? reloadTime : fireRate);
    }

    void FireProjectile()
    {
        if (projectilePrefab == null || player == null) return;

        Vector3 origin = muzzle ? muzzle.position : transform.position + Vector3.up * 1.4f;
        Vector3 dir    = (player.position + Vector3.up * 0.5f - origin).normalized;

        if (spreadAngle > 0f)   // makineli yayılımı
            dir = Quaternion.Euler(Random.Range(-spreadAngle, spreadAngle),
                                   Random.Range(-spreadAngle, spreadAngle), 0f) * dir;

        var proj = Instantiate(projectilePrefab, origin, Quaternion.LookRotation(dir));
        proj.Launch(dir, projectileSpeed, projectileDamage);
        sfx.Play(attackClip, attackVolume);
    }

    bool HasRangedLoS()
    {
        if (player == null) return false;
        Vector3 origin = muzzle ? muzzle.position : transform.position + Vector3.up * 1.4f;
        Vector3 target = player.position + Vector3.up * 0.5f;
        Vector3 dir    = target - origin;

        foreach (var h in Physics.RaycastAll(origin, dir.normalized, dir.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform)) continue;             // kendi gövden
            if (h.collider.GetComponentInParent<PlayerMovement>() != null) continue; // oyuncu engel değil
            if (h.collider.GetComponentInParent<EnemyAI>() != null) continue;    // diğer düşmanlar engel değil
            return false;  // duvar
        }
        return true;
    }

    // ───────────────── Saldırı ─────────────────

    void DoAttack(float dist)
    {
        if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();
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

            // Büyük düşman: vurunca oyuncuyu geri it
            if (playerKnockback > 0f && playerMovement != null)
            {
                Vector3 away = player.position - transform.position; away.y = 0f;
                playerMovement.Launch(away.normalized * playerKnockback + Vector3.up * 2f);
            }

            sfx.Play(attackClip, attackVolume);
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

    // Yumruk geri itmesi — zeminde kısa, duvar-farkında bir kayma (duvardan geçmez)
    public void Knockback(Vector3 dir, float force)
    {
        if (isLarge || beingPulled) return;
        if (!isActiveAndEnabled) return;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) return;
        HideIndicator();
        StopStunEffect();          // yumruk stun'ı keser, efekt takılı kalmasın
        StartCoroutine(KnockbackRoutine(dir.normalized, force));
    }

    void StopStunEffect()
    {
        if (stunEffect)
        {
            stunEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            stunEffect.gameObject.SetActive(false);
        }
    }

    IEnumerator KnockbackRoutine(Vector3 dir, float force)
    {
        knockedBack = true;
        if (agent.enabled) agent.enabled = false;

        // Yatay fırlatma mesafesi — eğlenceli, force 7 → ~2.5m
        float distance = force * 0.35f;

        // Duvar kontrolü — yolda engel varsa mesafeyi kırp (kendi yarıçapının ötesinden başla)
        Vector3 origin = transform.position + Vector3.up * 0.6f + dir * 0.5f;
        if (Physics.Raycast(origin, dir, out RaycastHit wall, distance, ~0, QueryTriggerInteraction.Ignore))
            distance = Mathf.Max(0f, wall.distance);

        Vector3 start = transform.position;
        Vector3 land  = start + dir * distance;

        // İniş noktasını NavMesh'e kelepçele (duvar arkası yürünmez alana taşmasın)
        if (NavMesh.SamplePosition(land, out NavMeshHit nav, 1.5f, NavMesh.AllAreas))
            land = nav.position;

        // Yay: yatay start→land + dikey parabol (0 → tepe → 0)
        float height = Mathf.Clamp(force * 0.12f, 0.4f, 1.5f);
        const float dur = 0.35f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            Vector3 pos = Vector3.Lerp(start, land, p);
            pos.y += height * 4f * p * (1f - p);   // parabolik yükseklik
            transform.position = pos;
            yield return null;
        }

        // Zemine/NavMesh'e otur ve agent'ı geri aç
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit end, 1.5f, NavMesh.AllAreas))
            transform.position = end.position;
        agent.enabled = true;
        state = State.Chase;
        knockedBack = false;
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
            launchVelocity = Vector3.zero;
            agent.enabled  = true;
            state          = State.Chase;
        }
        else
        {
            launchVelocity = momentum;   // agent kapalı kalır, launch fazı bitince Update açar
        }
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

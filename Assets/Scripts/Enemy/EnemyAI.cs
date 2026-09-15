using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Bloodrush.Shared;
using Bloodrush.Shared.Audio;
using Bloodrush.FX;
using Bloodrush.Player;

namespace Bloodrush.Enemy
{
[RequireComponent(typeof(Rigidbody))]
public class EnemyAI : EnemyAIBase
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
    [Tooltip("Agent NavMesh disina dusarse bu yaricap icinde en yakin gecerli noktaya " +
             "geri yapistirilir. KUCUK TUT: buyuk deger ustteki/alttaki KATIN NavMesh'ini " +
             "de kapsar ve dusmani oraya isinlar. 1.5 m bake bosluklarini kapatmaya yeter.")]
    [SerializeField] float navRecoverRadius = 1.5f;

    [Tooltip("Kurtarma sirasinda izin verilen en fazla KOT FARKI (m). Bulunan gecerli nokta " +
             "bundan fazla yukarida/asagidaysa warp YAPILMAZ — dusman baska bir kata " +
             "isinlanmaz. Arenada kattan kata mesafe 4 m.")]
    [SerializeField] float navRecoverMaxRise = 1f;

    [Header("Algılama")]
    [SerializeField] float sightRange = 20f;
    [SerializeField] LayerMask obstacleMask;          // duvar/engel katmanı

    // ───── Saldırı (melee) ─────
    [Header("Saldırı")]
    [SerializeField] float attackRange = 2f;
    [Tooltip("Yumruğun DİKEY erişimi (m). Oyuncu bundan daha yukarıda/aşağıdaysa menzil " +
             "dışı sayılır. Arenada kattan kata mesafe 4 m; 2 = alçak basamaktan " +
             "vurabilir ama platformdan vuramaz, zıplamak da kaçınma aracı olur.")]
    [SerializeField] float meleeVerticalReach = 2f;
    [SerializeField] float attackDamage = 15f;
    [SerializeField] float attackCooldown = 1.2f;

    // ───── Menzilli (behavior = Ranged) ─────
    [Header("Menzilli")]
    [SerializeField] EnemyProjectile projectilePrefab;
    [SerializeField] Transform muzzle;
    [Tooltip("ATEŞ menzili — melee'nin attackRange'inden AYRI bir alandır, ona dokunmaz. " +
             "Düşman bu mesafeye girince durup ateş eder; yarısından yakına gelinirse " +
             "mesafe açar (bkz. EnemyRangedAttack.GetChaseMove).")]
    [SerializeField] float rangedRange      = 26f;
    [Tooltip("Menzilli düşmanın NavMeshAgent durma mesafesi. 0 = otomatik (rangedRange × 0.8). " +
             "SADECE behavior = Ranged için uygulanır; melee düşmanın durma mesafesi 0'da kalır.")]
    [SerializeField] float rangedStopDistance = 0f;
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
    [SerializeField] GameObject healthPickupPrefab;
    [SerializeField] [Range(0f,1f)] float healthDropChance = 0.25f;

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
    Rigidbody rb;
    Animator anim;   // opsiyonel — animasyonlu gövdesi olan düşmanlarda (yoksa null, atlanır)
    bool hasStunnedAnimParam;   // Animator'da "Stunned" bool'u var mı (varsa stun klibi oynar, yoksa dondurulur)

    EnemyPatrolBehavior   patrol;
    EnemyMeleeAttack      meleeAttack;
    EnemyRangedAttack     rangedAttack;
    EnemyKnockbackHandler knockbackHandler;
    EnemyLeapBehavior     leap;
    bool leaping;

    // ─────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();

        anim = GetComponentInChildren<Animator>();   // animasyonlu gövde varsa

        // Animator'da "Stunned" bool'u tanımlı mı? Varsa stun'da o klibe geçilir;
        // yoksa fallback: animator DONDURULUR (yumruk ortada kalır — stun okunur).
        if (anim)
            foreach (var p in anim.parameters)
                if (p.type == AnimatorControllerParameterType.Bool && p.name == "Stunned")
                { hasStunnedAnimParam = true; break; }
        rb     = GetComponent<Rigidbody>();
        rb.isKinematic = true;   // normalde NavMeshAgent sürer; sadece fırlatma/düşüş sırasında fizik açılır
        rb.useGravity  = false;

        patrol = new EnemyPatrolBehavior(patrolPoints, patrolWaitTime, sightRange, obstacleMask);
        meleeAttack = new EnemyMeleeAttack(attackRange, meleeVerticalReach, attackDamage, attackCooldown,
            telegraphDuration, playerKnockback, attackIndicator, sfx, attackClip, attackVolume);
        rangedAttack = new EnemyRangedAttack(rangedRange, magSize, fireRate, reloadTime,
            projectileSpeed, projectileDamage, spreadAngle, projectilePrefab, muzzle, sfx, attackClip, attackVolume);
        knockbackHandler = new EnemyKnockbackHandler();
        leap = new EnemyLeapBehavior(leapRangeMin, leapRangeMax, leapCooldown, leapSpeed, leapArcHeight);

        // Menzilli düşman hedefe yürümeyi menzil kenarında bıraksın — yoksa
        // GetChaseMove'un "Stop" kararı gelene kadar (pathInterval 0.2 sn) üstüne
        // yürümeye devam ediyor. Melee'ye DOKUNULMUYOR: stoppingDistance 0'da kalır.
        if (behavior == Behavior.Ranged)
            agent.stoppingDistance = rangedStopDistance > 0f
                                   ? rangedStopDistance
                                   : rangedRange * 0.8f;

        health.onHealthChanged.AddListener(h => { if (h > 0f) PlayHurt(); });
    }

    void Start()
    {
        if (patrol.FirstPoint != null)
            agent.SetDestination(patrol.FirstPoint.position);
    }

    float navRecoverAt;   // off-mesh kurtarmayi her karede denemeyelim
    int   navRecoverCount;
    Vector3 lastAnimPos;
    float   animSpeed;

    // ANIMASYON HIZI AGENT'TAN DEGIL GERCEK HAREKETTEN OLCULUR.
    //
    // Eskiden `anim.SetFloat("Speed", agent.velocity.magnitude)` idi. Agent bir an
    // NavMesh disina dustugunde velocity 0 oluyor, KucukDusmanController'in 0.1 esigi
    // animasyonu aninda Idle'a snap ediyordu — "platform altinda animasyon bozuluyor"
    // sikayetinin ta kendisi. Transform yer degistirmesinden olcmek bunu KOKUNDEN cozer:
    // agent'in ic durumu ne olursa olsun, dusman hareket ediyorsa kosma animasyonu oynar.
    //
    // Bu ayrim ayni zamanda kurtarma warp'ini animasyon icin GEREKSIZ kilar; boylece warp
    // asagidaki gibi sikica sinirlanabiliyor (bkz. TryRecoverNavMesh).
    void UpdateAnimSpeed()
    {
        if (anim == null) return;

        float dt = Mathf.Max(Time.deltaTime, 1e-4f);
        float measured = (transform.position - lastAnimPos).magnitude / dt;
        lastAnimPos = transform.position;

        // Isinlanma/warp gibi ani siramalar animasyonu patlatmasin.
        if (measured > 50f) measured = animSpeed;

        animSpeed = Mathf.Lerp(animSpeed, measured, dt * 10f);
        anim.SetFloat("Speed", animSpeed);
    }

    // Bake kusurlu ya da bayat olsa bile dusman kendi kendine toparlansin diye sigorta.
    //
    // DIKKAT — DIKEY SINIR: eskiden yaricap 5 m ve dikey sinir yoktu. CH3 arenasinda
    // zemin y 0.00, platform decki y 4.00; yani 5 m'lik ornekleme kuresi UST KATI da
    // kapsiyordu. Dusman platformun altinda bir an off-mesh olunca en yakin gecerli
    // poligon ustteki deck cikiyor ve Warp onu 4 m yukari isinliyordu — "altina girmiyor,
    // ustune cikiyor" regresyonunun sebebi buydu. Artik kat degistiren bir kurtarma
    // YAPILMIYOR: boyle bir durumda hic warp etmeyip agent'in kendi kendine oturmasini
    // bekliyoruz (animasyon zaten artik bundan etkilenmiyor).
    void TryRecoverNavMesh()
    {
        if (Time.time < navRecoverAt) return;
        navRecoverAt = Time.time + 0.5f;

        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit,
                                    navRecoverRadius, agent.areaMask))
            return;

        float rise = Mathf.Abs(hit.position.y - transform.position.y);
        if (rise > navRecoverMaxRise)
        {
            Debug.Log($"[EnemyAI] '{name}' off-mesh, ama en yakin gecerli nokta {rise:0.00} m " +
                      $"kot farkinda (sinir {navRecoverMaxRise:0.00}) — BASKA KATA isinlanmamak " +
                      "icin warp yapilmadi.", this);
            return;
        }

        agent.Warp(hit.position);
        navRecoverCount++;
        Debug.Log($"[EnemyAI] '{name}' NavMesh disinda kaldi, {hit.distance:0.00} m otedeki " +
                  $"gecerli noktaya alindi (kot farki {rise:0.00} m).", this);
    }

    // HEDEF OYUNCUNUN AYAGINDAN ORNEKLENIR, PIVOTUNDAN DEGIL.
    //
    // player.position oyuncunun pivotu ve o pivot AYAKLARDAN 1.17 m YUKARIDA
    // (CharacterController center.y = -0.17, height = 2). SetDestination hedefi kendi
    // icinde en yakin NavMesh'e esliyor; baslangic noktasi 1.17 m yukarida olunca esleme
    // ustteki platform deckine dogru egiliyor. Ayak hizasindan KUCUK bir yaricapla
    // orneklemek hedefi platformun ALTINDAKI zemine sabitler.
    Vector3 ChaseTarget()
    {
        Vector3 feet = EnemyVision.PlayerFeet(player);
        if (NavMesh.SamplePosition(feet, out NavMeshHit hit, 2f, agent.areaMask))
            return hit.position;
        return player.position;   // yakinda gecerli nokta yoksa eski davranis
    }

    // ───── Debug (EnemyDebugOverlay okur — davranisi etkilemez) ─────
    public string DebugState     => state.ToString();
    public bool   OnNavMesh      => agent != null && agent.enabled && agent.isOnNavMesh;
    public int    NavRecoverCount => navRecoverCount;
    public Vector3 Destination   => agent != null && agent.enabled && agent.hasPath
                                    ? agent.destination : transform.position;
    public NavMeshPathStatus PathStatus => agent != null && agent.enabled && agent.hasPath
                                    ? agent.path.status : NavMeshPathStatus.PathInvalid;
    public float AgentSpeed      => agent != null && agent.enabled ? agent.velocity.magnitude : 0f;
    public float AnimSpeed       => animSpeed;

    void Update()
    {
        if (player == null) return;

        if (KeyBindings.DownKey(KeyCode.F1)) {
        NavMeshPath p = new NavMeshPath();
        agent.CalculatePath(player.position, p);
        Debug.Log($"{name} -> {p.status} | corners: {p.corners.Length}");
    }

        UpdateAnimSpeed();

        if (beingPulled) return;
        if (knockedBack) return;
        if (physicsFalling) return;
        if (leaping) return;

        // Agent NavMesh'ten dustuyse KENDINI TOPARLA.
        // Onceden DoChase basinda sessizce donuluyordu; dusman sonsuza kadar donup kaliyor,
        // animasyon da agent.velocity'ye bagli oldugu icin Idle'a snap ediyordu ("platform
        // altinda bozuluyor" sikayetinin kaynagi). WaveDirector'daki kanitlanmis desenle
        // en yakin gecerli noktaya yapistiriyoruz.
        if (agent.enabled && !agent.isOnNavMesh) { TryRecoverNavMesh(); return; }

        if (state == State.Stunned)
        {
            if (Time.time >= stunUntil)
            {
                state = State.Chase;
                StopStunEffect();
                SetStunAnim(false);   // animasyonu serbest bırak (koşuya dönsün)
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
            agent.SetDestination(ChaseTarget());
        }

        // Attack'e SADECE görüş hattı açıkken girilir. Kapalıyken girilseydi DoAttack'in
        // agent.ResetPath()'i düşmanı bulunduğu yere çakardı — platformun üstüne çıkıp
        // aşağı inmeyi bırakmasının sebebi tam olarak buydu. Artık engel varken
        // kovalamaya devam eder.
        if (meleeAttack.InRange(transform, player) && HasMeleeSight())
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

        // Menzilden çıkmak kadar GÖRÜŞÜ KAYBETMEK de Chase'e döndürür — yoksa oyuncu
        // siperin arkasına geçtiğinde düşman yerinde durup boşluğa yumruk atardı.
        if (meleeAttack.OutOfRange(transform, player) || !HasMeleeSight())
        {
            state = State.Chase;
            return;
        }

        if (meleeAttack.ReadyToTelegraph())
        {
            state = State.Telegraphing;
            meleeAttack.StartTelegraph();
            if (anim) anim.SetTrigger("Attack");   // yumruk animasyonu (wind-up + vuruş)
        }
    }

    void DoTelegraph(float dist)
    {
        FacePlayer();

        if (meleeAttack.OutOfRange(transform, player))
        {
            meleeAttack.HideIndicator();
            state = State.Chase;
            return;
        }

        if (meleeAttack.TickTelegraph())
        {
            meleeAttack.HideIndicator();
            // Görüş kontrolü ResolveTelegraph'ın İÇİNDE — telegraph ortasında siper
            // alan oyuncu hasar almasın diye son ana kadar bekleniyor.
            meleeAttack.ResolveTelegraph(transform, player, playerMovement);
            state = State.Attack;
        }
    }

    // Göğüsten göğüse tek ışın + yakın alan muafiyeti (bkz. EnemyVision.ClearForMelee).
    // Menzilli tarafın üç noktalı kontrolü burada gereksiz: yumruk mesafesi zaten kısa,
    // asıl mesele araya zemin/duvar girip girmediği.
    bool HasMeleeSight() => EnemyVision.ClearForMelee(transform, player);

    // ───────────────── Yardımcılar ─────────────────

    public override bool IsLarge      => isLarge;
    public override bool IsParryable  => state == State.Telegraphing;

    // Kancayla cekiliyor mu. Salt-okunur; davranisi degistirmez. ZoneAvoidance gibi
    // disaridan yon veren bilesenler, oyuncu dusmani cekerken ARAYA GIRMESIN diye bunu
    // yokluyor (bkz. Flow/Overload/ZoneAvoidance.cs).
    public bool IsBeingPulled => beingPulled;

    // WaveDirector gibi "takviye" spawn'ları için: devriye/görüş beklemeden
    // doğrudan oyuncuyu avlamaya başlar — zaten nerede olduğunu biliyorlar.
    public void AlertNow()
    {
        if (state == State.Patrol) state = State.Chase;
    }

    public void SetPatrolPoints(Transform[] points)
    {
        patrol.SetPoints(points);
        if (patrol.FirstPoint != null)
            agent.SetDestination(patrol.FirstPoint.position);
    }

    public override void Parry(float stunDuration = 2f)
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
        SetStunAnim(false);        // donmuş animator'ı serbest bırak
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

    // Stun'da saldırı animasyonunu KES: bekleyen Attack trigger'ı iptal + ya "Stunned"
    // klibine geç (parametre varsa) ya da animator'ı dondur (yumruk ortada asılı kalır —
    // sersemletme okunur). Stun bitince/kesilince geri sarılır: donmuş saldırı devam
    // etmesin diye mevcut state sonuna atlanır → çıkış geçişiyle koşu/idle'a döner.
    void SetStunAnim(bool on)
    {
        if (anim == null) return;

        anim.ResetTrigger("Attack");                    // bekleyen yumruğu iptal et

        if (hasStunnedAnimParam)
        {
            anim.SetBool("Stunned", on);
            return;
        }

        if (on)
        {
            anim.speed = 0f;                            // fallback: kare dondur
        }
        else
        {
            anim.speed = 1f;
            var st = anim.GetCurrentAnimatorStateInfo(0);
            anim.Play(st.shortNameHash, 0, 0.999f);     // saldırıyı bitmiş say → çıkış geçişi
        }
    }

    public void StartBeingPulled()
    {
        beingPulled = true;
        SetStunAnim(false);        // stun'da donmuşsa çekilirken serbest kalsın
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
        meleeAttack.HideIndicator();       // kafadaki telegraph ışığı sönsün
        SetStunAnim(true);                 // saldırı animasyonunu kes/dondur
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

    protected override void OnDeath()
    {
        if (ammoPickupPrefab != null)
            Instantiate(ammoPickupPrefab, transform.position + Vector3.up * 0.3f, Quaternion.identity);

        if (healthPickupPrefab != null && Random.value < healthDropChance)
            Instantiate(healthPickupPrefab, transform.position + Vector3.up * 0.3f, Quaternion.identity);

        DamageVignette.OnKill();
        CameraShake.HitPause();

        SfxPlayer.PlayDetached(deathClip, transform.position, deathVolume);

        SetRenderersVisible(false);

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

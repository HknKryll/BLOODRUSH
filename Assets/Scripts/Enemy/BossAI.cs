using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

// Ch2 Boss: pompalı (hitscan koni), cana bağlı 3 faz, %66/%33'te ışık söndürüp
// arkaya ışınlanma, uzakta kemp yapılırsa agresif dash, yakında parry'lenebilir
// kabza vuruşu + oyuncuyu geri itme. Büyük düşman: kanca/yumruk işlemez.
using Bloodrush.Shared;
using Bloodrush.FX;
using Bloodrush.Flow;
using Bloodrush.Player;

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

    State state = State.Chase;
    int   phase = 1;              // 1: >66%, 2: 66-33%, 3: <33%
    float fireTimer;
    float farTimer;               // dash tetiği için "uzakta durma" süresi
    float aimTimer;
    float meleeTimer;
    float meleeReadyTime;
    float stunUntil;
    float logTimer;
    bool  dead;

    NavMeshAgent   agent;
    Health         health;
    Transform      player;
    Health         playerHealth;
    PlayerMovement playerMovement;
    AudioSource    audioSrc;
    Renderer[]     renderers;
    Image          blackoutImg;
    readonly List<Light> litLights = new();

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

        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.playOnAwake  = false;
        audioSrc.spatialBlend = 1f;

        renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            smr.updateWhenOffscreen = true;   // büyük model yanlış culling ile kaybolmasın

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

        logTimer += Time.deltaTime;
        if (logTimer >= 1f)
        {
            logTimer = 0f;
            Debug.Log($"[Boss] state={state} dist={dist:F1} onNavMesh={agent.isOnNavMesh} " +
                      $"agentEnabled={agent.enabled} hasPath={(agent.enabled && agent.isOnNavMesh && agent.hasPath)} phase={phase}", this);
        }

        switch (state)
        {
            case State.Chase:          DoChase(dist);          break;
            case State.ShotgunAim:     DoShotgunAim();          break;
            case State.MeleeTelegraph: DoMeleeTelegraph(dist);  break;
            case State.Dash:           DoDash(dist);            break;
        }
    }

    // ───────── Faz ─────────

    void CheckPhaseTransition()
    {
        float frac = health.Max > 0f ? health.Current / health.Max : 1f;
        if (phase == 1 && frac <= 0.66f) { phase = 2; StartCoroutine(BlackoutRoutine()); }
        else if (phase == 2 && frac <= 0.33f) { phase = 3; StartCoroutine(BlackoutRoutine()); }
    }

    float FireInterval => fireInterval[Mathf.Clamp(phase - 1, 0, fireInterval.Length - 1)];
    float PhaseSpeedMult => 1f + (phase - 1) * 0.15f;

    // ───────── Chase ─────────

    void DoChase(float dist)
    {
        if (agent.enabled) agent.speed = moveSpeed * PhaseSpeedMult;

        // Yakınsa kabza
        if (dist <= meleeRange && Time.time >= meleeReadyTime)
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
        if (fireTimer >= FireInterval && HasLoS() && dist <= shotgunRange)
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
        state    = State.ShotgunAim;
        aimTimer = 0f;
        if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();
        ShowIndicator(aimIndicator);
    }

    void DoShotgunAim()
    {
        aimTimer += Time.deltaTime;
        if (aimTimer >= aimTime)
        {
            HideIndicator(aimIndicator);
            FireShotgun();
            fireTimer = 0f;
            state = State.Chase;
        }
    }

    void FireShotgun()
    {
        if (muzzleFlash) { muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); muzzleFlash.Play(); }
        if (fireClip) audioSrc.PlayOneShot(fireClip, fireVolume);
        CameraShake.Shake(0.08f, 0.1f);

        float dist = Vector3.Distance(transform.position, player.position);
        if (dist > shotgunRange) return;

        Vector3 flat = player.position - transform.position; flat.y = 0f;
        if (Vector3.Angle(transform.forward, flat) > coneAngle) return;  // koni dışı → ıska
        if (!HasLoS()) return;                                           // duvar arkası → ıska

        float dmg = Mathf.Lerp(damageNear, damageFar, Mathf.Clamp01(dist / shotgunRange));
        playerHealth?.TakeDamage(dmg);
    }

    bool HasLoS()
    {
        if (player == null) return false;
        Vector3 origin = muzzle ? muzzle.position : transform.position + Vector3.up * 1.5f;
        Vector3 target = player.position + Vector3.up * 0.5f;
        Vector3 dir    = target - origin;
        float   dist   = dir.magnitude;

        // Kendi gövdesini (Cube/Sphere) ve oyuncuyu yok say — sadece gerçek engel (duvar) LoS'u keser
        foreach (var h in Physics.RaycastAll(origin, dir.normalized, dist, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform)) continue;          // kendi gövden
            if (h.collider.GetComponentInParent<PlayerMovement>() != null) continue; // oyuncu engel değil
            return false;   // araya giren gerçek engel
        }
        return true;
    }

    // ───────── Kabza (melee) ─────────

    void StartMelee()
    {
        state      = State.MeleeTelegraph;
        meleeTimer = 0f;
        if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();
        ShowIndicator(meleeIndicator);
    }

    void DoMeleeTelegraph(float dist)
    {
        meleeTimer += Time.deltaTime;
        if (meleeTimer >= meleeTelegraph)
        {
            HideIndicator(meleeIndicator);
            // hâlâ menzildeyse vur
            if (dist <= meleeRange * 1.4f)
            {
                playerHealth?.TakeDamage(meleeDamage);
                if (playerMovement != null)
                {
                    Vector3 away = player.position - transform.position; away.y = 0f;
                    playerMovement.Launch(away.normalized * meleeKnockback + Vector3.up * 3f);
                }
                if (meleeClip) audioSrc.PlayOneShot(meleeClip, meleeVolume);
                CameraShake.Shake(0.3f, 0.2f);
            }
            meleeReadyTime = Time.time + meleeCooldown;
            state = State.Chase;
        }
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

    IEnumerator BlackoutRoutine()
    {
        state = State.Blackout;
        HideIndicator(aimIndicator);
        HideIndicator(meleeIndicator);
        if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();

        // Sahne ışıklarını söndür
        litLights.Clear();
        foreach (var l in FindObjectsOfType<Light>())
            if (l.enabled) { litLights.Add(l); l.enabled = false; }

        // Tam siyah overlay (garanti kör)
        blackoutImg = GameFlow.CreateOverlay(Color.black);
        blackoutImg.color = Color.black;

        // Boss görünmez
        SetRenderers(false);

        yield return null;

        // Oyuncunun arkasına ışınla
        if (player != null)
        {
            Vector3 behind = player.position - player.forward * teleportBehindDist;
            if (NavMesh.SamplePosition(behind, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            {
                agent.enabled = false;
                transform.position = hit.position;
                agent.enabled = true;
            }
            FacePlayer();
        }

        yield return new WaitForSecondsRealtime(blackoutDuration);

        // Işıkları geri aç, overlay kaldır, boss görünür
        RestoreLights();
        SetRenderers(true);
        if (blackoutImg) { Destroy(blackoutImg.canvas.gameObject); blackoutImg = null; }

        fireTimer = 0f;
        state = State.Chase;
    }

    void RestoreLights()
    {
        foreach (var l in litLights) if (l != null) l.enabled = true;
        litLights.Clear();
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
        RestoreLights();                                   // karanlıkta öldüyse ışıkları geri ver
        if (blackoutImg) Destroy(blackoutImg.canvas.gameObject);
        if (agent.enabled) agent.enabled = false;
        SetRenderers(false);
        enabled = false;
        Destroy(gameObject, 0.1f);
    }

    // Boss büyük — kanca/yumruk işlemez (kancanın kontrol edeceği bilgi)
    public bool IsLarge => true;
}
}

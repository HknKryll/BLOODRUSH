using UnityEngine;
using Bloodrush.Flow;
using Bloodrush.UI;
using Bloodrush.Enemy;
using Bloodrush.Shared.Audio;

namespace Bloodrush.Player
{
public class GrapplingHook : MonoBehaviour
{
    [Header("Kanca")]
    [SerializeField] float maxRange = 30f;
    [SerializeField] float pullSpeed = 28f;
    [SerializeField] float arrivalDistance = 0.6f;
    [SerializeField] float pickupArrivalDistance = 0.6f;
    [SerializeField] float launchMultiplier = 0.5f;
    [SerializeField] LayerMask hookMask = ~0;

    [Header("Denge")]
    [SerializeField] float cooldown        = 0.6f;   // her kullanımdan sonra bekleme
    [SerializeField] float hookTravelSpeed = 120f;   // mesafe/hız = uçuş süresi
    [SerializeField] float minTravelTime   = 0.1f;   // yakın hedefte bile minik gecikme

    [Header("Duvarda Asılı Kalma")]
    [SerializeField] float wallHangDuration = 1f;    // duvara varınca asılı kalma süresi
    [SerializeField] float wallHangJumpUp   = 12f;   // asılıyken Space: yukarı
    [SerializeField] float wallHangJumpOut  = 8f;    // asılıyken Space: duvardan dışa

    [Header("Görsel")]
    [SerializeField] LineRenderer rope;
    [SerializeField] Transform hookOrigin;   // namlu/kamera ucu

    [Header("Referanslar")]
    [SerializeField] Camera playerCamera;

    [Header("Ses")]
    [SerializeField] AudioClip hookFireClip;
    [SerializeField] [Range(0f,1f)] float hookFireVolume = 0.9f;
    [SerializeField] AudioClip hookReleaseClip;
    [SerializeField] [Range(0f,1f)] float hookReleaseVolume = 0.7f;

    SfxPlayer sfx;
    PlayerMovement movement;
    Vector3 hookPoint;
    bool isHooked;
    bool  firing;          // kanca ucu hedefe uçuyor (henüz çekmiyor)
    float fireTimer;
    float fireDuration;
    float cooldownUntil;
    bool    hanging;       // duvara varıp asılı bekliyor
    float   hangTimer;
    Vector3 hangWallNormal;
    bool hasLeftGround;
    bool releasedManually;  // true sadece oyuncu tuşu kasıtlı bıraktıysa
    EnemyAI hookedEnemy;
    bool pullingEnemy;
    AmmoPickup hookedPickup;
    bool pullingPickup;
    PlayerShoot shoot;

    // Dışarıdan kapatılabilir (ör. Flip Kulesi / GravityFlipZone) — hook varsa flip
    // tırmanışı anlamsızlaşır. Kapatılınca aktif kanca hemen iptal edilir.
    public bool HookEnabled { get; set; } = true;

    void Start()
    {
        movement = GetComponent<PlayerMovement>();
        shoot    = GetComponent<PlayerShoot>();
        if (playerCamera == null) playerCamera = Camera.main;
        if (rope != null) rope.gameObject.SetActive(false);
        sfx = SfxPlayer.Create(gameObject, spatialBlend: 0f);
    }

    void Update()
    {
        if (!HookEnabled)                       // kule/flip bölgesi — kanca devre dışı
        {
            if (firing || isHooked || hanging) CancelActive();
            return;
        }

        if (Input.GetKeyDown(KeyBindings.Grapple)) TryGrapple();
        if (Input.GetKeyUp(KeyBindings.Grapple))
        {
            // Uçuş sırasında bırakmak iptal ETMEZ — kanca varınca kısa çekişle fırlatır (tap desteği)
            if (firing) releasedDuringFlight = true;
            else if (!hanging) { releasedManually = true; ReleaseGrapple(); }
        }

        if (firing)        UpdateFiring();
        else if (hanging)  HandleHang();
        else if (isHooked) Pull();

        // Cooldown çubuğu (nişangâh altı)
        float norm = Time.time >= cooldownUntil ? 1f
                   : 1f - (cooldownUntil - Time.time) / cooldown;
        CrosshairHUD.Instance?.SetHookCooldown(norm);
    }

    // Kanca ucu hedefe uçarken ip uzar; uçuş bitince Attach()
    void UpdateFiring()
    {
        fireTimer += Time.deltaTime;
        float t = fireDuration > 0f ? Mathf.Clamp01(fireTimer / fireDuration) : 1f;

        if (rope != null)
        {
            Vector3 origin = hookOrigin != null ? hookOrigin.position : playerCamera.transform.position;
            rope.SetPosition(0, origin);
            rope.SetPosition(1, Vector3.Lerp(origin, hookPoint, t));
        }

        if (t >= 1f) Attach();
    }

    void TryGrapple()
    {
        // Cooldown / halihazırda meşgul
        if (Time.time < cooldownUntil || firing || isHooked) return;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        // Trigger hacimleri (DataTerminal, Arena, LevelExit...) kancaya hedef olmasın.
        // İnce ışın ıskalarsa kalın SphereCast ile ikinci şans (kat kenarı gibi ince hedefler için)
        releasedDuringFlight = false;

        if (!Physics.Raycast(ray, out RaycastHit hit, maxRange, hookMask, QueryTriggerInteraction.Ignore) &&
            !Physics.SphereCast(ray, 0.4f, out hit, maxRange, hookMask, QueryTriggerInteraction.Ignore))
        {
            cooldownUntil = Time.time + 0.2f;   // ıska: kısa ceza (tam cooldown değil)
            return;
        }

        // Hedefi belirle ama HENÜZ çekme — önce ip hedefe uçacak (firing)
        sfx.Play(hookFireClip, hookFireVolume);
        hookPoint        = hit.point;
        releasedManually = false;
        hookedEnemy  = hit.collider.GetComponent<EnemyAI>();
        hookedPickup = hit.collider.GetComponent<AmmoPickup>();

        float dist   = Vector3.Distance(playerCamera.transform.position, hookPoint);
        firing       = true;
        fireTimer    = 0f;
        fireDuration = Mathf.Max(minTravelTime, dist / hookTravelSpeed);

        if (rope != null)
        {
            rope.gameObject.SetActive(true);
            Vector3 origin = hookOrigin != null ? hookOrigin.position : playerCamera.transform.position;
            rope.SetPosition(0, origin);
            rope.SetPosition(1, origin);   // ip ucu başta namluda, uçacak
        }
    }

    float attachTime;
    bool  releasedDuringFlight;
    float stuckTimer;

    // Uçuş bitti — çekme fazını kur
    void Attach()
    {
        firing        = false;
        isHooked      = true;
        hasLeftGround = false;
        attachTime    = Time.time;
        movement.StopMomentum();   // önceki slide/launch momentumunu temizle

        if (hookedPickup != null)
        {
            pullingPickup = true;
            movement.DisableGravity = false;
        }
        else if (hookedEnemy != null && !hookedEnemy.IsLarge)
        {
            pullingEnemy = true;
            movement.DisableGravity = false;
            hookedEnemy.StartBeingPulled();
        }
        else
        {
            pullingEnemy = false;
            movement.DisableGravity = true;
        }

        // Uçuş sırasında tuş bırakıldıysa (tap): varır varmaz fırlatmalı bırakış
        if (releasedDuringFlight || !Input.GetKey(KeyBindings.Grapple))
        {
            releasedDuringFlight = false;
            releasedManually     = true;
            ReleaseGrapple();
        }
    }

    bool ActuallyGrounded()
    {
        Vector3 origin    = transform.position + Vector3.up * 0.1f;
        float   checkDist = movement.Controller.height * 0.5f + 0.45f;
        return Physics.Raycast(origin, Vector3.down, checkDist,
                               ~0, QueryTriggerInteraction.Ignore);
    }

    void Pull()
    {
        if (pullingPickup)
        {
            if (hookedPickup == null) { ReleaseGrapple(); return; }
            Vector3 toPlayer = transform.position - hookedPickup.transform.position;
            float dist = toPlayer.magnitude;
            if (dist <= pickupArrivalDistance) { hookedPickup.Collect(shoot); ReleaseGrapple(); return; }
            hookedPickup.transform.position += toPlayer.normalized * pullSpeed * Time.deltaTime;
            hookPoint = hookedPickup.transform.position;
        }
        else if (pullingEnemy && hookedEnemy != null)
        {
            Vector3 toPlayer = transform.position - hookedEnemy.transform.position;
            float dist = toPlayer.magnitude;
            if (dist <= arrivalDistance) { ReleaseGrapple(); return; }
            hookedEnemy.transform.position += toPlayer.normalized * pullSpeed * Time.deltaTime;
            hookPoint = hookedEnemy.transform.position;
        }
        else
        {
            // Yerden ayrılıp tekrar zemine değince bırak — ama sadece hedefe iyice
            // yaklaşmışken (uzaktayken ufak bir çıkıntı/rampa üzerinden geçerken
            // yanlışlıkla iptal olmasın)
            if (!ActuallyGrounded()) hasLeftGround = true;
            float distToTarget = Vector3.Distance(transform.position, hookPoint);
            if (hasLeftGround && ActuallyGrounded() && distToTarget <= arrivalDistance * 2f)
            { ReleaseGrapple(); return; }

            if (hookedEnemy != null)  hookPoint = hookedEnemy.transform.position;

            float dist = Vector3.Distance(transform.position, hookPoint);
            if (dist <= arrivalDistance) { EnterHang(); return; }   // hedefe vardı → asılı kal

            Vector3 dir    = (hookPoint - transform.position).normalized;
            Vector3 before = transform.position;
            movement.Controller.Move(dir * pullSpeed * Time.deltaTime);

            // İlerleme durduysa (duvara/kenara takıldık) → asılı kal.
            // Kavisli duvara sürtünmeyi iptal SANMAZ; sadece gerçekten tıkanınca durur.
            // Eşik gevşetildi: normal eğim/köşelerde tetiklenmesin, sadece gerçek
            // sıkışmalarda (hareket neredeyse tamamen durduğunda, daha uzun süre) devreye girsin.
            float moved = Vector3.Distance(before, transform.position);
            if (moved < pullSpeed * Time.deltaTime * 0.15f)
            {
                stuckTimer += Time.deltaTime;
                if (stuckTimer >= 0.4f) { EnterHang(); return; }
            }
            else stuckTimer = 0f;
        }

        if (rope != null)
        {
            Vector3 origin = hookOrigin != null ? hookOrigin.position : playerCamera.transform.position;
            rope.SetPosition(0, origin);
            rope.SetPosition(1, hookPoint);
        }
    }

    void ReleaseGrapple()
    {
        // Uçuş sırasında bırakılırsa — çekme kurulmadı, sadece iptal + cooldown
        if (firing)
        {
            firing = false;
            hookedEnemy = null; hookedPickup = null;
            releasedManually = false;
            cooldownUntil = Time.time + cooldown;
            if (rope != null) rope.gameObject.SetActive(false);
            return;
        }

        if (!isHooked) return;
        sfx.Play(hookReleaseClip, hookReleaseVolume);
        isHooked = false;
        cooldownUntil = Time.time + cooldown;   // kullanımdan sonra bekleme
        movement.DisableGravity = false;

        if (pullingEnemy)
        {
            // Momentum verme — düşmanı senin içinden geçirip arkandaki duvara
            // fırlatıyordu. Olduğu yerde bırak, NavMesh'e otursun (EnemyAI halleder).
            if (hookedEnemy != null)
                hookedEnemy.StopBeingPulled();
        }
        else
        {
            if (releasedManually && !ActuallyGrounded())
            {
                Vector3 dir = (hookPoint - transform.position).normalized;
                movement.Launch(dir * pullSpeed * launchMultiplier);
            }
            else
            {
                movement.StopMomentum();  // duvara çarpma/yere inme: momentum sıfırla
            }
        }

        releasedManually = false;
        hookedEnemy   = null;
        hookedPickup  = null;
        pullingEnemy  = false;
        pullingPickup = false;

        if (rope != null) rope.gameObject.SetActive(false);
    }

    // Not: Kanca çekişini artık çarpışma callback'i İPTAL ETMİYOR. Bitiş koşulları
    // Pull() içinde: hedefe varış, ilerlemenin durması (takılma) veya tuş bırakma.
    // Bu, kavisli duvara sürtününce "rastgele iptal" sorununu kökten kaldırdı.

    // ───────── Duvarda asılı kalma ─────────

    void EnterHang()
    {
        hanging   = true;
        hangTimer = wallHangDuration;
        movement.DisableGravity = true;   // duvara yapışık dur
        movement.StopMomentum();

        // Duvar normalini bul (asılıyken zıplama yönü)
        Vector3 toWall = hookPoint - transform.position; toWall.y = 0f;
        if (toWall.sqrMagnitude > 0.01f && Physics.Raycast(transform.position + Vector3.up * 0.5f,
                toWall.normalized, out RaycastHit h, toWall.magnitude + 1f, hookMask, QueryTriggerInteraction.Ignore))
            hangWallNormal = new Vector3(h.normal.x, 0f, h.normal.z).normalized;
        else
            hangWallNormal = (-toWall).normalized;
    }

    void HandleHang()
    {
        hangTimer -= Time.deltaTime;

        // Zıpla tuşu → duvardan zıpla
        if (Input.GetKeyDown(KeyBindings.Jump))
        {
            movement.Launch(hangWallNormal * wallHangJumpOut + Vector3.up * wallHangJumpUp);
            EndHang();
            return;
        }

        // Süre doldu → düş
        if (hangTimer <= 0f) { EndHang(); return; }

        movement.ZeroVerticalVelocity();   // duvarda kayma, sabit dur

        // İp görselini koru
        if (rope != null)
        {
            Vector3 origin = hookOrigin != null ? hookOrigin.position : playerCamera.transform.position;
            rope.SetPosition(0, origin);
            rope.SetPosition(1, hookPoint);
        }
    }

    void EndHang()
    {
        hanging = false;
        isHooked = false;
        movement.DisableGravity = false;
        cooldownUntil = Time.time + cooldown;

        hookedEnemy   = null;
        hookedPickup  = null;
        pullingEnemy  = false;
        pullingPickup = false;
        releasedManually = false;

        if (rope != null) rope.gameObject.SetActive(false);
    }

    public bool IsHooked => isHooked;

    // Kanca dışarıdan kapatıldığında aktif durumu güvenle sök (yerçekimi/ip/düşman geri al)
    void CancelActive()
    {
        firing = false; isHooked = false; hanging = false;
        if (movement != null) movement.DisableGravity = false;
        if (hookedEnemy != null) hookedEnemy.StopBeingPulled();
        hookedEnemy = null; hookedPickup = null;
        pullingEnemy = false; pullingPickup = false;
        releasedManually = false;
        cooldownUntil = Time.time + cooldown;
        if (rope != null) rope.gameObject.SetActive(false);
    }
}
}

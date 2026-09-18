using System;
using System.Collections;
using UnityEngine;
using Bloodrush.Player;

namespace Bloodrush.Flow
{
// Valf cevirme mini oyunu — ibre yakalama. Sadece MANTIK: ibre, yesil bolge, basari/iska,
// oyuncu kilidi. Gosterge (ValveGaugeUI) ve gerilim sesleri (PressureAmbience) event'leri dinler.
//
// Kurallar: ibre surekli doner; yesil bolgedeyken E = basari (bolge daralir, ibre hizlanir,
// bolge baska yere ziplar). Yanlis anda E ya da hic basmadan tam tur = iska (1 basari geri,
// ibre kisa sure durur). Hareket tusu = valfi birak, ilerleme silinir.
//
// ValveSequence bu bileseni kendi objesine ekler ve Begin'i cagirir; sahneye bir sey koyulmaz.
public class ValveMiniGame : MonoBehaviour
{
    public bool  IsActive    { get; private set; }
    public ValveInteractable Valve { get; private set; }
    public float NeedleAngle { get; private set; }   // derece; 0 = tepe, saat yonunde artar
    public float ZoneCenter  { get; private set; }
    public float ZoneWidth   { get; private set; }
    public int   Successes   { get; private set; }
    public int   Required    { get; private set; }
    public bool  InLockout   => lockout > 0f;

    public event Action<ValveInteractable>       Started;
    public event Action<bool>                    Checked;             // true = isabet
    public event Action                          NeedleEnteringZone;  // sinsi patlama anı
    public event Action<ValveInteractable, bool> Ended;               // true = valf acildi

    ValvePressureConfig      config;
    ValvePressureConfig.Tier tier;
    KeyCode interactKey;
    float   speed;
    float   lapAccum;      // son basistan/bolge degisiminden beri ibrenin katettigi aci
    float   lockout;
    bool    wasInZone;
    int     startFrame;

    PlayerMovement player;
    PlayerShoot    shoot;
    Transform      camHolder;
    bool           disabledPlayer;
    bool           prevCanShoot;
    float          camPitch;
    Coroutine      turning;

    public bool Begin(ValveInteractable valve, ValvePressureConfig cfg, int tierIndex, KeyCode key)
    {
        if (IsActive || valve == null || cfg == null) return false;

        config      = cfg;
        tier        = cfg.GetTier(tierIndex);
        interactKey = key;
        Valve       = valve;
        Required    = Mathf.Max(1, tier.checks);
        Successes   = 0;
        lapAccum    = 0f;
        lockout     = 0f;
        wasInZone   = false;
        startFrame  = Time.frameCount;   // baslatan E ayni karede ilk basis sayilmasin

        NeedleAngle = UnityEngine.Random.Range(0f, 360f);
        ApplyDifficulty();
        PlaceZoneAwayFromNeedle();

        LockPlayer();
        IsActive = true;
        Debug.Log($"[ValveMiniGame] {valve.name} basladi — kademe {tierIndex}, {Required} basari.", this);
        Started?.Invoke(valve);
        return true;
    }

    void Update()
    {
        if (!IsActive || Time.timeScale <= 0f) return;   // pause'da ibre ve girdi donar

        if (CancelPressed())
        {
            End(false);
            return;
        }

        float dt = Time.deltaTime;
        if (lockout > 0f)
        {
            lockout -= dt;
            return;
        }

        float delta = speed * dt;
        NeedleAngle = Mathf.Repeat(NeedleAngle + delta, 360f);
        lapAccum   += delta;

        bool inZone = InZone(NeedleAngle);
        if (inZone && !wasInZone) NeedleEnteringZone?.Invoke();
        wasInZone = inZone;

        if (Time.frameCount != startFrame && PressedCheck())
        {
            if (inZone) Hit();
            else        Miss();
        }
        else if (lapAccum >= 360f)
        {
            Miss();   // tam tur boyunca hic basilmadi
        }
    }

    void Hit()
    {
        Successes++;
        Checked?.Invoke(true);
        Valve.SetTurnProgress(Successes / (float)Required);

        if (Successes >= Required)
        {
            End(true);
            return;
        }
        ApplyDifficulty();
        PlaceZoneAwayFromNeedle();
    }

    void Miss()
    {
        Successes = Mathf.Max(0, Successes - 1);
        lockout   = config.missLockout;
        lapAccum  = 0f;
        Checked?.Invoke(false);
        Valve.SetTurnProgress(Successes / (float)Required);
        ApplyDifficulty();
    }

    void ApplyDifficulty()
    {
        float k   = Required > 1 ? Mathf.Clamp01(Successes / (Required - 1f)) : 0f;
        ZoneWidth = Mathf.Lerp(tier.zoneStart, tier.zoneEnd, k);
        speed     = Mathf.Lerp(tier.speedStart, tier.speedEnd, k);
    }

    void PlaceZoneAwayFromNeedle()
    {
        // Bolge ibrenin en az 90 derece ilerisinde: oyuncunun tepki verecek zamani olsun.
        ZoneCenter = Mathf.Repeat(NeedleAngle + UnityEngine.Random.Range(90f, 270f), 360f);
        lapAccum   = 0f;
        wasInZone  = false;
    }

    bool InZone(float angle) => Mathf.Abs(Mathf.DeltaAngle(angle, ZoneCenter)) <= ZoneWidth * 0.5f;

    bool PressedCheck()
    {
        if (KeyBindings.DownKey(interactKey) && InteractionInput.TryConsume()) return true;
        var gp = UnityEngine.InputSystem.Gamepad.current;
        return gp != null && gp.buttonSouth.wasPressedThisFrame;
    }

    // Basili tutulan degil YENI basilan hareket tusu: E'ye basarken W'ye yaslanmis oyuncu
    // mini oyuna girer girmez atilmasin.
    static bool CancelPressed() =>
        KeyBindings.Down(KeyBindings.Action.MoveForward) || KeyBindings.Down(KeyBindings.Action.MoveBack) ||
        KeyBindings.Down(KeyBindings.Action.MoveLeft)    || KeyBindings.Down(KeyBindings.Action.MoveRight);

    // Valfi birakmak / cozum disi bir sebeple durdurmak icin (ör. sahne kapanirken).
    public void Abort()
    {
        if (IsActive) End(false);
    }

    void End(bool opened)
    {
        IsActive = false;
        if (!opened) Valve.SetTurnProgress(0f);
        UnlockPlayer();

        var v = Valve;
        Valve = null;
        Debug.Log($"[ValveMiniGame] {v.name} {(opened ? "ACILDI" : "birakildi")}.", this);
        Ended?.Invoke(v, opened);
    }

    // ── Oyuncu kilidi ──────────────────────────────────────────────────

    void LockPlayer()
    {
        var cam = Camera.main;
        player = cam != null ? cam.GetComponentInParent<PlayerMovement>() : null;
        if (player == null) return;

        shoot = player.GetComponent<PlayerShoot>();
        if (shoot != null) { prevCanShoot = shoot.CanShoot; shoot.CanShoot = false; }

        disabledPlayer = player.enabled;
        player.enabled = false;   // hareket + bakis birlikte kilitlenir

        // Pitch'i tasiyan obje: kameradan oyuncu kokune kadar cikilan son ara obje.
        camHolder = cam.transform;
        while (camHolder.parent != null && camHolder.parent != player.transform) camHolder = camHolder.parent;

        if (turning != null) StopCoroutine(turning);
        turning = StartCoroutine(TurnToward(Valve.AimTarget));
    }

    IEnumerator TurnToward(Vector3 target)
    {
        Transform body = player.transform;
        Vector3 flat = target - body.position;
        flat.y = 0f;
        Quaternion fromYaw = body.rotation;
        Quaternion toYaw   = flat.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(flat) : fromYaw;

        float fromPitch = Mathf.DeltaAngle(0f, camHolder.localEulerAngles.x);
        Vector3 toTarget = target - camHolder.position;
        float toPitch = toTarget.sqrMagnitude > 0.0001f
            ? Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(toTarget.normalized.y, -1f, 1f)) * Mathf.Rad2Deg, -80f, 80f)
            : fromPitch;

        float dur = Mathf.Max(0.01f, config.cameraTurnTime);
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / dur);
            body.rotation = Quaternion.Slerp(fromYaw, toYaw, k);
            camPitch = Mathf.Lerp(fromPitch, toPitch, k);
            camHolder.localRotation = Quaternion.Euler(camPitch, 0f, 0f);
            yield return null;
        }
        turning = null;
    }

    void UnlockPlayer()
    {
        if (turning != null) { StopCoroutine(turning); turning = null; }
        if (player == null) return;

        // Look() kendi pitch degiskeniyle yazar — yoksa bakis kilit oncesine ziplardi.
        if (camHolder != null) player.SetLookPitch(Mathf.DeltaAngle(0f, camHolder.localEulerAngles.x));
        if (disabledPlayer) player.enabled = true;
        if (shoot != null) shoot.CanShoot = prevCanShoot;

        player = null;
        shoot  = null;
        disabledPlayer = false;
    }

    // Sahne kapanirken: valf ve dinleyiciler yok edilmis olabilir — event atma, sadece oyuncuyu birak.
    void OnDisable()
    {
        if (!IsActive) return;
        IsActive = false;
        Valve = null;
        UnlockPlayer();
    }
}
}

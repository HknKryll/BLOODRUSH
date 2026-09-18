using System.Collections;
using UnityEngine;
using Bloodrush.FX;
using Bloodrush.Player;
using Bloodrush.Shared.Audio;

namespace Bloodrush.Flow
{
// Valf bolumunun basinc kabusu: ilk mini oyun baslayinca devreye girer, iki valf de acilinca
// susar. Oyuncunun cevresinde rastgele fisirtilar, metal boru darbeleri ve patlamalar;
// yakin olanlar kamerayi sarsar, tavandan toz dokar, seyrek olarak feneri titretir.
// Mini oyun sirasinda siklasir, carkin yaninda surekli tislama olur ve bazi patlamalar
// BILEREK ibre yesil bolgeye girerken gelir (ibre hic etkilenmez — sadece sinirler).
// Pencere islerken hizlanan bir alarm biper.
//
// Tum sayilar ve klipler ValvePressureConfig'te. ValveSequence bu bileseni kendi objesine ekler.
public class PressureAmbience : MonoBehaviour
{
    ValvePressureConfig config;
    ValveMiniGame       game;
    ValveSequence       sequence;
    Flashlight          flashlight;

    bool  active, intense;
    float nextEvent, nextBeep, sneakyReadyAt, flickerReadyAt;

    AudioSource loopSource;
    Coroutine   loopFade;

    public void Bind(ValveMiniGame miniGame, ValveSequence seq, ValvePressureConfig cfg)
    {
        Unbind();
        game = miniGame;
        sequence = seq;
        config = cfg;

        game.Started            += OnStarted;
        game.Checked            += OnChecked;
        game.Ended              += OnEnded;
        game.NeedleEnteringZone += OnNeedleEnteringZone;
        sequence.ValveClosed    += OnValveClosed;
    }

    void Unbind()
    {
        if (game != null)
        {
            game.Started            -= OnStarted;
            game.Checked            -= OnChecked;
            game.Ended              -= OnEnded;
            game.NeedleEnteringZone -= OnNeedleEnteringZone;
        }
        if (sequence != null) sequence.ValveClosed -= OnValveClosed;
    }

    void OnDestroy() => Unbind();

    // ── Yasam dongusu ──────────────────────────────────────────────────

    public void Begin()
    {
        if (active) return;
        active = true;
        ScheduleNext();
        nextBeep = Time.time;
        Debug.Log("[PressureAmbience] Basinc gerilimi basladi.", this);
    }

    public void End()
    {
        if (!active) return;
        active = intense = false;
        StopLoop(2f);
        var cam = Camera.main;
        if (cam != null) PlayAt(config.releaseHiss, cam.transform.position + cam.transform.forward * 3f, config.hissVolume, 4f);
        Debug.Log("[PressureAmbience] Hat basinclandi — sesler susuyor.", this);
    }

    void Update()
    {
        if (!active) return;

        if (Time.time >= nextEvent)
        {
            RandomEvent();
            ScheduleNext();
        }

        // Alarm: yalnizca bir valfin penceresi islerken; sure azaldikca siklasir.
        float remaining = sequence.RemainingFraction;
        if (remaining >= 0f && config.alarmBeep != null && Time.time >= nextBeep)
        {
            SfxPlayer.PlayDetached(config.alarmBeep, Vector3.zero, config.alarmVolume, spatialBlend: 0f,
                                   lifetime: config.alarmBeep.length + 0.1f);
            nextBeep = Time.time + Mathf.Lerp(config.beepIntervalEnd, config.beepIntervalStart, remaining);
        }
    }

    void ScheduleNext()
    {
        Vector2 range = intense ? config.intenseInterval : config.calmInterval;
        nextEvent = Time.time + Random.Range(range.x, range.y);
    }

    // ── Mini oyun olaylari ─────────────────────────────────────────────

    void OnStarted(ValveInteractable valve)
    {
        Begin();
        intense = true;
        nextEvent = Mathf.Min(nextEvent, Time.time + Random.Range(config.intenseInterval.x, config.intenseInterval.y));
        StartLoop(valve.AimTarget);
    }

    void OnEnded(ValveInteractable valve, bool opened)
    {
        intense = false;
        StopLoop(0.4f);
    }

    void OnChecked(bool hit)
    {
        Vector3 at = game.Valve != null ? game.Valve.AimTarget : transform.position;
        if (hit)
        {
            PlayAt(config.turnStep, at, config.turnVolume, 2f);
        }
        else
        {
            PlayAt(config.steamBurst, at, config.burstVolume, 2f);
            CameraShake.Shake(config.shakeNear, 0.3f);
        }
    }

    void OnNeedleEnteringZone()
    {
        if (!active || Time.time < sneakyReadyAt || Random.value >= config.sneakyChance) return;
        sneakyReadyAt = Time.time + config.sneakyCooldown;
        Bang(near: true, big: Random.value < 0.5f);
        nextEvent = Mathf.Max(nextEvent, Time.time + 1.5f);   // hemen ardina ikinci olay binmesin
    }

    void OnValveClosed(ValveInteractable valve)
    {
        PlayAt(ValvePressureConfig.Pick(config.hissOneShots), valve.AimTarget, config.hissVolume, 3f);
    }

    // ── Rastgele olaylar ───────────────────────────────────────────────

    void RandomEvent()
    {
        if (Random.value >= config.bangRatio)
        {
            PlayAt(ValvePressureConfig.Pick(config.hissOneShots), PointAround(Random.value < 0.6f),
                   config.hissVolume, 5f);
            return;
        }
        Bang(Random.value < config.nearChance, Random.value < config.bigExplosionChance);
    }

    void Bang(bool near, bool big)
    {
        AudioClip clip;
        if (big) clip = ValvePressureConfig.Pick(near ? config.explosionsNear : config.explosionsFar)
                        ?? ValvePressureConfig.Pick(config.explosionsNear);
        else     clip = ValvePressureConfig.Pick(config.pipeBangs);

        Vector3 pos = PointAround(near);
        PlayAt(clip, pos, big ? config.explosionVolume : config.bangVolume, near ? 5f : 7f);

        if (!near) return;
        CameraShake.Shake(big ? config.shakeBig : config.shakeNear, big ? 0.45f : 0.3f);
        if (config.ceilingDust) SpawnCeilingDust();
        if (big) TryFlicker();
    }

    void TryFlicker()
    {
        if (Time.time < flickerReadyAt || Random.value >= config.flickerChance) return;
        if (flashlight == null) flashlight = FindFirstObjectByType<Flashlight>();
        if (flashlight == null || !flashlight.IsOn) return;

        flickerReadyAt = Time.time + config.flickerCooldown;
        flashlight.Flicker(Random.Range(config.flickerDuration.x, config.flickerDuration.y));
    }

    // Arkadan ve yandan gelen sesler daha tedirgin: yon, kameranin gorus acisinin disina egilimli.
    Vector3 PointAround(bool near)
    {
        var cam = Camera.main;
        Vector3 origin = cam != null ? cam.transform.position : transform.position;
        Vector3 fwd = cam != null ? cam.transform.forward : Vector3.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        fwd.Normalize();

        float angle = Random.value < 0.65f ? Random.Range(100f, 260f) : Random.Range(-100f, 100f);
        Vector3 dir = Quaternion.Euler(0f, angle, 0f) * fwd;
        Vector2 range = near ? config.nearDistance : config.farDistance;
        return origin + dir * Random.Range(range.x, range.y) + Vector3.up * Random.Range(0.5f, 2f);
    }

    // SfxPlayer.PlayAtPoint'in varsayilan dusumu (min 1 m) 20 m'deki sesi duyulmaz yapar;
    // burada min mesafe acilarak uzak patlamalar da duyulur kalir.
    static void PlayAt(AudioClip clip, Vector3 pos, float volume, float minDistance)
    {
        if (clip == null) return;
        var go = new GameObject("BasincSesi");
        go.transform.position = pos;
        var src = go.AddComponent<AudioSource>();
        src.clip         = clip;
        src.volume       = volume;
        src.spatialBlend = 1f;
        src.rolloffMode  = AudioRolloffMode.Logarithmic;
        src.minDistance  = minDistance;
        src.maxDistance  = 80f;
        src.outputAudioMixerGroup = AudioRouting.Sfx;
        src.Play();
        Destroy(go, clip.length + 0.1f);
    }

    // ── Carkin yanindaki surekli tislama ───────────────────────────────

    void StartLoop(Vector3 at)
    {
        if (config.hissLoop == null) return;
        if (loopSource == null)
        {
            var go = new GameObject("ValfTislamasi");
            go.transform.SetParent(transform, false);
            loopSource = go.AddComponent<AudioSource>();
            loopSource.loop         = true;
            loopSource.playOnAwake  = false;
            loopSource.spatialBlend = 1f;
            loopSource.minDistance  = 2f;
            loopSource.maxDistance  = 30f;
            loopSource.outputAudioMixerGroup = AudioRouting.Sfx;
        }
        loopSource.transform.position = at;
        loopSource.clip = config.hissLoop;
        if (!loopSource.isPlaying)
        {
            loopSource.volume = 0f;
            loopSource.time   = Random.Range(0f, config.hissLoop.length * 0.8f);
            loopSource.Play();
        }
        FadeLoop(config.hissLoopVolume, 0.3f);
    }

    void StopLoop(float fade)
    {
        if (loopSource != null && loopSource.isPlaying) FadeLoop(0f, fade);
    }

    void FadeLoop(float to, float duration)
    {
        if (loopFade != null) StopCoroutine(loopFade);
        loopFade = StartCoroutine(FadeLoopRoutine(to, duration));
    }

    IEnumerator FadeLoopRoutine(float to, float duration)
    {
        float from = loopSource.volume, t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            loopSource.volume = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        loopSource.volume = to;
        if (to <= 0f) loopSource.Stop();
        loopFade = null;
    }

    // ── Tavandan toz ───────────────────────────────────────────────────

    void SpawnCeilingDust()
    {
        var cam = Camera.main;
        if (cam == null) return;

        // Gorunsun diye oyuncunun biraz onune: tavani yukari isinla bul.
        Vector3 fwd = cam.transform.forward;
        fwd.y = 0f;
        Vector3 probe = cam.transform.position + fwd.normalized * Random.Range(1.2f, 3f)
                      + Quaternion.Euler(0f, 90f, 0f) * fwd.normalized * Random.Range(-1.2f, 1.2f);
        if (!Physics.Raycast(probe, Vector3.up, out RaycastHit hit, 8f, ~0, QueryTriggerInteraction.Ignore)) return;

        var go = new GameObject("TavanTozu");
        go.transform.position = hit.point - Vector3.up * 0.05f;

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop            = false;
        main.playOnAwake     = false;
        main.duration        = 0.8f;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0f, 0.3f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.02f, 0.07f);
        main.startColor      = new Color(0.55f, 0.52f, 0.47f, 0.8f);
        main.gravityModifier = 0.6f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = Mathf.Max(10, config.dustParticles * 2);

        var emission = ps.emission;
        emission.rateOverTime = config.dustParticles * 0.6f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(config.dustParticles * 0.5f)) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale     = new Vector3(1.2f, 0.05f, 1.2f);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        var mat = SoftDotVFX.CreateMaterial(Color.white);
        renderer.material = mat;

        ps.Play();
        Destroy(mat, 4f);
        Destroy(go, 4f);
    }
}
}

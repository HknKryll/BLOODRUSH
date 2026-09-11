using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Bloodrush.Player;
using Bloodrush.FX;

namespace Bloodrush.Flow
{
// "Asansör kazası": oyuncu kabine biner, kamera tavandaki halata döner, kabin sarsılıp
// ışıkları kırpışır, halat kopar — ekran kararırken oyuncu TÜM ekipmanını kaybeder ve
// karanlık bir odada silahsız uyanır.
//
// Elevator'ın içine HİÇ dokunmaz: IElevatorSequenceHook üzerinden gözlemci olarak takılır
// (Elevator aynı objede GetComponent ile bulur). Bu component olmayan asansörler — CH1'deki
// mevcut asansör dahil — eskisiyle birebir aynı davranır.
//
// Kamera notu: PlayerMovement.cameraHolder = Main Camera'nın ta kendisi ve CameraShake de
// AYNI objede. Look() yalnızca localRotation, CameraShake yalnızca localPosition yazar —
// bu yüzden buradaki SADECE-ROTASYON sineması sarsıntıyla çakışmaz ve CameraShake'i
// kapatmak GEREKMEZ (SeatInteractable kapatmak zorundaydı, çünkü kamerayı konum olarak
// taşıyordu). Sinema sırasında PlayerMovement zaten Elevator tarafından kapatılmış olur.
[RequireComponent(typeof(Elevator))]
public class ElevatorAccidentSequence : MonoBehaviour, IElevatorSequenceHook
{
    [Header("Kabin Sineması")]
    [Tooltip("Kameranın döneceği nokta (tavandaki halat/motor). Boşsa kamera dönmez.")]
    [SerializeField] Transform focusPoint;
    [SerializeField] float     turnDuration  = 1.2f;
    [Tooltip("Kamera döndükten sonra, kaos başlamadan önceki bekleme.")]
    [SerializeField] float     holdAfterTurn = 0.5f;
    [SerializeField] AudioClip creakClip;                            // metal iniltisi
    [Tooltip("1'in ÜSTÜ yükseltir. Saha kayıtları oyun SFX'lerinden çok daha kısıktır; " +
             "silah sesinin yanında duyulmuyorsa buradan yükselt.")]
    [SerializeField] [Range(0f,4f)] float creakVolume = 1.5f;

    [Header("Kaza (kararma penceresi)")]
    [Tooltip("Kabin ışıkları — kaza boyunca kırpışır, sonunda tamamen söner.")]
    [SerializeField] Light[]   cabinLights;
    [SerializeField] AudioClip snapClip;                             // halat kopma
    [Tooltip("1'in ÜSTÜ yükseltir — kopma anı en yüksek ses olmalı.")]
    [SerializeField] [Range(0f,4f)] float snapVolume = 2.5f;
    [Tooltip("Sarsıntı şiddeti (m). DİKKAT: CameraShake tavanı 0.5 m — kapalı bir kabinde " +
             "0.3+ değerler kamerayı yarım metre savurur ve kafa dönüşünü boğar, hareket " +
             "'takılıyor' gibi görünür. 0.12-0.18 arası bu sahne için yeterli.")]
    [SerializeField] float shakeIntensity = 0.15f;
    [Tooltip("Sarsıntının kaç saniyede bir tazeleneceği. Çok sık tazelemek trauma'yı tavana " +
             "yapıştırıp sürekli maksimum sarsıntı yaratır — yuvarlanan bir gümbürtü yerine.")]
    [SerializeField] float shakeInterval  = 0.28f;
    [SerializeField] float flickerMin     = 0.03f;
    [SerializeField] float flickerMax     = 0.14f;

    [Header("Telaşlı bakınma (kaos sırasında)")]
    [Tooltip("Kafanın sağa/sola çevrileceği EN BÜYÜK açı (derece). 0 = kapalı. " +
             "Panikte insan gerçekten 70-90° döner; 30° civarı 'nişan alma' gibi durur.")]
    [SerializeField] float panicYawMax  = 85f;
    [Tooltip("En küçük açı — her çevirme bunun ile Max arasında rastgele seçilir.")]
    [SerializeField] float panicYawMin  = 45f;
    [Tooltip("Yukarı/aşağı sapma (derece). Küçük tut, yoksa baş dönmesi yapar.")]
    [SerializeField] float panicPitch   = 8f;
    [Tooltip("Kafa çevirme HIZI (derece/sn). Süre açıya göre hesaplanır — 85°'lik dönüş " +
             "420'de ~0.2 sn sürer. Çok yüksek değer 'aim trainer' gibi yapay durur.")]
    [SerializeField] float panicTurnSpeed = 420f;
    [Tooltip("Çevirdikten sonra o yöne BAKMA süresi (sn). İnsan döner, görür, sonra döner.")]
    [SerializeField] float panicHoldMin  = 0.18f;
    [SerializeField] float panicHoldMax  = 0.45f;
    [Tooltip("Kafanın hedefi aşıp geri oturması (savrulma). 0 = kapalı, 0.9 ≈ doğal.")]
    [SerializeField] float panicOvershoot = 0.9f;

    [Tooltip("Kaos en az bu kadar sürsün — asansörün fade ayarları çok kısaysa sekans " +
             "tek karede çöküp sesler duyulmadan kesilmesin.")]
    [SerializeField] float minChaosDuration = 1.2f;
    [Tooltip("Ekran karardıktan sonra sesin sönme süresi. 0 = anında kes (önerilmez — " +
             "kopma sesi karanlığa doğru sönerek devam etmeli).")]
    [SerializeField] float audioFadeOut = 0.8f;

    [Header("Uyanış")]
    [Tooltip("Ekran tam karardığında oyuncunun TÜM ekipmanını al (PlayerLoadout.DisarmAll).")]
    [SerializeField] bool  disarmOnBlackout   = true;
    [Tooltip("Yerçekimi odasından çıkıyoruz — düşük yerçekimi/flip kalıntısını temizle. AÇIK kalsın.")]
    [SerializeField] bool  resetGravityOnWake = true;
    [SerializeField] bool  setWakePitch       = true;
    [Tooltip("Uyanırken bakış açısı (+ = aşağı bakar).")]
    [SerializeField] float wakePitch          = 12f;

    Camera         cam;
    PlayerMovement rider;
    Quaternion     savedCamLocalRot;
    bool           camRotSaved;

    AudioSource      audioSrc;
    Coroutine        chaosRoutine;
    Coroutine        panicRoutine;
    readonly List<Light> litLights = new();   // kaza öncesi AÇIK olanlar (BossBlackoutSequence deseni)

    void Start() => EnsureAudio();

    // Kendi AudioSource'umuz: SfxPlayer'da Stop() yok, oysa tam siyahta sesi KESMEK
    // zorundayız (5. beat "sessiz karanlık"). Elevator.rideLoopSource ile aynı desen.
    //
    // TEMBEL kurulum: Start()'a güvenmek yerine ilk kullanımda da kendini kuruyor. Aksi
    // halde component'in Start'ı herhangi bir sebeple geç kalırsa (script yeniden derleme,
    // objenin sonradan aktifleşmesi) audioSrc null kalıp sesler SESSİZCE yutuluyordu.
    void EnsureAudio()
    {
        if (audioSrc != null) return;
        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.playOnAwake        = false;
        audioSrc.spatialBlend       = 0f;   // 2D — konumdan bağımsız duyulsun
        audioSrc.volume             = 1f;
        audioSrc.mute               = false;
        audioSrc.outputAudioMixerGroup = null;
        audioSrc.ignoreListenerPause   = true;
    }

    // ───────────────── IElevatorSequenceHook ─────────────────

    public IEnumerator OnCabinCinematic(Elevator elevator)
    {
        // Ses EN BAŞTA çalsın: kamera bulunamazsa aşağıdaki erken çıkış sesi de yutuyordu.
        Play(creakClip, creakVolume, "Creak");

        rider = elevator.Rider;
        cam   = rider != null ? rider.GetComponentInChildren<Camera>(true) : Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[ElevatorAccidentSequence] Oyuncu kamerası bulunamadı — " +
                              "kamera sineması ve telaşlı bakınma atlanıyor.", this);
            yield break;
        }

        savedCamLocalRot = cam.transform.localRotation;
        camRotSaved      = true;

        if (focusPoint != null && turnDuration > 0f)
        {
            Quaternion from = cam.transform.rotation;
            Vector3    dir  = focusPoint.position - cam.transform.position;
            if (dir.sqrMagnitude > 0.0001f)
            {
                // Poz Transform'u kopyalamak yerine HEDEFE BAKIYORUZ: tasarımcı tavana boş
                // bir obje bırakınca yeter, oyuncu kabinde nerede durursa dursun çalışır,
                // ve roll kendiliğinden sıfırlanır.
                Quaternion to = Quaternion.LookRotation(dir, Vector3.up);
                float t = 0f;
                while (t < turnDuration)
                {
                    t += Time.deltaTime;
                    float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / turnDuration));
                    cam.transform.rotation = Quaternion.Slerp(from, to, p);
                    yield return null;
                }
                cam.transform.rotation = to;
            }
        }

        if (holdAfterTurn > 0f) yield return new WaitForSeconds(holdAfterTurn);
    }

    public void OnBlackoutBegin(float chaosWindow)
    {
        // Asansörün fade ayarları kısaysa (Use Fade kapalı / Pre Fade Chaos Hold 0) pencere
        // sıfıra çöker ve sekans tek karede biterdi — tabanla koru.
        chaosWindow = Mathf.Max(chaosWindow, minChaosDuration);

        chaosRoutine = StartCoroutine(Chaos(chaosWindow));
        // Ayrı coroutine: Chaos() ışık kırpışması için WaitForSeconds ile ilerliyor, oysa
        // bakınma HER KARE güncellenmeli. İkisini ayırmak flicker ritmini de bozmuyor.
        panicRoutine = StartCoroutine(PanicLook(chaosWindow));
    }

    public void OnFullyBlack()
    {
        if (chaosRoutine != null) { StopCoroutine(chaosRoutine); chaosRoutine = null; }
        if (panicRoutine != null) { StopCoroutine(panicRoutine); panicRoutine = null; }
        SetLights(false);                       // kabin öldü — ışıklar bir daha yanmaz

        // Sesi SERT KESMİYORUZ: kopma sesi karanlığa doğru sönerek devam etsin. Sert kesme
        // hem dramatik olarak yanlıştı hem de kaos penceresi kısa olduğunda sesleri hiç
        // duyulmadan öldürüyordu. Sessizlik zaten sönüş bitince geliyor.
        if (audioSrc != null)
        {
            if (audioFadeOut > 0f) StartCoroutine(FadeOutAudio(audioFadeOut));
            else                   audioSrc.Stop();
        }

        if (rider != null && resetGravityOnWake)
        {
            // Yerçekimi odasından geliyoruz. Teleport() flip GÖRSELİNİ sıfırlıyor ama
            // GravityScale/DisableGravity/SpeedMultiplier'a dokunmuyor — temizlenmezse
            // oyuncu karanlık odada uçar gibi hareket eder.
            rider.SetFlipMode(false);
            rider.DisableGravity  = false;
            rider.GravityScale    = 1f;
            rider.SpeedMultiplier = 1f;
            rider.JumpEnabled     = true;
            rider.SlideEnabled    = true;
        }

        if (disarmOnBlackout) PlayerLoadout.DisarmAll();
    }

    public void OnRideEnd()
    {
        RestoreCameraRotation();
        if (rider != null && setWakePitch) rider.SetLookPitch(wakePitch);
    }

    // ───────────────── Kaza efektleri ─────────────────

    IEnumerator Chaos(float seconds)
    {
        CacheLights();
        Play(snapClip, snapVolume, "Snap");
        CameraShake.Shake(shakeIntensity);      // ilk sert vuruş

        float end       = Time.time + Mathf.Max(0f, seconds);
        float nextShake = Time.time + shakeInterval;

        // DamageVignette.DoFlicker ritmi: kısa, düzensiz aralıklarla aç/kapa.
        while (Time.time < end)
        {
            SetLights(false);
            yield return new WaitForSeconds(Random.Range(flickerMin, flickerMax));
            SetLights(true);
            yield return new WaitForSeconds(Random.Range(flickerMin, flickerMax));

            if (Time.time >= nextShake)
            {
                CameraShake.Shake(shakeIntensity * 0.6f);   // trauma sönümlenmesin
                nextShake = Time.time + shakeInterval;
            }
        }

        SetLights(false);
        chaosRoutine = null;
    }

    // Kaza boyunca kafayı SERT şekilde sağa-sola çevirir: "ne oluyor?!" paniği.
    //
    // Sürekli bir gürültü eğrisi (Perlin/sinüs) yerine AYRIK hedefler kullanıyorum: her
    // seferinde bir yöne kırbaç gibi çevir, kısa duraksa, sonra öbür yöne. Yumuşak salınım
    // "sarhoş" gibi durur; asıl panik hissi, gözün bir şeye kilitlenip hemen başka yere
    // sıçramasından gelir. Yön her adımda TERS çevrilir (sağ-sol-sağ), açı rastgele —
    // düzenli olsa metronom gibi mekanik okunurdu.
    //
    // CameraShake ile çakışmaz: o localPosition, bu rotation yazıyor; ikisi üst üste biner.
    IEnumerator PanicLook(float seconds)
    {
        if (cam == null || panicYawMax <= 0f) yield break;

        Quaternion baseRot = cam.transform.rotation;   // kapıya bakan açı
        float end  = Time.time + seconds;
        float sign = Random.value < 0.5f ? -1f : 1f;

        while (Time.time < end)
        {
            float yaw   = Random.Range(Mathf.Min(panicYawMin, panicYawMax), panicYawMax) * sign;
            float pitch = Random.Range(-panicPitch, panicPitch);
            sign = -sign;                              // bir sağa, bir sola

            Quaternion from   = cam.transform.rotation;
            Quaternion target = baseRot * Quaternion.Euler(pitch, yaw, 0f);

            // Süre AÇIYA GÖRE: 90°'lik dönüş 20°'likten uzun sürer. Sabit süre kullanmak,
            // büyük açılarda insan üstü bir hız üretip "aim trainer" hissi veriyordu.
            float angle = Quaternion.Angle(from, target);
            float dur   = Mathf.Max(0.04f, angle / Mathf.Max(1f, panicTurnSpeed));

            float t = 0f;
            while (t < dur && Time.time < end)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / dur);
                // easeOutBack: hedefi hafif aşıp geri oturur — kafa savrulup duruyormuş gibi.
                float c1 = panicOvershoot, c3 = c1 + 1f, u = p - 1f;
                p = 1f + c3 * u * u * u + c1 * u * u;
                // Unclamped: p 1'i geçtiğinde gerçekten hedefin ötesine savrulsun.
                cam.transform.rotation = Quaternion.SlerpUnclamped(from, target, p);
                yield return null;
            }
            cam.transform.rotation = target;

            yield return new WaitForSeconds(Random.Range(panicHoldMin, panicHoldMax));
        }

        cam.transform.rotation = baseRot;
        panicRoutine = null;
    }

    IEnumerator FadeOutAudio(float seconds)
    {
        float start = audioSrc.volume;
        float t = 0f;
        while (t < seconds && audioSrc != null)
        {
            t += Time.deltaTime;
            audioSrc.volume = Mathf.Lerp(start, 0f, t / seconds);
            yield return null;
        }
        if (audioSrc != null) { audioSrc.Stop(); audioSrc.volume = start; }
    }

    void CacheLights()
    {
        litLights.Clear();
        if (cabinLights == null) return;
        foreach (var l in cabinLights)
            if (l != null && l.enabled) litLights.Add(l);
    }

    void SetLights(bool on)
    {
        foreach (var l in litLights)
            if (l != null) l.enabled = on;
    }

    void Play(AudioClip clip, float volume, string label)
    {
        if (clip == null)
        {
            Debug.LogWarning($"[ElevatorAccidentSequence] {label} klibi ATANMAMIŞ — " +
                              "Inspector'da bu alanı doldur.", this);
            return;
        }

        EnsureAudio();
        if (audioSrc == null)
        {
            Debug.LogError($"[ElevatorAccidentSequence] {label}: AudioSource kurulamadı.", this);
            return;
        }

        audioSrc.PlayOneShot(clip, volume);
        Debug.Log($"[ElevatorAccidentSequence] {label} çalındı: '{clip.name}' " +
                  $"({clip.length:0.00} sn) ses={volume:0.0}", this);
    }

    void RestoreCameraRotation()
    {
        if (!camRotSaved || cam == null) return;
        cam.transform.localRotation = savedCamLocalRot;
        camRotSaved = false;
    }

    // Sekans ortasında (ölüm/sahne değişimi) yarıda kalırsa kamerayı ve sesi geri al —
    // BossBlackoutSequence.CleanupOnDeath konvansiyonu. Işıklar bilerek KAPALI bırakılır:
    // kabin gerçekten düştü.
    void OnDisable()
    {
        if (chaosRoutine != null) { StopCoroutine(chaosRoutine); chaosRoutine = null; }
        if (panicRoutine != null) { StopCoroutine(panicRoutine); panicRoutine = null; }
        if (audioSrc != null) audioSrc.Stop();
        RestoreCameraRotation();
    }
}
}

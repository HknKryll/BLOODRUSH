using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace Bloodrush.Shared.Audio
{
// Bir odanin arka plan muzigi: loop calar, yumusakca acilip kapanir.
//
// Odaya ozel HICBIR sey bilmez — hangi odada oldugunu, oyuncunun nerede oldugunu, ne zaman
// sonecegini bilmez. Sadece "cal / ac / kapat" der. Bu yuzden baska odalarda da aynen
// kullanilabilir: bos bir objeye ekle, klibi ata, bitti.
//
// Sondurmeyi MusicFadeZone tetikler (ya da istersen herhangi bir UnityEvent'ten
// FadeIn/FadeOut cagirabilirsin).
//
// NOT: Projedeki SfxPlayer bu is icin uygun degil — o PlayOneShot tabanli, loop ve fade
// destegi yok. Bu yuzden ayri, kucuk bir bilesen.
[DisallowMultipleComponent]
public class RoomMusic : MonoBehaviour
{
    [Header("Muzik")]
    [SerializeField] AudioClip clip;
    [Tooltip("Projede AudioMixer yok; ileride kurarsan muzik grubunu buraya baglarsin. " +
             "Bos birakilabilir.")]
    [SerializeField] AudioMixerGroup mixerGroup;

    [Header("Ses")]
    [Range(0f, 1f)]
    [SerializeField] float volume = 0.5f;
    [Tooltip("0 = 2D (her yerde esit duyulur), 1 = 3D (kaynaga yaklastikca artar). " +
             "Arka plan muzigi icin 2D dogrusu.")]
    [Range(0f, 1f)]
    [SerializeField] float spatialBlend = 0f;

    [Header("Gecisler")]
    [SerializeField] float fadeInDuration  = 2f;
    [SerializeField] float fadeOutDuration = 3f;
    [Tooltip("Sahne baslar baslamaz fade-in ile calsin mi?")]
    [SerializeField] bool  playOnStart = true;

    AudioSource src;
    Coroutine   fadeRoutine;

    public bool IsPlaying => src != null && src.isPlaying;

    void Awake()
    {
        src = gameObject.AddComponent<AudioSource>();
        src.clip                 = clip;
        src.loop                 = true;
        src.playOnAwake          = false;
        src.spatialBlend         = spatialBlend;
        src.outputAudioMixerGroup = mixerGroup;
        src.volume               = 0f;

        if (clip == null)
            Debug.LogWarning("[RoomMusic] Klip atanmamis — muzik calmayacak.", this);
    }

    void Start()
    {
        if (playOnStart) FadeIn();
    }

    // UnityEvent'ten de cagrilabilir.
    public void FadeIn()
    {
        if (src == null || clip == null) return;

        // Duraklatilmissa KALDIGI YERDEN devam et. Play() bastan baslatirdi — odaya her
        // donuste muzik sifirdan calardi.
        src.UnPause();
        if (!src.isPlaying) src.Play();

        StartFade(volume, fadeInDuration);
    }

    public void FadeOut()
    {
        if (src == null) return;
        StartFade(0f, fadeOutDuration);
    }

    // Tek coroutine slotu: oyuncu tetige girip cikip tekrar girerse fade'ler UST USTE
    // BINMEZ. Yenisi mevcut ses seviyesinden devam eder, ses sicramaz.
    void StartFade(float target, float duration)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(Fade(target, duration));
    }

    IEnumerator Fade(float target, float duration)
    {
        float start = src.volume;

        if (duration <= 0f)
        {
            src.volume = target;
        }
        else
        {
            // Sure kalan MESAFEYLE orantili: yarida donen bir fade (gir-cik-gir) bastan
            // basliyormus gibi yavaslamasin, kalan yolu kendi hizinda bitirsin.
            float span = Mathf.Abs(target - start);
            float full = Mathf.Max(0.0001f, volume);          // tam aralik: 0..volume
            float dur  = duration * Mathf.Clamp01(span / full);
            if (dur <= 0.0001f) dur = 0.0001f;

            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                src.volume = Mathf.Lerp(start, target, t / dur);
                yield return null;
            }
            src.volume = target;
        }

        // Tamamen sustuysa kaynagi durdur — sessiz bir loop'u dondurmenin anlami yok.
        if (Mathf.Approximately(target, 0f) && src.isPlaying) src.Pause();

        fadeRoutine = null;
    }

    void OnValidate()
    {
        if (src == null) return;
        src.spatialBlend          = spatialBlend;
        src.outputAudioMixerGroup = mixerGroup;
    }
}
}

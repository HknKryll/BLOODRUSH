using UnityEngine;
using Bloodrush.Shared.Audio;

namespace Bloodrush.UI.Menu
{
// Arayuz sesleri icin hazir yapi. KLIPLER BOS — sesleri sonra ekleyeceksin, kod
// degistirmeye gerek kalmayacak.
//
// Kendi AudioSource'unu kurar ve GameMixer'daki UI grubuna baglar, boylece Ayarlar'daki
// "Arayuz" kaydiricisi bunlari ayri kontrol eder. Klip atanmamissa sessizce hicbir sey
// yapmaz — eksik ses hata uretmez.
//
// Kullanim: MenuButton.OnHighlighted += UISounds.Hover;  btn.onClick -> UISounds.Click
public class UISounds : MonoBehaviour
{
    [Tooltip("Butona gelince/odaklanınca. Boş bırakılabilir.")]
    [SerializeField] AudioClip hoverClip;
    [Tooltip("Tıklanınca. Boş bırakılabilir.")]
    [SerializeField] AudioClip clickClip;
    [Tooltip("Geri/iptal. Boş bırakılabilir.")]
    [SerializeField] AudioClip backClip;
    [Range(0f, 1f)] [SerializeField] float volume = 0.6f;

    static UISounds instance;
    AudioSource src;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;

        src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake  = false;
        src.spatialBlend = 0f;
        src.volume       = volume;
        // Arayuz sesleri kendi mixer grubundan gecer — muzik/efektten bagimsiz kisilir.
        src.outputAudioMixerGroup = AudioRouting.Ui;
        // Menude Time.timeScale 0 olabilir; AudioSource bundan etkilenmez ama emin olalim.
        src.ignoreListenerPause = true;
    }

    public static UISounds Ensure()
    {
        if (instance != null) return instance;
        var go = new GameObject("UISounds");
        DontDestroyOnLoad(go);
        return go.AddComponent<UISounds>();
    }

    static void Play(AudioClip clip)
    {
        if (instance == null || clip == null || instance.src == null) return;
        instance.src.PlayOneShot(clip, instance.volume);
    }

    public static void Hover() => Play(instance != null ? instance.hoverClip : null);
    public static void Click() => Play(instance != null ? instance.clickClip : null);
    public static void Back()  => Play(instance != null ? instance.backClip  : null);
}
}

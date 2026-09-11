using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Bloodrush.Player;
using Bloodrush.Shared.Audio;

namespace Bloodrush.Flow
{
// ADIM 12 — Çıkış. Oyuncu eşikten geçince ekran kararır ve sekans BİTTİ olarak işaretlenir.
//
// Bir sonraki bölümü (gözlem odası / kasaba) BURASI KURMAZ ve ona referans vermez —
// DarkSequence.Complete() çağrılır, o kadar. Sonraki bölümü kuran kişi
// DarkSequence.OnComplete'e abone olur ya da onExit event'ine kendi işini bağlar.
[RequireComponent(typeof(Collider))]
public class DarkSequenceExit : MonoBehaviour
{
    [Header("Kapı arkasındaki ışık (ADIM 12)")]
    [Tooltip("Kapı açılınca yanacak SOĞUK BEYAZ ışık — karanlık/endüstriyel tondan kopuş. " +
             "Bunu kapının açılmasına (UnityEvent) bağlayabilir ya da burada bırakabilirsin.")]
    [SerializeField] Light coldLight;
    [SerializeField] float coldLightFadeIn = 1.2f;
    [SerializeField] float coldLightLumen  = 2600f;

    [Header("Çıkış")]
    [SerializeField] float fadeOutDuration = 1.2f;
    [Tooltip("Kararma bittikten sonra ekranın siyah kaldığı süre.")]
    [SerializeField] float holdBlack = 0.6f;
    [Tooltip("Kararma sırasında oyuncu kilitlensin mi?")]
    [SerializeField] bool  freezePlayer = true;

    [Header("Ses")]
    [SerializeField] AudioClip exitClip;
    [SerializeField] [Range(0f,2f)] float exitVolume = 1f;

    [Tooltip("Kararma bitince tetiklenir. Sonraki bölümü buraya BAĞLAMA — bu sekans " +
             "kapalı bir bütün; sonrası DarkSequence.OnComplete üzerinden dinlenir.")]
    public UnityEvent onExit;

    bool fired;

    void Reset()
    {
        var c = GetComponent<Collider>();
        if (c) c.isTrigger = true;
    }

    void Start()
    {
        if (coldLight != null) coldLight.enabled = false;
    }

    // Kapı açılınca çağır (UnityEvent) — soğuk beyaz ışık sızmaya başlar.
    public void RevealColdLight()
    {
        if (coldLight == null) return;
        coldLight.enabled = true;
        StartCoroutine(FadeLight());
    }

    IEnumerator FadeLight()
    {
        var hd = coldLight.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
        if (hd == null) hd = coldLight.gameObject.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
        hd.EnableShadows(false);

        float t = 0f;
        while (t < coldLightFadeIn)
        {
            t += Time.deltaTime;
            hd.SetIntensity(Mathf.Lerp(0f, coldLightLumen, t / coldLightFadeIn),
                            UnityEngine.Rendering.HighDefinition.LightUnit.Lumen);
            yield return null;
        }
        hd.SetIntensity(coldLightLumen, UnityEngine.Rendering.HighDefinition.LightUnit.Lumen);
    }

    void OnTriggerEnter(Collider other)
    {
        if (fired) return;
        if (other.GetComponentInParent<PlayerMovement>() == null) return;

        fired = true;
        StartCoroutine(Finish());
    }

    IEnumerator Finish()
    {
        var playerGo = GameObject.FindGameObjectWithTag("Player");
        var pm       = playerGo != null ? playerGo.GetComponent<PlayerMovement>() : null;

        SfxPlayer.PlayAtPoint(exitClip, transform.position, exitVolume);

        var img = GameFlow.CreateOverlay(Color.black);
        float t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;
            img.color = new Color(0f, 0f, 0f, t / fadeOutDuration);
            yield return null;
        }
        img.color = Color.black;

        if (freezePlayer && pm != null) pm.enabled = false;
        if (holdBlack > 0f) yield return new WaitForSeconds(holdBlack);

        DarkSequence.Complete();
        onExit?.Invoke();

        // Perde BİLEREK ekranda bırakılıyor: sekans burada bitiyor, ekranı kimin ve ne zaman
        // açacağına sonraki bölüm karar verecek. Yok etmek istersen aşağıdaki satırı aç.
        // Destroy(img.canvas.gameObject);
    }
}
}

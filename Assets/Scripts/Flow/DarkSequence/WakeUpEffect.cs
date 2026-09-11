using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Bloodrush.Player;

namespace Bloodrush.Flow
{
// ADIM 1 — Uyanış: "yerden kalkma" hissi. Bulanık görüş + vinyet 1'den 0'a iner, aynı anda
// siyah perde açılır.
//
// Volume'u kod içinde kurma deseni DamageVignette'ten alındı (o da global bir Volume kurup
// Vignette/ChromaticAberration ekliyor) — ayrı bir post-process sistemi kurmuyoruz.
//
// Sekansın EN BAŞINDA, sahne açılışında kendiliğinden çalışır. Asansör kazasından gelen
// SceneFadeIn'in karartması zaten söndükten sonra devreye girdiği için ikisi çakışmaz:
// o ekranı açar, bu görüşü netleştirir.
public class WakeUpEffect : MonoBehaviour
{
    [Header("Süreler")]
    [Tooltip("Sahne açılışından sonra beklenecek süre (SceneFadeIn'in karanlığı bitsin diye).")]
    [SerializeField] float startDelay = 0.2f;
    [Tooltip("Bulanıklığın/vinyetin dağılma süresi.")]
    [SerializeField] float clearDuration = 1.0f;

    [Header("Şiddet")]
    [Tooltip("Başlangıç bulanıklığı (HDRP yakın-alan odak mesafesi, m). Büyük = daha bulanık.")]
    [SerializeField] float blurStart     = 8f;
    [SerializeField] float vignetteStart = 0.55f;

    [Header("Kontrol")]
    [Tooltip("Efekt boyunca oyuncu hareketi kilitli kalsın mı? (Yerden kalkıyor.)")]
    [SerializeField] bool freezePlayer = true;

    Vignette      vignette;
    DepthOfField  dof;

    void Start() => StartCoroutine(Run());

    IEnumerator Run()
    {
        BuildVolume();

        var playerGo = GameObject.FindGameObjectWithTag("Player");
        var pm       = playerGo != null ? playerGo.GetComponent<PlayerMovement>() : null;
        if (freezePlayer && pm != null) pm.enabled = false;

        SetAmount(1f);
        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

        float t = 0f;
        while (t < clearDuration)
        {
            t += Time.deltaTime;
            SetAmount(1f - Mathf.Clamp01(t / clearDuration));
            yield return null;
        }
        SetAmount(0f);

        if (freezePlayer && pm != null) pm.enabled = true;
    }

    void BuildVolume()
    {
        var vol      = gameObject.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 5;   // DamageVignette (1) üstünde kalsın

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        vol.profile = profile;

        vignette = profile.Add<Vignette>();
        vignette.active = true;
        vignette.color.Override(Color.black);
        vignette.intensity.Override(0f);

        // Yakın alan bulanıklığı: odak çok yakına alınınca tüm görüş bulanıklaşır.
        dof = profile.Add<DepthOfField>();
        dof.active = true;
        dof.focusMode.Override(DepthOfFieldMode.Manual);
        dof.nearFocusStart.Override(0f);
        dof.nearFocusEnd.Override(0f);
    }

    // amount: 1 = tam bulanık/karanlık, 0 = net
    void SetAmount(float amount)
    {
        if (vignette != null) vignette.intensity.value  = vignetteStart * amount;
        if (dof      != null) dof.nearFocusEnd.value    = blurStart     * amount;
    }
}
}

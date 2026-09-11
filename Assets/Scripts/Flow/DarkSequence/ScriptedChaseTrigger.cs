using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Bloodrush.Player;
using Bloodrush.FX;
using Bloodrush.Shared.Audio;

namespace Bloodrush.Flow
{
// ADIM 9 — "Kaçış anı". Arkada bir patlama: buhar VFX + yüksek ses + kamera sarsıntısı.
//
// GERÇEK BİR TEHLİKE YOK: bilerek hasar/ölüm sistemi kurulmadı (spec böyle istedi).
// Tamamen atmosferik bir tempo yükseltmesi — oyuncu koşar, hiçbir şey onu yakalayamaz.
//
// NOT: Spec "Cinemachine Impulse" diyordu ama projede Cinemachine YOK; sarsıntı için
// projenin kendi CameraShake singleton'ı kullanılıyor (asansör kazasında da öyle yaptık).
[RequireComponent(typeof(Collider))]
public class ScriptedChaseTrigger : MonoBehaviour
{
    [Header("Patlama")]
    [Tooltip("Aynı anda oynatılacak buhar/kıvılcım efektleri.")]
    [SerializeField] ParticleSystem[] bursts;
    [SerializeField] AudioClip explosionClip;
    [SerializeField] [Range(0f,3f)] float explosionVolume = 2f;
    [Tooltip("Sarsıntı şiddeti (m). CameraShake tavanı 0.5 — 0.3 sert bir darbedir.")]
    [SerializeField] float shakeIntensity = 0.3f;

    [Header("Söndürülecek ışıklar (opsiyonel)")]
    [Tooltip("KOLAY YOL: ışıkları tek tek sürüklemek yerine, hepsini içeren KÖK objeyi " +
             "buraya sürükle (ör. H_KacisKoridoru/SonecekIsiklar) — altındaki tüm Light'lar " +
             "otomatik toplanır.")]
    [SerializeField] Transform killLightsRoot;
    [Tooltip("Elle liste (Kill Lights Root doluysa gerek yok).")]
    [SerializeField] Light[] killLights;

    [Header("Sonrası")]
    [Tooltip("Patlamadan hemen sonra tetiklenir (kapı kapatma, ambiyans değişimi vb.).")]
    public UnityEvent onTriggered;

    bool fired;

    void Reset()
    {
        var c = GetComponent<Collider>();
        if (c) c.isTrigger = true;
    }

    void Awake()
    {
        if (killLightsRoot != null && (killLights == null || killLights.Length == 0))
            killLights = killLightsRoot.GetComponentsInChildren<Light>(true);
    }

    void OnTriggerEnter(Collider other)
    {
        if (fired) return;
        if (other.GetComponentInParent<PlayerMovement>() == null) return;

        fired = true;
        StartCoroutine(Blast());
    }

    IEnumerator Blast()
    {
        if (bursts != null)
            foreach (var p in bursts)
                if (p != null) p.Play();

        if (explosionClip != null)
            SfxPlayer.PlayAtPoint(explosionClip, transform.position, explosionVolume);

        CameraShake.Shake(shakeIntensity);

        if (killLights != null)
            foreach (var l in killLights)
                if (l != null) l.enabled = false;

        Debug.Log("[ScriptedChaseTrigger] Patlama tetiklendi.", this);
        onTriggered?.Invoke();

        // Kısa bir ikinci dalga — tek darbe yerine sarsıntının sönmesi daha inandırıcı.
        yield return new WaitForSeconds(0.35f);
        CameraShake.Shake(shakeIntensity * 0.45f);
    }
}
}

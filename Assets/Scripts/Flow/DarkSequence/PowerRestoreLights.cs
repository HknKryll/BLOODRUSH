using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Bloodrush.Flow
{
// ADIM 10 — "Güç geri geliyor": tavan ışıkları birkaç kez titreyip YARI şiddette sabitlenir.
// Tek seferlik; EmergencyLight'ın aksine sürekli döngü değil.
//
// Restore() dışarıdan çağrılır (ScriptedChaseTrigger.onFinished ya da koridor sonundaki
// trigger) — kendi başına bir tetikleyicisi yok, böylece ne zaman çalışacağına sahne karar verir.
public class PowerRestoreLights : MonoBehaviour
{
    [Header("Işıklar")]
    [Tooltip("KOLAY YOL: ışıkları tek tek sürüklemek yerine, hepsini içeren KÖK objeyi " +
             "buraya sürükle (ör. H_KacisKoridoru/GucIsigilar) — altındaki tüm Light'lar " +
             "otomatik toplanır.")]
    [SerializeField] Transform lightsRoot;
    [Tooltip("Elle liste (lightsRoot doluysa gerek yok).")]
    [SerializeField] Light[] lights;
    [Tooltip("Sabitleneceği şiddet (lümen). 'Yarı güç' hissi için normalin yarısı.")]
    [SerializeField] float finalLumen = 1100f;

    [Header("Titreme")]
    [SerializeField] int   flickerCount = 4;
    [SerializeField] Vector2 onTime  = new Vector2(0.05f, 0.14f);
    [SerializeField] Vector2 offTime = new Vector2(0.06f, 0.2f);
    [Tooltip("Son titremeden sonra tam şiddete oturma süresi.")]
    [SerializeField] float settleTime = 0.6f;

    [Header("Ses")]
    [SerializeField] AudioClip powerUpClip;
    [SerializeField] [Range(0f,2f)] float volume = 1f;

    HDAdditionalLightData[] hd;
    bool done;

    void Awake()
    {
        // Kök verildiyse listeyi ondan doldur — Inspector'da tek tek sürüklemeye gerek kalmasın.
        if (lightsRoot != null && (lights == null || lights.Length == 0))
            lights = lightsRoot.GetComponentsInChildren<Light>(true);

        if (lights == null || lights.Length == 0)
        {
            Debug.LogWarning("[PowerRestoreLights] Işık bulunamadı — Lights Root ata.", this);
            return;
        }
        hd = new HDAdditionalLightData[lights.Length];
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] == null) continue;
            hd[i] = lights[i].GetComponent<HDAdditionalLightData>();
            if (hd[i] == null) hd[i] = lights[i].gameObject.AddComponent<HDAdditionalLightData>();
            hd[i].EnableShadows(false);
            lights[i].enabled = false;          // sekans başında kapalı
        }
    }

    // UnityEvent'ten çağır.
    public void Restore()
    {
        if (done) return;
        done = true;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        if (powerUpClip != null)
            Bloodrush.Shared.Audio.SfxPlayer.PlayAtPoint(powerUpClip, transform.position, volume);

        for (int i = 0; i < flickerCount; i++)
        {
            SetAll(finalLumen, true);
            yield return new WaitForSeconds(Random.Range(onTime.x, onTime.y));
            SetAll(0f, false);
            yield return new WaitForSeconds(Random.Range(offTime.x, offTime.y));
        }

        // Sabitlenme: 0'dan finalLumen'e yumuşak çıkış
        float t = 0f;
        while (t < settleTime)
        {
            t += Time.deltaTime;
            SetAll(Mathf.Lerp(0f, finalLumen, t / settleTime), true);
            yield return null;
        }
        SetAll(finalLumen, true);
    }

    void SetAll(float lumen, bool on)
    {
        if (lights == null) return;
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] == null) continue;
            float scaled = lumen * DarkSceneExposure.LightScale;
            if (hd != null && hd[i] != null) hd[i].SetIntensity(scaled, LightUnit.Lumen);
            else                             lights[i].intensity = scaled;
            lights[i].enabled = on && lumen > 0.01f;
        }
    }
}
}

using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Bloodrush.Shared;

namespace Bloodrush.FX
{
public class DamageVignette : MonoBehaviour
{
    public static DamageVignette Instance { get; private set; }

    [SerializeField] Health playerHealth;
    [SerializeField] float  flashIntensity = 0.55f;
    [SerializeField] float  fadeSpeed      = 4f;

    Vignette            vignette;
    ChromaticAberration chrAb;
    float               target;
    float               caTarget;

    UnityEngine.UI.Image killFlashImg;
    float                killFlashAlpha;

    // Çöküş kalıcı katmanı
    float collapseVignette = 0f;
    float collapseCA       = 0f;
    bool  flickering       = false;

    void Awake()
    {
        Instance = this;

        var vol      = gameObject.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 1;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        vol.profile = profile;

        vignette = profile.Add<Vignette>();
        vignette.active = true;
        vignette.color.Override(Color.red);
        vignette.intensity.Override(0f);

        chrAb = profile.Add<ChromaticAberration>();
        chrAb.active = true;
        chrAb.intensity.Override(0f);

        // Kill flash canvas
        var cgo    = new GameObject("KillFlashCanvas");
        var canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 15;
        cgo.AddComponent<UnityEngine.UI.CanvasScaler>();
        cgo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        var igo = new GameObject("FlashImg");
        igo.transform.SetParent(cgo.transform, false);
        var rt = igo.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        killFlashImg = igo.AddComponent<UnityEngine.UI.Image>();
        killFlashImg.color         = new Color(1f, 0.15f, 0.15f, 0f);
        killFlashImg.raycastTarget = false;

        if (playerHealth == null)
            playerHealth = GameObject.FindGameObjectWithTag("Player")?.GetComponent<Health>();

        if (playerHealth)
            playerHealth.onHealthChanged.AddListener(OnHealthChanged);
    }

    void OnHealthChanged(float normalized)
    {
        target   = flashIntensity;
        caTarget = 0.45f;
    }

    void Update()
    {
        if (vignette == null) return;

        // Hasar flash — collapseVignette minimumuna kadar söner
        if (target > 0f) { vignette.intensity.value = Mathf.Max(target, collapseVignette); target = 0f; }
        vignette.intensity.value = Mathf.Lerp(vignette.intensity.value, collapseVignette, Time.deltaTime * fadeSpeed);

        // ChromaticAberration flash — collapseCA minimumuna kadar söner
        if (caTarget > 0f) { chrAb.intensity.value = Mathf.Max(caTarget, collapseCA); caTarget = 0f; }
        chrAb.intensity.value = Mathf.Lerp(chrAb.intensity.value, collapseCA, Time.deltaTime * fadeSpeed);

        killFlashAlpha = Mathf.Lerp(killFlashAlpha, 0f, Time.unscaledDeltaTime * 8f);
        if (killFlashImg) killFlashImg.color = new Color(1f, 0.15f, 0.15f, killFlashAlpha);
    }

    public static void OnKill()
    {
        if (Instance == null) return;
        Instance.killFlashAlpha = 0.35f;
        Instance.caTarget       = 0.8f;
    }

    // Uyarıcı kullanıldıkça çağrılır. level: 1=sağlıklı, 0=ölüm
    public static void SetCollapseLevel(float level)
    {
        if (Instance == null) return;
        float t = 1f - Mathf.Clamp01(level);
        Instance.collapseVignette = t * 0.55f;
        Instance.collapseCA       = t * 0.45f;

        if (level < 0.25f && !Instance.flickering)
            Instance.StartCoroutine(Instance.DoFlicker());
    }

    IEnumerator DoFlicker()
    {
        flickering = true;
        while (flickering)
        {
            yield return new WaitForSeconds(Random.Range(1.5f, 4f));
            if (vignette != null) vignette.intensity.value = 0.92f;
            yield return new WaitForSecondsRealtime(0.04f);
            if (vignette != null) vignette.intensity.value = collapseVignette;
            yield return new WaitForSecondsRealtime(0.07f);
            if (vignette != null) vignette.intensity.value = 0.92f;
            yield return new WaitForSecondsRealtime(0.04f);
            if (vignette != null) vignette.intensity.value = collapseVignette;
        }
    }
}
}

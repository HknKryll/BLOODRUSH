using System.Collections;
using UnityEngine;
using Bloodrush.Player;
using Bloodrush.Shared.Audio;

namespace Bloodrush.Flow
{
// ADIM 5 + 8 — Beliren/kaybolan statik insan silüeti. AI, NavMesh, animasyon YOK: sadece
// görünüp kaybolan bir model. Aynı component iki anı da karşılar, tek fark Inspector
// değerleri (konum, süre).
//
// İki tetikleme yolu var: kendi trigger'ına oyuncu girince, ya da dışarıdan Show() —
// böylece "ikinci sigorta alınırken belirsin" gibi anları FuseItem.onCollected'a
// bağlayabilirsin (ADIM 5).
//
// Solma için silüetin materyali TRANSPARENT olmalı (HDRP/Lit → Surface Type: Transparent).
// Opak materyalde solma olmaz, silüet birden belirir — o da korkutucudur, bozulmaz.
public class SilhouetteScare : MonoBehaviour
{
    [Header("Silüet")]
    [Tooltip("Görünecek model. Basit bir capsule yeterli. Obje AKTİF kalabilir — gizleme " +
             "renderer üzerinden yapılıyor, bu component'in kendi objesi de atanabilir.")]
    [SerializeField] GameObject silhouette;
    [SerializeField] float fadeIn  = 0.4f;
    [Tooltip("Tam görünür kaldığı süre. ADIM 5 için ~1.5, ADIM 8 için ~3.5.")]
    [SerializeField] float hold    = 1.5f;
    [SerializeField] float fadeOut = 0.6f;

    [Header("Tetikleme")]
    [Tooltip("Bu objenin trigger'ına oyuncu girince kendiliğinden çalışsın mı? " +
             "Kapatırsan sadece Show() ile (UnityEvent'ten) tetiklenir.")]
    [SerializeField] bool triggerOnEnter = true;
    [SerializeField] bool onlyOnce       = true;

    [Header("Halusinasyon: bakinca kaybolma")]
    [Tooltip("ACIK: oyuncu figure DOGRUDAN bakinca hizla solup kaybolur. " +
             "Kapaliyken davranis birebir eskisi gibi (sureli gorunup kaybolma).")]
    [SerializeField] bool  vanishOnLook  = false;
    [Tooltip("Ne kadar dogrudan bakmak sayilsin. 1 = tam merkeze, 0.93 ≈ ustune bakmak.")]
    [Range(0.5f, 1f)]
    [SerializeField] float vanishLookDot = 0.93f;
    [Tooltip("Bu kadar KESINTISIZ bakinca kaybolur (sn). Kisa tut — 'bakinca yok oldu' hissi.")]
    [SerializeField] float vanishStare   = 0.3f;
    [Tooltip("Bakisla kaybolurken solma suresi. Normal fadeOut'tan HIZLI olmali.")]
    [SerializeField] float vanishFadeOut = 0.15f;

    [Header("Ses")]
    [SerializeField] AudioClip stingClip;
    [SerializeField] [Range(0f,2f)] float stingVolume = 0.8f;

    Renderer[]            renderers;
    Collider[]            colliders;
    MaterialPropertyBlock mpb;
    bool                  played;
    Coroutine             routine;

    static readonly int BaseColorId  = Shader.PropertyToID("_BaseColor");
    static readonly int UnlitColorId = Shader.PropertyToID("_UnlitColor");

    void Awake()
    {
        if (silhouette == null) { Debug.LogWarning("[SilhouetteScare] Silüet atanmamış.", this); return; }
        renderers = silhouette.GetComponentsInChildren<Renderer>(true);
        colliders = silhouette.GetComponentsInChildren<Collider>(true);
        mpb       = new MaterialPropertyBlock();
        SetVisible(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!triggerOnEnter) return;
        if (other.GetComponentInParent<PlayerMovement>() == null) return;
        Show();
    }

    // UnityEvent'ten çağrılabilir (ör. FuseItem.onCollected).
    public void Show()
    {
        if (silhouette == null) return;
        if (onlyOnce && played) return;
        played = true;

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        SetVisible(true);
        SetAlpha(0f);

        if (stingClip != null)
            SfxPlayer.PlayAtPoint(stingClip, silhouette.transform.position, stingVolume);

        WarnIfOffScreen();

        yield return Fade(0f, 1f, fadeIn);

        // Bekleme artik tek bir WaitForSeconds degil, HER KARE yoklanan bir dongu —
        // yoksa oyuncunun bakisini bekleme suresince fark edemezdik.
        bool stared = false;
        if (hold > 0f)
        {
            float t = 0f, stare = 0f;
            var cam = Camera.main;

            while (t < hold)
            {
                t += Time.deltaTime;

                if (vanishOnLook && cam != null)
                {
                    Vector3 to  = FigureCenter() - cam.transform.position;
                    float   mag = to.magnitude;
                    float   dot = mag > 0.001f ? Vector3.Dot(cam.transform.forward, to / mag) : 1f;

                    // Bakis kesilirse sayac SIFIRLANIR: figur ancak KESINTISIZ bakista kaybolur,
                    // ekranin kenarindan surekli goz ucuyla gormek onu oldurmez.
                    stare = dot >= vanishLookDot ? stare + Time.deltaTime : 0f;
                    if (stare >= vanishStare) { stared = true; break; }
                }
                yield return null;
            }
        }

        yield return Fade(1f, 0f, stared ? vanishFadeOut : fadeOut);

        SetVisible(false);
        routine = null;
    }

    // Figur ekranin DISINDA belirdiyse soyle. "Ses geliyor ama goremiyorum" durumunun
    // neredeyse tek sebebi bu: tetikleyici bir odada, figur baska odada.
    void WarnIfOffScreen()
    {
        var cam = Camera.main;
        if (cam == null) return;

        Vector3 vp = cam.WorldToViewportPoint(FigureCenter());
        bool visible = vp.z > 0f && vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f;

        if (!visible)
            Debug.LogWarning($"[SilhouetteScare] '{name}' figuru EKRAN DISINDA belirdi " +
                              "(oyuncu ona bakmiyor ya da baska odada). Sesi duyar ama goremez — " +
                              "tetikleyiciyi figurun GORULDUGU yere tasi.", this);
    }

    // Editor'da yerlestirme kolayligi: Play'e girmeden figuru gorunur/gizli yap.
    [ContextMenu("Test: Figuru Goster")]
    void EditorShow()
    {
        CacheVisuals();
        SetVisible(true);
        SetAlpha(1f);
    }

    [ContextMenu("Test: Figuru Gizle")]
    void EditorHide()
    {
        CacheVisuals();
        SetVisible(false);
    }

    void CacheVisuals()
    {
        if (silhouette == null) { Debug.LogWarning("[SilhouetteScare] Siluet atanmamis.", this); return; }
        renderers = silhouette.GetComponentsInChildren<Renderer>(true);
        colliders = silhouette.GetComponentsInChildren<Collider>(true);
        if (mpb == null) mpb = new MaterialPropertyBlock();
    }

    // Figurun GORSEL merkezi — pivot genelde ayakta/yerde kalir, bakis olcumu oradan
    // yapilirsa oyuncu govdeye baksa bile dot tutmaz.
    Vector3 FigureCenter()
    {
        if (renderers == null || renderers.Length == 0)
            return silhouette != null ? silhouette.transform.position : transform.position;

        bool   any = false;
        Bounds b   = default;
        foreach (var r in renderers)
        {
            if (r == null) continue;
            if (!any) { b = r.bounds; any = true; }
            else       b.Encapsulate(r.bounds);
        }
        return any ? b.center
                   : (silhouette != null ? silhouette.transform.position : transform.position);
    }

    // Gizleme SetActive ile DEGIL renderer/collider ile yapiliyor.
    //
    // Neden: silhouette alanina bu component'in KENDI GameObject'i (ya da bir atasi)
    // atandiginda SetActive(false) bileseni de olduruyordu — "Coroutine couldn't be started
    // because the game object is inactive" hatasinin kaynagi buydu. Renderer gizlemesinde
    // obje hep aktif kalir, dolayisiyla silüetin ayni obje / child / ayri obje olmasi fark
    // etmez. Collider de kapatiliyor: gorunmez bir kapsule toslamak istemiyoruz.
    void SetVisible(bool visible)
    {
        if (renderers != null)
            foreach (var r in renderers)
                if (r != null) r.enabled = visible;

        if (colliders != null)
            foreach (var c in colliders)
                if (c != null) c.enabled = visible;
    }

    IEnumerator Fade(float from, float to, float seconds)
    {
        if (seconds <= 0f) { SetAlpha(to); yield break; }
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            SetAlpha(Mathf.Lerp(from, to, t / seconds));
            yield return null;
        }
        SetAlpha(to);
    }

    // HDRP'de renk özelliği shader'a göre değişiyor — PuzzleConsole'daki fallback zincirinin
    // aynısı: önce Lit (_BaseColor), sonra Unlit (_UnlitColor).
    void SetAlpha(float a)
    {
        if (renderers == null) return;
        foreach (var r in renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(mpb);

            Color c = r.sharedMaterial != null && r.sharedMaterial.HasProperty(BaseColorId)
                    ? r.sharedMaterial.GetColor(BaseColorId)
                    : Color.black;
            c.a = a;

            if (r.sharedMaterial != null && r.sharedMaterial.HasProperty(UnlitColorId))
                mpb.SetColor(UnlitColorId, c);
            else
                mpb.SetColor(BaseColorId, c);

            r.SetPropertyBlock(mpb);
        }
    }
}
}

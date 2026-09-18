using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bloodrush.Flow
{
// Alinan sigortanin oyuncunun SOL elinde gorunmesi. FuseSequenceManager'in event'lerini
// dinler; mantiga hic karismaz.
//
// Kameraya CALISMA ZAMANINDA baglanir — Player.prefab'a dokunulmaz. Sol tarafta durur: el
// feneri (0.18, -0.15) ve silah tutucusu kameranin SAG tarafinda.
//
// Model, yerden alinan sigortanin KENDI gorselinin kopyasidir: placeholder'i gercek modelle
// degistirdiginde eldeki de kendiliginden degisir. Kopyadan etkilesim script'leri ve
// collider'lar sokulur; boyutu mesh sinirlarina gore handSize'a olceklenir, yani gercek
// modelin olcegi ne olursa olsun elde ayni buyuklukte durur.
public class FuseHandView : MonoBehaviour
{
    [Header("Yerlesim (kameraya gore)")]
    [SerializeField] Vector3 handOffset = new Vector3(-0.21f, -0.16f, 0.42f);
    [SerializeField] Vector3 handEuler  = new Vector3(-15f, 0f, -30f);
    [Tooltip("Modelin en uzun kenari bu boya (metre) olceklenir.")]
    [SerializeField] float   handSize   = 0.13f;

    [Header("Animasyon")]
    [SerializeField] float raiseDuration  = 0.22f;
    [SerializeField] float insertDuration = 0.25f;
    [SerializeField] float bobAmount      = 0.004f;

    FuseSequenceManager manager;
    Transform  anchor;     // kameraya bagli, sabit ofset
    Transform  pivot;      // kalkma / itme animasyonu
    Transform  fit;        // sabit tutus acisi
    GameObject model;
    Coroutine  anim;

    Vector3 animPos;
    float   animScale = 1f;

    public void Bind(FuseSequenceManager m)
    {
        if (manager != null) Unbind();
        manager = m;
        manager.Collected += OnCollected;
        manager.Inserted  += OnInserted;
    }

    void Unbind()
    {
        manager.Collected -= OnCollected;
        manager.Inserted  -= OnInserted;
        manager = null;
    }

    void OnDestroy()
    {
        if (manager != null) Unbind();
        if (anchor  != null) Destroy(anchor.gameObject);
    }

    // ── Event'ler ──────────────────────────────────────────────────────

    void OnCollected(Transform visual)
    {
        if (!EnsureAnchor() || visual == null) return;

        if (model == null)
        {
            model = BuildCopy(visual);
            if (model == null) return;
        }

        // Zaten eldeyse (ikinci sigorta) yeni model cikmaz — sayi HUD'da. Takma animasyonu
        // suruyorsa o bitince elde kalan sigorta icin kendisi yeniden kaldirir.
        if (!model.activeSelf) Play(Raise());
    }

    void OnInserted(int _)
    {
        if (model == null || !model.activeSelf) return;
        Play(InsertThenMaybeRaise());
    }

    // ── Kurulum ────────────────────────────────────────────────────────

    bool EnsureAnchor()
    {
        if (anchor != null) return true;
        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[FuseHandView] Kamera bulunamadi — sigorta elde gosterilemiyor.", this);
            return false;
        }

        anchor = new GameObject("FuseHandAnchor").transform;
        anchor.SetParent(cam.transform, false);
        anchor.localPosition = handOffset;

        pivot = new GameObject("Pivot").transform;
        pivot.SetParent(anchor, false);

        fit = new GameObject("Fit").transform;
        fit.SetParent(pivot, false);
        fit.localRotation = Quaternion.Euler(handEuler);
        return true;
    }

    GameObject BuildCopy(Transform source)
    {
        // KAPALI bir tasiyicinin altinda kopyala: Awake/OnEnable calismadan istenmeyen
        // bilesenler sokulur, kopya bir sonraki karede kendi prompt'unu acamaz.
        var staging = new GameObject("FuseHandStaging");
        staging.SetActive(false);

        var copy = Instantiate(source.gameObject, staging.transform, false);
        copy.name = "FuseHeld";
        copy.SetActive(true);   // kaynak kapatilmis olsa bile; tasiyici kapali, Awake yine calismaz
        Strip(copy);

        var t = copy.transform;
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale    = Vector3.one;

        Bounds b;
        if (!LocalMeshBounds(t, out b))
        {
            Destroy(staging);
            Destroy(copy);
            Debug.LogWarning("[FuseHandView] Sigorta gorselinde mesh yok — elde gosterilmiyor.", this);
            return null;
        }

        float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        float s = longest > 1e-4f ? handSize / longest : 1f;

        t.SetParent(fit, false);
        t.localScale    = Vector3.one * s;
        t.localPosition = -b.center * s;   // tutus noktasi = modelin gorsel merkezi
        Destroy(staging);

        foreach (var glow in copy.GetComponentsInChildren<FuseGlow>(true)) glow.SetHeld(true);
        copy.SetActive(false);
        return copy;
    }

    static void Strip(GameObject go)
    {
        // Oyun script'leri (etkilesim, ses) gider; FuseGlow ve HDRP'nin kendi bilesenleri kalir.
        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null || mb is FuseGlow) continue;
            string ns = mb.GetType().Namespace;
            if (ns != null && ns.StartsWith("Bloodrush")) DestroyImmediate(mb);
        }
        foreach (var c  in go.GetComponentsInChildren<Collider>(true))    DestroyImmediate(c);
        foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))   DestroyImmediate(rb);
        foreach (var a  in go.GetComponentsInChildren<AudioSource>(true)) DestroyImmediate(a);
        foreach (var r  in go.GetComponentsInChildren<Renderer>(true))
            r.shadowCastingMode = ShadowCastingMode.Off;
    }

    // Kapali hiyerarsilerde Renderer.bounds guvenilir degil; mesh sinirlarini kok uzayina
    // elle tasiyoruz.
    static bool LocalMeshBounds(Transform root, out Bounds bounds)
    {
        Bounds result = default;
        bool any = false;
        Matrix4x4 toRoot = root.worldToLocalMatrix;

        void Add(Mesh mesh, Transform owner)
        {
            if (mesh == null) return;
            Bounds mb = mesh.bounds;
            Matrix4x4 m = toRoot * owner.localToWorldMatrix;
            Vector3 c = mb.center, e = mb.extents;
            for (int i = 0; i < 8; i++)
            {
                var corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x,
                                             (i & 2) == 0 ? -e.y : e.y,
                                             (i & 4) == 0 ? -e.z : e.z);
                var p = m.MultiplyPoint3x4(corner);
                if (!any) { result = new Bounds(p, Vector3.zero); any = true; }
                else       result.Encapsulate(p);
            }
        }

        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))          Add(mf.sharedMesh, mf.transform);
        foreach (var sk in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) Add(sk.sharedMesh, sk.transform);
        bounds = result;
        return any;
    }

    // ── Animasyon ──────────────────────────────────────────────────────

    void Play(IEnumerator routine)
    {
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(routine);
    }

    IEnumerator Raise()
    {
        model.SetActive(true);
        var from = new Vector3(0f, -0.22f, -0.05f);
        yield return Animate(from, Vector3.zero, 0.85f, 1f, raiseDuration);
        anim = null;
    }

    IEnumerator InsertThenMaybeRaise()
    {
        // Ileri itilip kuculerek kaybolur — "kutuya taktim" hissi.
        yield return Animate(animPos, new Vector3(0.08f, 0.04f, 0.22f), animScale, 0.35f, insertDuration);
        model.SetActive(false);

        if (manager != null && manager.HeldCount > 0)
        {
            yield return new WaitForSeconds(0.12f);
            yield return Raise();
        }
        anim = null;
    }

    IEnumerator Animate(Vector3 fromPos, Vector3 toPos, float fromScale, float toScale, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, duration > 0f ? t / duration : 1f);
            animPos   = Vector3.LerpUnclamped(fromPos, toPos, k);
            animScale = Mathf.LerpUnclamped(fromScale, toScale, k);
            yield return null;
        }
        animPos   = toPos;
        animScale = toScale;
    }

    void LateUpdate()
    {
        if (pivot == null || model == null || !model.activeSelf) return;
        float tm = Time.time;
        var bob = new Vector3(Mathf.Sin(tm * 1.3f), Mathf.Sin(tm * 2.1f), 0f) * bobAmount;
        pivot.localPosition = animPos + bob;
        pivot.localScale    = Vector3.one * animScale;
    }
}
}

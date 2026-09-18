using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.HighDefinition;
using Bloodrush.FX;
using Bloodrush.Player;
using Bloodrush.Shared.Audio;

namespace Bloodrush.Flow
{
// Patlamayla kapinin onune coken moloz yigini. Iki asamali:
//  1) EDITOR: dis menusunden "Moloz Yiginini Kur" — yiginin SON hali kurulur ve sahnede
//     gorunur; kapiyi kapatip kapatmadigini gozle ayarlarsin.
//  2) PLAY: parcalar gizlenir. Collapse() gelince tavandan dusup bu son hallerine oturur:
//     tavanda karanlik bosluk + sarkan kablolar, flas, toz bulutu, ses, sarsinti. Bitince
//     gorunmez bir duvar geri donusu kapatir.
//
// YERLESIM: objeyi kapinin koridor tarafina, ZEMINE koy; mavi ok (+Z) koridorun icine baksin.
// Yigin kapi duzleminden (+Z = 0) koridora dogru "Depth" kadar uzanir.
//
// TETIK: "Chase Trigger" doluysa onun onTriggered'ina, bossa sahnede TEK bir
// ScriptedChaseTrigger varsa ona kendiliginden baglanir. Baska bir seyle tetiklemek icin
// Collapse()'i bir UnityEvent'e bagla.
//
// HASAR YOK: dusen parcalarin collider'i yok; engel de oyuncu yigin alanindan cikinca acilir.
public class RubbleCollapse : MonoBehaviour
{
    const string PileName    = "Yigin";
    const string HoleName    = "TavanBoslugu";
    const string BlockerName = "GeriDonusEngeli";
    const float  BounceTime  = 0.14f;
    const float  Gravity     = 9.81f;

    [Header("Yigin boyutu (yerel: X = kapi genisligi, +Z = koridor)")]
    [SerializeField] float width  = 3.4f;
    [Tooltip("Kapiya yaslanan en yuksek noktasi.")]
    [SerializeField] float height = 2.6f;
    [Tooltip("Kapi duzleminden koridora dogru uzanma.")]
    [SerializeField] float depth  = 1.6f;
    [Tooltip("Tavan bulunamazsa kullanilan tavan yuksekligi (zeminden).")]
    [SerializeField] float fallbackCeiling = 4.5f;

    [Header("Icerik")]
    [SerializeField] int chunkCount   = 30;
    [Tooltip("Kapiya yaslanan buyuk kirik beton levhalar.")]
    [SerializeField] int slabCount    = 3;
    [SerializeField] int panelPieces  = 4;
    [SerializeField] int rebarCount   = 6;
    [SerializeField] int brokenPipes  = 1;
    [SerializeField] int hangingCables = 3;
    [Tooltip("Ayni sayi = ayni yigin. Begenmezsen degistirip yeniden kur.")]
    [SerializeField] int seed = 2024;

    [Header("Tetik")]
    [SerializeField] ScriptedChaseTrigger chaseTrigger;
    [Tooltip("Chase Trigger bossa sahnedeki TEK ScriptedChaseTrigger'a otomatik baglan.")]
    [SerializeField] bool autoBindChaseTrigger = true;

    [Header("Cokus")]
    [Tooltip("Tetikten sonra cokusun baslamasina kadar gecen sure.")]
    [SerializeField] float startDelay  = 0.15f;
    [Tooltip("Alttaki parcalar once, ustekiler sonra: en uste kadar yayilan gecikme.")]
    [SerializeField] float stagger     = 0.45f;
    [Tooltip("1 = gercek yercekimi. Biraz yuksek daha sert/agir durur.")]
    [SerializeField] float fallGravity = 1.4f;
    [Tooltip("Ilk parca yere carpinca kamera sarsintisi.")]
    [SerializeField] float landShake   = 0.18f;

    [Header("Ses")]
    [Tooltip("Cokus basladiginda (ufalanan moloz).")]
    public AudioClip collapseClip;
    [SerializeField] [Range(0f, 1f)] float collapseVolume = 1f;
    [Tooltip("Ilk parca yere carptiginda (sert darbe).")]
    public AudioClip impactClip;
    [SerializeField] [Range(0f, 1f)] float impactVolume = 1f;

    [Header("Efekt")]
    [SerializeField] bool  flash        = true;
    [SerializeField] Color flashColor   = new Color(1f, 0.58f, 0.28f);
    [Tooltip("DarkSceneExposure.LightScale ile carpilir.")]
    [SerializeField] float flashLumen   = 9000f;
    [SerializeField] float flashRange   = 14f;
    [SerializeField] float flashDuration = 0.6f;
    [SerializeField] bool  dust          = true;
    [SerializeField] Color dustColor     = new Color(0.34f, 0.32f, 0.29f, 0.55f);
    [SerializeField] int   dustParticles = 45;

    [Header("Olay")]
    [Tooltip("Yigin oturup engel kapandiginda.")]
    public UnityEvent onCollapsed;

    [Header("Materyaller (bos = duz renk uretilir)")]
    public Material concreteMat;
    public Material ceilingPanelMat;
    public Material metalMat;
    public Material cableMat;
    public Material copperMat;
    public Material sootMat;

    [SerializeField, HideInInspector] float builtCeilingY = 4.5f;

    class Piece
    {
        public Transform  t;
        public Vector3    restPos, startPos;
        public Quaternion restRot, startRot;
        public float      delay, fallTime, bounce;
    }

    Piece[]   pieces;
    Transform hole;
    Transform blocker;
    bool      collapsed;

    // ── Calisma zamani ─────────────────────────────────────────────────

    void Awake()
    {
        var pile = transform.Find(PileName);
        hole     = transform.Find(HoleName);
        blocker  = transform.Find(BlockerName);

        if (pile == null)
        {
            Debug.LogWarning("[RubbleCollapse] Yigin kurulmamis — dis menusunden 'Moloz Yiginini Kur'.", this);
            return;
        }

        pieces = new Piece[pile.childCount];
        for (int i = 0; i < pieces.Length; i++)
        {
            var t = pile.GetChild(i);
            pieces[i] = new Piece { t = t, restPos = t.localPosition, restRot = t.localRotation };
            t.gameObject.SetActive(false);
        }
        if (hole    != null) hole.gameObject.SetActive(false);
        if (blocker != null) SetBlocker(false);
    }

    void Start()
    {
        if (chaseTrigger == null && autoBindChaseTrigger)
        {
            var all = FindObjectsByType<ScriptedChaseTrigger>(FindObjectsSortMode.None);
            if (all.Length == 1) chaseTrigger = all[0];
            else if (all.Length > 1)
                Debug.LogWarning("[RubbleCollapse] Sahnede birden fazla ScriptedChaseTrigger var — " +
                                 "'Chase Trigger' alanini elle doldur.", this);
        }
        if (chaseTrigger != null) chaseTrigger.onTriggered.AddListener(Collapse);
    }

    void OnDestroy()
    {
        if (chaseTrigger != null) chaseTrigger.onTriggered.RemoveListener(Collapse);
    }

    public void Collapse()
    {
        if (collapsed || pieces == null) return;
        collapsed = true;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

        Vector3 center = transform.TransformPoint(new Vector3(0f, height * 0.5f, depth * 0.4f));
        SfxPlayer.PlayAtPoint(collapseClip, center, collapseVolume);
        if (flash) StartCoroutine(Flash(transform.TransformPoint(new Vector3(0f, builtCeilingY - 0.8f, depth * 0.4f))));
        if (dust)  SpawnDust(transform.TransformPoint(new Vector3(0f, 0.8f, depth * 0.5f)));
        if (hole != null) hole.gameObject.SetActive(true);

        // Baslangic pozlari: parcalar tavandaki bosluktan toplanip asagi dokulur.
        var prevState = Random.state;
        Random.InitState(seed ^ 0x5bd1e995);
        Vector3 origin = new Vector3(0f, 0f, depth * 0.4f);
        float   total  = 0f;
        foreach (var p in pieces)
        {
            float h01 = Mathf.Clamp01(p.restPos.y / Mathf.Max(0.01f, height));
            p.delay    = h01 * stagger + Random.Range(0f, 0.2f);
            p.startPos = Vector3.Lerp(origin, p.restPos, 0.45f);
            p.startPos.y = Mathf.Max(p.restPos.y + 0.6f, builtCeilingY - Random.Range(0.1f, 0.5f));
            p.startRot = p.restRot * Quaternion.Euler(Random.Range(-70f, 70f), Random.Range(-70f, 70f), Random.Range(-70f, 70f));
            p.fallTime = Mathf.Sqrt(2f * (p.startPos.y - p.restPos.y) / (Gravity * fallGravity));
            p.bounce   = Random.Range(0.02f, 0.07f);
            total = Mathf.Max(total, p.delay + p.fallTime + BounceTime);
        }
        Random.state = prevState;

        bool  impacted = false;
        float t = 0f;
        while (t < total)
        {
            t += Time.deltaTime;
            foreach (var p in pieces)
            {
                float lt = t - p.delay;
                if (lt < 0f) continue;
                if (!p.t.gameObject.activeSelf) p.t.gameObject.SetActive(true);

                if (lt < p.fallTime)
                {
                    float u = lt / p.fallTime;
                    Vector3 pos = Vector3.Lerp(p.startPos, p.restPos, u);
                    pos.y = p.startPos.y - 0.5f * Gravity * fallGravity * lt * lt;
                    p.t.localPosition = pos;
                    p.t.localRotation = Quaternion.Slerp(p.startRot, p.restRot, u);
                }
                else
                {
                    float bu = Mathf.Clamp01((lt - p.fallTime) / BounceTime);
                    p.t.localPosition = p.restPos + Vector3.up * (Mathf.Sin(bu * Mathf.PI) * p.bounce);
                    p.t.localRotation = p.restRot;

                    if (!impacted)
                    {
                        impacted = true;
                        SfxPlayer.PlayAtPoint(impactClip, center, impactVolume);
                        CameraShake.Shake(landShake, 0.35f);
                    }
                }
            }
            yield return null;
        }

        foreach (var p in pieces)
        {
            p.t.gameObject.SetActive(true);
            p.t.localPosition = p.restPos;
            p.t.localRotation = p.restRot;
        }

        yield return CloseWhenPlayerClear();
        Debug.Log("[RubbleCollapse] Moloz oturdu, geri donus kapandi.", this);
        onCollapsed?.Invoke();
    }

    // Engel, oyuncu icindeyken acilirsa onu firlatir ya da sikistirir — disari cikmasini bekle.
    IEnumerator CloseWhenPlayerClear()
    {
        if (blocker == null) yield break;
        var box = blocker.GetComponent<BoxCollider>();
        if (box == null) yield break;

        bool warned = false;
        while (PlayerInside(box))
        {
            if (!warned)
            {
                Debug.Log("[RubbleCollapse] Oyuncu yigin alaninda — cikinca engel kapanacak.", this);
                warned = true;
            }
            yield return new WaitForSeconds(0.2f);
        }
        SetBlocker(true);
    }

    static bool PlayerInside(BoxCollider box)
    {
        var tr = box.transform;
        Vector3 half = Vector3.Scale(box.size * 0.5f, tr.lossyScale);
        var hits = Physics.OverlapBox(tr.TransformPoint(box.center), half, tr.rotation, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
            if (h.GetComponentInParent<PlayerMovement>() != null) return true;
        return false;
    }

    void SetBlocker(bool on)
    {
        foreach (var c in blocker.GetComponents<Collider>()) c.enabled = on;
    }

    IEnumerator Flash(Vector3 worldPos)
    {
        var go = new GameObject("PatlamaFlasi");
        go.transform.position = worldPos;
        var l = go.AddComponent<Light>();
        l.type  = LightType.Point;
        l.color = flashColor;
        l.range = flashRange;
        var hd = go.GetComponent<HDAdditionalLightData>();
        if (hd == null) hd = go.AddComponent<HDAdditionalLightData>();
        hd.EnableShadows(false);

        float t = 0f;
        while (t < flashDuration)
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(t / flashDuration);
            hd.SetIntensity(flashLumen * k * k * DarkSceneExposure.LightScale, LightUnit.Lumen);
            yield return null;
        }
        Destroy(go);
    }

    void SpawnDust(Vector3 worldPos)
    {
        var go = new GameObject("MolozTozu");
        go.transform.SetPositionAndRotation(worldPos, transform.rotation);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop            = false;
        main.playOnAwake     = false;
        main.duration        = 1f;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.3f, 1.8f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.8f, 2f);
        main.startRotation   = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor      = dustColor;
        main.gravityModifier = -0.01f;   // cok hafif yukselir
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 300;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f,    (short)dustParticles),
            new ParticleSystem.Burst(0.35f, (short)(dustParticles / 2)),
        });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale     = new Vector3(width, 1f, depth);

        var colorLife = ps.colorOverLifetime;
        colorLife.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        colorLife.color = g;

        var sizeLife = ps.sizeOverLifetime;
        sizeLife.enabled = true;
        sizeLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.6f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        var mat = SoftDotVFX.CreateMaterial(Color.white);
        renderer.material = mat;

        ps.Play();
        Destroy(mat, 7f);
        Destroy(go, 7f);
    }

    // ── Editor kurulumu ────────────────────────────────────────────────

    [ContextMenu("Moloz Yiginini Kur")]
    void Build()
    {
        foreach (var n in new[] { PileName, HoleName, BlockerName })
        {
            var old = transform.Find(n);
            if (old != null) DestroyImmediate(old.gameObject);
        }

        EnsureMaterials();
        Physics.SyncTransforms();
        builtCeilingY = ProbeCeiling(out float c) ? c : fallbackCeiling;

        var prevState = Random.state;
        Random.InitState(seed);

        var pile = Group(PileName, transform, Vector3.zero);
        BuildSlabs(pile);
        BuildChunks(pile);
        BuildPanels(pile);
        BuildRebar(pile);
        BuildPipes(pile);

        hole = Group(HoleName, transform, Vector3.zero);
        Prim("Bosluk", PrimitiveType.Cube, hole, new Vector3(0f, builtCeilingY - 0.006f, depth * 0.4f),
             new Vector3(width * 0.85f, 0.01f, depth), Quaternion.identity, sootMat);
        for (int i = 0; i < hangingCables; i++)
            Cable(i, hole, new Vector3(Random.Range(-0.4f, 0.4f) * width, builtCeilingY, Random.Range(0f, 0.8f) * depth));

        var block = new GameObject(BlockerName);
        block.transform.SetParent(transform, false);
        var box = block.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, builtCeilingY * 0.5f, depth * 0.35f);
        box.size   = new Vector3(width, builtCeilingY, depth * 0.7f);

        Random.state = prevState;
        Debug.Log($"[RubbleCollapse] Yigin kuruldu: {pile.childCount} parca, tavan {builtCeilingY:0.00} m. " +
                  "Play'de gizlenir, patlamayla duser.", this);
    }

    float SurfaceHeight(float x, float z)
    {
        float zf = Mathf.Clamp01(z / Mathf.Max(0.01f, depth));
        float xf = Mathf.Clamp01(Mathf.Abs(x) / Mathf.Max(0.01f, width * 0.5f));
        return height * Mathf.Pow(1f - zf, 0.8f) * (1f - 0.3f * xf * xf);
    }

    void BuildSlabs(Transform pile)
    {
        // Kapiya yaslanmis kirik tavan dosemesi: ust kenar (-Z) kapiya, alt kenar koridora.
        for (int i = 0; i < slabCount; i++)
        {
            float len  = Random.Range(1.4f, 2.1f);
            float wid  = Random.Range(0.8f, 1.3f);
            float th   = Random.Range(0.12f, 0.2f);
            float tilt = Random.Range(52f, 72f);
            float x    = Random.Range(-0.3f, 0.3f) * width;
            float z    = Mathf.Cos(tilt * Mathf.Deg2Rad) * len * 0.5f + Random.Range(0f, 0.2f);
            var rot = Quaternion.Euler(0f, Random.Range(-15f, 15f), 0f) *
                      Quaternion.Euler(tilt, 0f, Random.Range(-10f, 10f));
            Prim($"Levha_{i}", PrimitiveType.Cube, pile,
                 new Vector3(x, Mathf.Sin(tilt * Mathf.Deg2Rad) * len * 0.5f + th * 0.3f, z),
                 new Vector3(wid, th, len), rot, concreteMat);
        }
    }

    void BuildChunks(Transform pile)
    {
        for (int i = 0; i < chunkCount; i++)
        {
            float x  = Random.Range(-0.5f, 0.5f) * width;
            float zr = Random.value;
            float z  = zr * zr * depth;                        // kapiya yakin daha yogun
            float y  = SurfaceHeight(x, z) * Mathf.Sqrt(Random.value);
            float size = Mathf.Lerp(0.65f, 0.22f, y / Mathf.Max(0.01f, height)) * Random.Range(0.75f, 1.2f);

            var scale = new Vector3(size * Random.Range(0.7f, 1.3f),
                                    size * Random.Range(0.4f, 0.85f),
                                    size * Random.Range(0.7f, 1.3f));
            var rot = Quaternion.Euler(Random.Range(-35f, 35f), Random.Range(0f, 360f), Random.Range(-35f, 35f));
            Prim($"Parca_{i}", PrimitiveType.Cube, pile,
                 new Vector3(x, Mathf.Max(y, scale.y * 0.35f), z), scale, rot, concreteMat);
        }
    }

    void BuildPanels(Transform pile)
    {
        for (int i = 0; i < panelPieces; i++)
        {
            float x = Random.Range(-0.45f, 0.45f) * width;
            float z = Random.Range(0.2f, 0.95f) * depth;
            Prim($"TavanPaneli_{i}", PrimitiveType.Cube, pile,
                 new Vector3(x, SurfaceHeight(x, z) + 0.03f, z),
                 new Vector3(Random.Range(0.3f, 0.6f), 0.025f, Random.Range(0.3f, 0.6f)),
                 Quaternion.Euler(Random.Range(-35f, 35f), Random.Range(0f, 360f), Random.Range(-35f, 35f)),
                 ceilingPanelMat);
        }
    }

    void BuildRebar(Transform pile)
    {
        for (int i = 0; i < rebarCount; i++)
        {
            float x   = Random.Range(-0.4f, 0.4f) * width;
            float z   = Random.Range(0.1f, 0.8f) * depth;
            float len = Random.Range(0.5f, 1.1f);
            Vector3 dir = (Random.onUnitSphere + new Vector3(0f, 0.9f, 0.5f)).normalized;
            Vector3 root = new Vector3(x, SurfaceHeight(x, z) * 0.8f, z);
            Prim($"FilizDemiri_{i}", PrimitiveType.Cylinder, pile, root + dir * (len * 0.4f),
                 new Vector3(0.022f, len * 0.5f, 0.022f), Quaternion.FromToRotation(Vector3.up, dir), metalMat);
        }
    }

    void BuildPipes(Transform pile)
    {
        for (int i = 0; i < brokenPipes; i++)
        {
            float len = Random.Range(1.8f, 2.6f);
            float z   = Random.Range(0.35f, 0.6f) * depth;
            var rot = Quaternion.Euler(0f, Random.Range(-40f, 40f), 0f) *
                      Quaternion.Euler(0f, 0f, 90f + Random.Range(-18f, 18f));   // yana yatik, hafif egik
            Prim($"KirikBoru_{i}", PrimitiveType.Cylinder, pile,
                 new Vector3(Random.Range(-0.2f, 0.2f) * width, SurfaceHeight(0f, z) * 0.55f, z),
                 new Vector3(0.14f, len * 0.5f, 0.14f), rot, metalMat);
        }
    }

    void Cable(int index, Transform parent, Vector3 top)
    {
        var cable = Group($"KopukKablo_{index}", parent, top);
        float   total = Random.Range(0.6f, 1.6f);
        int     segs  = Random.Range(2, 4);
        Vector3 p     = Vector3.zero;
        Vector3 dir   = Vector3.down;
        for (int s = 0; s < segs; s++)
        {
            float len = total / segs;
            Vector2 w = Random.insideUnitCircle * 0.3f;
            dir = (dir + new Vector3(w.x, 0f, w.y) * 0.6f).normalized;
            if (dir.y > -0.6f) dir = new Vector3(dir.x, -0.6f, dir.z).normalized;
            Prim($"Parca_{s}", PrimitiveType.Cylinder, cable, p + dir * (len * 0.5f),
                 new Vector3(0.025f, len * 0.5f, 0.025f), Quaternion.FromToRotation(Vector3.up, dir), cableMat);
            p += dir * len;
        }
        Prim("BakirUc", PrimitiveType.Cylinder, cable, p + dir * 0.02f,
             new Vector3(0.012f, 0.025f, 0.012f), Quaternion.FromToRotation(Vector3.up, dir), copperMat);
    }

    static Transform Group(string name, Transform parent, Vector3 localPos)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = localPos;
        return t;
    }

    static void Prim(string name, PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale,
                     Quaternion rot, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = rot;
        go.transform.localScale    = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        var col = go.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);   // carpisma tek bir engel kutusunda
    }

    // Kapi lentosuna degil gercek tavana carpsin diye yiginin ortasindan yukari isin.
    bool ProbeCeiling(out float localY)
    {
        localY = 0f;
        Vector3 origin = transform.TransformPoint(new Vector3(0f, 0.3f, depth * 0.6f));
        var hits = Physics.RaycastAll(origin, transform.up, 12f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        bool  any  = false;
        foreach (var h in hits)
        {
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (h.distance < best) { best = h.distance; localY = transform.InverseTransformPoint(h.point).y; any = true; }
        }
        return any && localY > 1f;
    }

    void EnsureMaterials()
    {
        if (concreteMat     == null) concreteMat     = Lit(new Color(0.42f, 0.41f, 0.39f), 0f,   0.15f);
        if (ceilingPanelMat == null) ceilingPanelMat = Lit(new Color(0.72f, 0.71f, 0.67f), 0f,   0.2f);
        if (metalMat        == null) metalMat        = Lit(new Color(0.30f, 0.31f, 0.33f), 0.8f, 0.35f);
        if (cableMat        == null) cableMat        = Lit(new Color(0.04f, 0.04f, 0.045f), 0f,  0.4f);
        if (copperMat       == null) copperMat       = Lit(new Color(0.85f, 0.45f, 0.25f), 1f,   0.5f);
        if (sootMat         == null) sootMat         = Lit(new Color(0.02f, 0.018f, 0.016f), 0f, 0f);
    }

    static Material Lit(Color c, float metallic, float smoothness)
    {
        var m = new Material(Shader.Find("HDRP/Lit"));
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smoothness);
        return m;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color  = new Color(1f, 0.45f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(new Vector3(0f, height * 0.5f, depth * 0.5f), new Vector3(width, height, depth));
        Gizmos.DrawLine(new Vector3(0f, 0.05f, 0f), new Vector3(0f, 0.05f, depth + 0.8f));   // koridor yonu
    }
}
}

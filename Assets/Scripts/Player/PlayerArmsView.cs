using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Bloodrush.Player
{
// Birinci sahis yumruk gorunumu (ULTRAKILL tarzi): kol normalde gorunmez; F ile yumruk ya da
// parry atilinca asagidan ekrana girer, jab animasyonunu hizli oynatir ve geri cikar.
// Sadece SUNUM: PlayerParry'nin Punched/Parried event'lerini dinler, hasar/parry mantigina dokunmaz.
//
// Animator Controller YOK: klipler bir PlayableGraph ile elle oynatilir (zaman ve hiz kodda).
// Model FBX'inin Unity'ye hangi eksen/olcekle girdigi bilinmedigi icin kol, kemiklerden olculerek
// kameraya hizalanir (Calibrate) — FBX import ayari degisse de ekranda ayni yerde durur.
public class PlayerArmsView : MonoBehaviour
{
    PlayerParry      parry;
    PlayerShoot      shoot;
    PlayerArmsConfig config;

    Transform           cam, anchor, rig;
    Animator            animator;
    SkinnedMeshRenderer skin;
    Transform           shoulderL, shoulderR;
    AnimationClip[]     clips;

    PlayableGraph         graph;
    AnimationClipPlayable current;

    bool  ready, playing, nextRight;
    float elapsed, speed, visibleDuration;
    int   hideSide;   // 0 = iki kol, -1 = sol gizli, +1 = sag gizli
    Material activeMaterial;

    public void Bind(PlayerParry p)
    {
        if (parry != null) { parry.Punched -= OnPunched; parry.Parried -= OnParried; }
        parry = p;
        parry.Punched += OnPunched;
        parry.Parried += OnParried;
    }

    void Start()
    {
        config = PlayerArmsConfig.Load();
        shoot  = GetComponentInChildren<PlayerShoot>(true);

        var camera = GetComponentInChildren<Camera>(true);
        if (camera == null) camera = Camera.main;
        if (camera == null) { Debug.LogWarning("[PlayerArmsView] Kamera yok — kol gorunumu kapali.", this); return; }
        cam = camera.transform;

        var prefab = Resources.Load<GameObject>(config.modelPath);
        clips = Resources.LoadAll<AnimationClip>(config.modelPath);
        if (prefab == null || clips == null || clips.Length == 0)
        {
            Debug.LogWarning($"[PlayerArmsView] Resources/{config.modelPath} modeli ya da klipleri yok.", this);
            return;
        }

        anchor = new GameObject("PlayerArmsAnchor").transform;
        anchor.SetParent(cam, false);
        anchor.localPosition = config.viewOffset;

        rig = Instantiate(prefab, anchor, false).transform;
        rig.name = "PlayerArms";
        foreach (var c in rig.GetComponentsInChildren<Collider>(true)) Destroy(c);

        animator = rig.GetComponentInChildren<Animator>(true);
        if (animator == null) animator = rig.gameObject.AddComponent<Animator>();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        skin = rig.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (skin != null)
        {
            skin.shadowCastingMode   = UnityEngine.Rendering.ShadowCastingMode.Off;
            skin.updateWhenOffscreen = true;   // kameraya cok yakin: sinir kutusu yuzunden kaybolmasin
        }
        shoulderL = FindBone("shoulder.L");
        shoulderR = FindBone("shoulder.R");

        graph = PlayableGraph.Create("PlayerArms");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);   // zamani biz suruyoruz
        AnimationPlayableOutput.Create(graph, "Arms", animator);

        Calibrate();
        SetVisible(false);
        ready = true;
    }

    void OnDestroy()
    {
        if (parry != null) { parry.Punched -= OnPunched; parry.Parried -= OnParried; }
        if (graph.IsValid()) graph.Destroy();
        if (anchor != null) Destroy(anchor.gameObject);
    }

    // ── Event'ler ──────────────────────────────────────────────────────

    void OnPunched(bool hit) => Play(config.punchClip, config.playbackSpeed);
    void OnParried()         => Play(config.parryClip, config.parrySpeed);

    void Play(string baseName, float playSpeed)
    {
        if (!ready) return;

        bool armed = shoot != null && shoot.isActiveAndEnabled && !shoot.WeaponsHidden && PlayerLoadout.AnyFirearm;
        bool right;
        if (armed && config.leftArmWhenArmed) { right = false; hideSide = +1; }
        else if (config.alternateWhenUnarmed) { right = nextRight; nextRight = !nextRight; hideSide = 0; }
        else                                  { right = false; hideSide = 0; }

        PlayWhoosh();

        var clip = FindClip(baseName + (right ? ".R" : ".L")) ?? FindClip(baseName);
        if (clip == null)
        {
            Debug.LogWarning($"[PlayerArmsView] '{baseName}' klibi bulunamadi.", this);
            return;
        }

        SetClip(clip);
        speed = Mathf.Max(0.05f, playSpeed);
        visibleDuration = Mathf.Max(0.05f, (clip.length - config.clipStartTime) / speed);

        // Zaten ekrandaysa (art arda basis) asagidan yeniden girmesin, kaldigi yerden yumruklasin.
        elapsed = playing ? config.slideInTime : 0f;
        playing = true;
        SetVisible(true);
        Evaluate();
    }

    AudioSource whooshSource;

    // SfxPlayer pitch vermiyor; ayni savurma sesi her basista birebir ayni duyulmasin diye kendi kaynagi.
    void PlayWhoosh()
    {
        if (config.whooshClip == null) return;
        if (whooshSource == null)
        {
            whooshSource = gameObject.AddComponent<AudioSource>();
            whooshSource.playOnAwake  = false;
            whooshSource.spatialBlend = 0f;
            whooshSource.outputAudioMixerGroup = Bloodrush.Shared.Audio.AudioRouting.Sfx;
        }
        whooshSource.pitch = Random.Range(config.whooshPitch.x, config.whooshPitch.y);
        whooshSource.PlayOneShot(config.whooshClip, config.whooshVolume);
    }

    // ── Kare guncellemesi ──────────────────────────────────────────────

    void LateUpdate()
    {
        if (!ready) return;
        if (DesiredMaterial() != activeMaterial) ApplyMaterial();   // Use Gloves Play'de canli
        if (!playing) return;

        elapsed += Time.deltaTime;
        if (elapsed >= visibleDuration)
        {
            playing = false;
            SetVisible(false);
            return;
        }
        Evaluate();
    }

    void Evaluate()
    {
        float clipTime = config.clipStartTime + elapsed * speed;
        current.SetTime(clipTime);
        graph.Evaluate(0f);

        // Kullanilmayan kol: omuz kemigi sifira olceklenince o kolun tum koseleri omuzda toplanir.
        if (shoulderL != null) shoulderL.localScale = hideSide == -1 ? Vector3.zero : Vector3.one;
        if (shoulderR != null) shoulderR.localScale = hideSide == +1 ? Vector3.zero : Vector3.one;

        // Asagidan giris / cikis.
        float slide = 0f;
        if (elapsed < config.slideInTime)
            slide = 1f - Mathf.SmoothStep(0f, 1f, elapsed / Mathf.Max(0.001f, config.slideInTime));
        else if (elapsed > visibleDuration - config.slideOutTime)
            slide = Mathf.SmoothStep(0f, 1f, (elapsed - (visibleDuration - config.slideOutTime)) / Mathf.Max(0.001f, config.slideOutTime));
        anchor.localPosition = config.viewOffset + Vector3.down * (slide * config.slideDistance);
    }

    // ── Kurulum yardimcilari ───────────────────────────────────────────

    // Kemiklerden eksen ve olcek olcer: ayak->ust kol = yukari, sol->sag ust kol = sag.
    // Unity FBX donusumleri her zaman 90 derecenin katlari oldugu icin eksenler en yakin ana
    // eksene yapistirilir.
    void Calibrate()
    {
        var restClip = FindClip("rest") ?? FindClip(config.punchClip + ".L");
        if (restClip != null) { SetClip(restClip); current.SetTime(0f); graph.Evaluate(0f); }

        Transform ul = FindBone("upper_arm.L"), ur = FindBone("upper_arm.R");
        if (ul == null || ur == null)
        {
            Debug.LogWarning("[PlayerArmsView] upper_arm kemikleri bulunamadi — hizalama varsayilan.", this);
            rig.localPosition = Vector3.down * config.eyeHeight;
            return;
        }

        Vector3 pl = rig.InverseTransformPoint(ul.position);
        Vector3 pr = rig.InverseTransformPoint(ur.position);
        Vector3 up    = SnapAxis((pl + pr) * 0.5f);
        Vector3 right = SnapAxis(pr - pl);
        Vector3 fwd   = Vector3.Cross(right, up);
        float unitsPerMeter = Vector3.Distance(pl, pr) / Mathf.Max(0.001f, config.referenceArmWidth);

        Quaternion q = Quaternion.Inverse(Quaternion.LookRotation(fwd, up));
        rig.localRotation = q;
        rig.localScale    = Vector3.one / Mathf.Max(0.0001f, unitsPerMeter);
        rig.localPosition = -(q * (up * config.eyeHeight));   // modelin goz noktasi = kamera

        Debug.Log($"[PlayerArmsView] Hizalandi — ileri {fwd}, yukari {up}, olcek {1f / unitsPerMeter:0.###}, " +
                  $"{clips.Length} klip.", this);
    }

    static Vector3 SnapAxis(Vector3 v)
    {
        Vector3 a = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        if (a.x >= a.y && a.x >= a.z) return new Vector3(Mathf.Sign(v.x), 0f, 0f);
        if (a.y >= a.z)               return new Vector3(0f, Mathf.Sign(v.y), 0f);
        return new Vector3(0f, 0f, Mathf.Sign(v.z));
    }

    void SetClip(AnimationClip clip)
    {
        if (current.IsValid() && current.GetAnimationClip() == clip) return;
        if (current.IsValid()) current.Destroy();
        current = AnimationClipPlayable.Create(graph, clip);
        current.SetApplyFootIK(false);
        graph.GetOutput(0).SetSourcePlayable(current);
    }

    // Blender FBX'i klipleri "Nesne|aksiyon" diye adlandirabiliyor: tam ad ya da "|ad" sonu.
    AnimationClip FindClip(string clipName)
    {
        foreach (var c in clips)
        {
            if (c == null || c.name.StartsWith("__preview__")) continue;
            if (c.name == clipName || c.name.EndsWith("|" + clipName)) return c;
        }
        return null;
    }

    Transform FindBone(string boneName)
    {
        foreach (var t in rig.GetComponentsInChildren<Transform>(true))
            if (t.name == boneName) return t;
        return null;
    }

    void SetVisible(bool visible)
    {
        if (skin == null) return;
        if (visible) ApplyMaterial();
        skin.enabled = visible;
    }

    Material DesiredMaterial() =>
        config.useGloves && config.gloveMaterial != null ? config.gloveMaterial : config.bareMaterial;

    void ApplyMaterial()
    {
        var mat = DesiredMaterial();
        activeMaterial = mat;
        if (mat == null || skin == null) return;

        if (config.pointFilter && mat.HasProperty("_BaseColorMap"))
        {
            var tex = mat.GetTexture("_BaseColorMap");
            if (tex != null) tex.filterMode = FilterMode.Point;
        }
        var mats = skin.sharedMaterials;
        for (int i = 0; i < mats.Length; i++) mats[i] = mat;
        skin.sharedMaterials = mats;
    }
}
}

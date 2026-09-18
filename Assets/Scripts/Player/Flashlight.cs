using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using Bloodrush.Shared.Audio;
using Bloodrush.UI;

namespace Bloodrush.Player
{
// El feneri. Oyuncu köküne ekle, sonra dişli menüsünden "El Fenerini Kur" — kamerayı bulur,
// spot ışığını kurar ve Editor'da GÖRÜNÜR hale getirir (koniyi Scene view'da ayarlayabilirsin).
// Kurmayı unutursan Play'de kendisi kurar, ama o zaman ancak oyun içinde görürsün.
//
// SİLAH DEĞİL: PlayerLoadout'un silahsızlaştırmasından etkilenmez. Asansör kazasından sonra
// oyuncunun elinde hiçbir şey yokken bile fener çalışır — karanlık sekansın oynanabilir
// olmasının tek şartı bu.
//
// Işık kameranın child'ı olduğu için CameraShake'in sarsıntısını miras alır. Ayrıca hafif bir
// GECİKME ile kamerayı takip eder: birebir kilitli ışık "kafaya vidalanmış" gibi durur.
public class Flashlight : MonoBehaviour
{
    [Header("Tuş")]
    [SerializeField] KeyCode toggleKey = KeyCode.F;
    [Tooltip("Sahne başında açık mı gelsin?")]
    [SerializeField] bool startOn = true;

    [Header("Işık")]
    [Tooltip("Boşsa 'El Fenerini Kur' ile ya da Play'de otomatik kurulur.")]
    [SerializeField] Light lamp;
    [SerializeField] float lumen     = 2600f;
    [SerializeField] float range     = 40f;
    [Tooltip("Koni açısı (derece). Dar = daha odaklı, korku hissi daha yüksek.")]
    [Range(5f, 120f)]
    [SerializeField] float spotAngle = 62f;
    [Tooltip("Koninin sert çekirdek oranı (%). HDRP bunu YÜZDE olarak ister, derece olarak " +
             "değil. Küçük = daha yumuşak kenar.")]
    [Range(0f, 100f)]
    [SerializeField] float innerPercent = 40f;
    [SerializeField] Color color = new Color(1f, 0.96f, 0.88f);
    [Tooltip("Gölge düşürsün mü? Atmosfer için çok iyi ama pahalı.")]
    [SerializeField] bool  castShadows = true;
    [Tooltip("KAPALI: isik menzili boyunca SONMEDEN gider — koridorun sonu da los da olsa " +
             "gorunur, gercek el feneri hissi budur. ACIK: HDRP'nin fiziksel mesafe-karesi " +
             "sonumu, birkac metre otesi kararir.")]
    [SerializeField] bool  rangeAttenuation = false;
    [Tooltip("Işığın kameraya göre yeri — göz hizasından biraz sağ/aşağı doğal durur.")]
    [SerializeField] Vector3 localOffset = new Vector3(0.18f, -0.15f, 0.1f);

    [Header("Uzak huzme (ikinci isik)")]
    [Tooltip("ACIK: dar ve guclu ikinci bir spot eklenir. Tek isikla hem yakini yumusak " +
             "aydinlatip hem uzagi gostermek mumkun degil — HDRP mesafenin KARESIYLE sonumluyor, " +
             "koniyi genisletmek de ayni lumeni daha genis alana yayip menzili kisaltiyor. " +
             "Gercek fenerlerde de parlak bir cekirdek + zayif bir sacilim vardir.")]
    [SerializeField] bool  enableReachBeam = false;
    [Tooltip("Dar cekirdek acisi (derece).")]
    [Range(4f, 40f)]
    [SerializeField] float reachAngle = 17f;
    [Tooltip("Cekirdegin siddeti. Genis konidekinden COK daha yuksek olmali.")]
    [SerializeField] float reachLumen = 22000f;
    [SerializeField] float reachRange = 45f;

    [Header("El hissi")]
    [Tooltip("Kamerayı takip gecikmesi. 0 = kafaya kilitli, 12-18 doğal, düşük değer savruk.")]
    [SerializeField] float followLag = 14f;

    [Header("Yakın mesafe kısma")]
    [Tooltip("AÇIK: fener yakındaki bir yüzeye vurunca gücü mesafenin karesiyle kısılır. Işık " +
             "mesafenin karesiyle arttığı için 'Full Distance'tan yakın her yüzey o mesafedeki kadar " +
             "aydınlanır — sabit pozlamalı karanlık sahnede yakındaki kutu/masa beyaza patlamaz, " +
             "uzağın görünüşü değişmez.")]
    [SerializeField] bool  nearDimming = true;
    [Tooltip("Bu mesafe ve ötesinde fener tam güçte (m). Gezinirken duvarların doğru göründüğü mesafe; " +
             "yakın yüzey hâlâ parlıyorsa ARTIR.")]
    [SerializeField] float nearFullDistance = 4f;
    [Tooltip("Kısmanın alt sınırı — burnunu duvara dayayınca fener tamamen sönmesin.")]
    [Range(0.005f, 1f)]
    [SerializeField] float nearMinScale = 0.03f;
    [Tooltip("Kısmanın değişim hızı. Yüksek = anında; düşük = göz alışıyormuş gibi yumuşak.")]
    [SerializeField] float nearDimSpeed = 10f;

    [Header("Ses")]
    [SerializeField] AudioClip clickClip;
    [SerializeField] [Range(0f, 2f)] float clickVolume = 0.7f;

    Light     reachLamp;
    Transform camTr;
    SfxPlayer sfx;
    bool      isOn;

    public bool IsOn => isOn;

    // ── Editor kurulumu ────────────────────────────────────────────────

    [ContextMenu("El Fenerini Kur")]
    void SetupInEditor()
    {
        var cam = FindCamera();
        if (cam == null)
        {
            Debug.LogError("[Flashlight] Kamera bulunamadı. Bu component'i OYUNCU köküne ekle " +
                            "(kamera onun child'ı olmalı) ya da kameranı MainCamera tag'le.", this);
            return;
        }

        if (lamp == null)
        {
            var existing = cam.Find("ElFeneri");
            lamp = existing != null ? existing.GetComponent<Light>() : CreateLamp(cam, "ElFeneri");
        }
        EnsureReachLamp(cam);

        ApplyLightSettings();
        lamp.enabled = true;
        if (reachLamp != null) reachLamp.enabled = enableReachBeam;
        Debug.Log("[Flashlight] Kuruldu — kameranın altında 'ElFeneri' objesi.", lamp);
    }

    Transform FindCamera()
    {
        var c = GetComponentInChildren<Camera>(true);
        if (c != null) return c.transform;
        return Camera.main != null ? Camera.main.transform : null;
    }

    void EnsureReachLamp(Transform cam)
    {
        if (!enableReachBeam)
        {
            // Daha once kurulmus bir huzme objesi sahnede kalmis olabilir — kapat.
            var stale = cam.Find("ElFeneri_Huzme");
            if (stale != null)
            {
                var sl = stale.GetComponent<Light>();
                if (sl != null) sl.enabled = false;
            }
            return;
        }
        if (reachLamp != null) return;
        var existing = cam.Find("ElFeneri_Huzme");
        reachLamp = existing != null ? existing.GetComponent<Light>()
                                     : CreateLamp(cam, "ElFeneri_Huzme");
    }

    Light CreateLamp(Transform cam, string goName)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(cam, false);
        go.transform.localPosition = localOffset;
        go.transform.localRotation = Quaternion.identity;

        var l  = go.AddComponent<Light>();
        l.type = LightType.Spot;
        return l;
    }

    void ApplyOne(Light l, float angle, float inner, float lm, float rng)
    {
        if (l == null) return;
        l.type      = LightType.Spot;
        l.color     = color;
        l.range     = rng;
        l.spotAngle = angle;
        l.transform.localPosition = localOffset;

        var hd = l.GetComponent<HDAdditionalLightData>();
        if (hd == null) hd = l.gameObject.AddComponent<HDAdditionalLightData>();
        hd.SetIntensity(lm * Bloodrush.Flow.DarkSceneExposure.LightScale, LightUnit.Lumen);
        hd.EnableShadows(castShadows);
        hd.SetSpotAngle(angle, inner);
        hd.applyRangeAttenuation = rangeAttenuation;
        hd.lightDimmer      = 1f;
        hd.volumetricDimmer = 1f;
        hd.affectDiffuse    = true;
        hd.affectSpecular   = true;
    }

    void ApplyLightSettings()
    {
        ApplyOne(lamp, spotAngle, innerPercent, lumen, range);
        // Cekirdek gölge DUSURMEZ: iki spot birden golge atinca hem pahali hem de
        // kenarlarda cift golge cizgisi olusuyor.
        if (reachLamp != null)
        {
            // inner 0 = merkezden kenara yumusak gecis. 80 verince ici sert bir daire olarak
            // goruluyordu (kullanici bunu bildirdi) — iki koninin sinirini belli ediyordu.
            ApplyOne(reachLamp, reachAngle, 0f, reachLumen, reachRange);
            var rhd = reachLamp.GetComponent<HDAdditionalLightData>();
            if (rhd != null) rhd.EnableShadows(false);
        }
    }

    // ── Çalışma zamanı ─────────────────────────────────────────────────

    void Start()
    {
        camTr = FindCamera();
        if (camTr == null)
        {
            Debug.LogError("[Flashlight] Oyuncu kamerası bulunamadı — fener çalışmayacak.", this);
            enabled = false;
            return;
        }

        sfx = SfxPlayer.CreateOrGet(gameObject, spatialBlend: 0f);

        if (lamp == null)
        {
            var existing = camTr.Find("ElFeneri");
            lamp = existing != null ? existing.GetComponent<Light>() : CreateLamp(camTr, "ElFeneri");
            Debug.Log("[Flashlight] Işık Play sırasında kuruldu. Editor'da görmek için " +
                      "dişli menüsünden 'El Fenerini Kur' çalıştırabilirsin.", this);
        }

        EnsureReachLamp(camTr);
        ApplyLightSettings();

        isOn = startOn;
        lamp.enabled = isOn;
        if (reachLamp != null) reachLamp.enabled = isOn && enableReachBeam;

        LogState("Start");
    }

    // Neyin ters gittigini tek bakista gostermek icin. Dis menusunden de cagrilabilir.
    [ContextMenu("Fener Durumunu Yazdir")]
    public void LogState(string where = "Manuel")
    {
        if (lamp == null)
        {
            Debug.LogError($"[Flashlight/{where}] lamp NULL — isik hic kurulmamis.", this);
            return;
        }

        var hd = lamp.GetComponent<HDAdditionalLightData>();
        Debug.Log(
            $"[Flashlight/{where}] " +
            $"acik={isOn} lamp.enabled={lamp.enabled} " +
            $"objeAktif={lamp.gameObject.activeInHierarchy} " +
            $"tip={lamp.type} aci={lamp.spotAngle:0} menzil={lamp.range:0} " +
            $"lumen={(hd != null ? hd.intensity.ToString("0") : "HD YOK")} " +
            $"parent={(lamp.transform.parent != null ? lamp.transform.parent.name : "YOK")} " +
            $"dunyaKonum={lamp.transform.position} " +
            $"tus={toggleKey}", lamp);
    }

    void Update()
    {
        if (lamp == null) return;

        if (KeyBindings.DownKey(toggleKey)) Toggle();

        UpdateNearDimming();

        // Gecikmeli takip: fener kameranın bakışına yumuşakça yetişir.
        if (followLag > 0f && camTr != null)
        {
            float k = 1f - Mathf.Exp(-followLag * Time.deltaTime);
            if (lamp.transform.parent == camTr)
                lamp.transform.localRotation = Quaternion.Slerp(lamp.transform.localRotation, Quaternion.identity, k);
            if (reachLamp != null && reachLamp.transform.parent == camTr)
                reachLamp.transform.localRotation = Quaternion.Slerp(reachLamp.transform.localRotation, Quaternion.identity, k);
        }
    }

    public void Toggle() => SetOn(!isOn);

    // ── Yakın mesafe kısma ─────────────────────────────────────────────
    //
    // Reflektörlü spot tüm lümeni dar koniye topluyor; x17 ışık çarpanıyla 1.5 m'deki açık renkli
    // bir yüzey beyazın onlarca katına çıkıyordu. Otomatik pozlama bunu çözemedi: ekranın çoğu
    // siyah olduğu için ölçüm hep "karanlık" diyordu. Burada fenerin kendisi, baktığı yüzeyin
    // mesafesine göre kısılır — pozlamaya ve diğer ışıklara dokunulmaz.

    static readonly Vector2[] ProbeDirs =
    {
        Vector2.zero, new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f),
    };

    float nearScale = 1f;
    HDAdditionalLightData lampHd, reachHd;

    void UpdateNearDimming()
    {
        float target = 1f;
        if (nearDimming && isOn && camTr != null && nearFullDistance > 0.01f)
        {
            float d = ProbeNearestHit();
            if (d < nearFullDistance)
                target = Mathf.Max(nearMinScale, (d * d) / (nearFullDistance * nearFullDistance));
        }

        float k = nearDimSpeed > 0f ? 1f - Mathf.Exp(-nearDimSpeed * Time.deltaTime) : 1f;
        float next = Mathf.Lerp(nearScale, target, k);
        if (Mathf.Abs(next - nearScale) < 0.0005f && Mathf.Abs(target - nearScale) < 0.0005f) return;
        nearScale = next;

        float scale = Bloodrush.Flow.DarkSceneExposure.LightScale * nearScale;
        if (lampHd == null && lamp != null) lampHd = lamp.GetComponent<HDAdditionalLightData>();
        if (lampHd != null) lampHd.SetIntensity(lumen * scale, LightUnit.Lumen);
        if (reachLamp != null)
        {
            if (reachHd == null) reachHd = reachLamp.GetComponent<HDAdditionalLightData>();
            if (reachHd != null) reachHd.SetIntensity(reachLumen * scale, LightUnit.Lumen);
        }
    }

    // Koninin merkezi ve iç kısmının dört yanı: kenara giren yakın bir kutu da yakalansın.
    // En yakın isabet kullanılır. Oyuncunun kendi kapsülü ışının başladığı yerde olduğu için
    // Raycast onu görmez.
    float ProbeNearestHit()
    {
        Vector3 origin = lamp != null ? lamp.transform.position : camTr.position;
        float   spread = Mathf.Tan(spotAngle * 0.25f * Mathf.Deg2Rad);
        float   best   = float.MaxValue;
        foreach (var o in ProbeDirs)
        {
            Vector3 dir = (camTr.forward + camTr.right * (o.x * spread) + camTr.up * (o.y * spread)).normalized;
            if (Physics.Raycast(origin, dir, out RaycastHit hit, nearFullDistance, ~0, QueryTriggerInteraction.Ignore))
                best = Mathf.Min(best, hit.distance);
        }
        return best;
    }

    Coroutine flickering;

    // Dis etkiyle kisa kirpisma (ör. yakin patlama). Oyuncunun ac/kapa durumunu DEGISTIRMEZ,
    // ses ve bildirim yok (SetOn ikisini de yapiyor). Fener kapaliysa dokunmaz.
    public void Flicker(float duration)
    {
        if (lamp == null || !isOn || !isActiveAndEnabled) return;
        if (flickering != null) StopCoroutine(flickering);
        flickering = StartCoroutine(FlickerRoutine(duration));
    }

    IEnumerator FlickerRoutine(float duration)
    {
        float end = Time.time + duration;
        while (Time.time < end)
        {
            bool on = Random.value < 0.35f;
            lamp.enabled = on && isOn;
            if (reachLamp != null) reachLamp.enabled = on && isOn && enableReachBeam;
            yield return new WaitForSeconds(Random.Range(0.03f, 0.09f));
        }
        lamp.enabled = isOn;
        if (reachLamp != null) reachLamp.enabled = isOn && enableReachBeam;
        flickering = null;
    }

    public void SetOn(bool on)
    {
        isOn = on;
        if (lamp      != null) lamp.enabled      = on;
        if (reachLamp != null) reachLamp.enabled = on && enableReachBeam;
        sfx?.Play(clickClip, clickVolume);

        // Gorunurlukten BAGIMSIZ geri bildirim: tusun calisip calismadigini ayirt etmek icin.
        // Isik gorunmese bile bu yazi cikiyorsa girdi saglam, sorun render/pozlama tarafinda.
        Notification.Show(on ? "FENER ACIK" : "FENER KAPALI", 1.2f);
        Debug.Log($"[Flashlight] Toggle -> {(on ? "ACIK" : "KAPALI")}", this);
    }
}
}

using UnityEngine;
using UnityEngine.Events;
using Bloodrush.Shared.Audio;

namespace Bloodrush.Flow
{
// ADIM 3+4 — Sigorta kutusu. Oyuncu elindeki sigortaları E ile TEK TEK takar; her takışta
// kısmi geri bildirim (yuva lambası + ses + onFuseInserted), hepsi takılınca onPowered.
//
// Kapıyı KENDİSİ açmaz: neyin olacağı (kapı, ışık, ses) Inspector'dan onPowered'a bağlanır,
// kodda sabit değil. Sayım FuseSequenceManager'da — kutu sigortaları tanımaz.
public class FusePanel : ProximityInteractable
{
    [Header("Panel")]
    [Tooltip("Boşsa sahnedeki FuseSequenceManager bulunur, o da yoksa kendiliğinden kurulur.")]
    [SerializeField] FuseSequenceManager manager;
    [Tooltip("Güç gelmeden önce aralıklarla patlayan kıvılcım efekti (opsiyonel).")]
    [SerializeField] ParticleSystem sparks;
    [Tooltip("Kıvılcımlar arası bekleme aralığı (min, max sn).")]
    [SerializeField] Vector2 sparkInterval = new Vector2(1.5f, 4f);

    [Header("Yuvalar (kısmi geri bildirim)")]
    [Tooltip("Her takılan sigortada SIRADAKİ lamba yanar. Boşsa altındaki adı 'Slot' ile " +
             "başlayan objelerin Renderer'ları kullanılır (placeholder prefab'da hazır). " +
             "Karanlıkta parlayabilsinler diye materyalleri Play'de Unlit ile değiştirilir.")]
    [SerializeField] Renderer[] slotIndicators;
    [SerializeField] Color slotOffColor    = new Color(0.10f, 0.03f, 0.02f);
    [SerializeField] Color slotOnColor     = new Color(1f, 0.55f, 0.15f);
    [Tooltip("Yanan lambanın HDR çarpanı. Yüksek = Bloom ile parlar.")]
    [SerializeField] float slotOnIntensity = 3f;
    [Tooltip("Takılınca görünür olan sigorta görselleri. Boşsa altındaki adı 'Takili' ile " +
             "başlayan objeler kullanılır. Başta gizlenir.")]
    [SerializeField] GameObject[] insertedVisuals;

    [Header("Ses")]
    [SerializeField] AudioClip insertClip;
    [SerializeField] AudioClip powerOnClip;
    [Tooltip("Elde sigorta yokken E'ye basılırsa (opsiyonel).")]
    [SerializeField] AudioClip deniedClip;
    [SerializeField] [Range(0f,2f)] float volume = 1f;

    [Header("Olaylar")]
    [Tooltip("Her sigorta takılışında tetiklenir (kısmi ilerleme). Argüman = takılı sigorta sayısı.")]
    public UnityEvent<int> onFuseInserted;
    [Tooltip("Tüm sigortalar takılınca. Kapı/ışık/ses buraya: ElevatorDoor.Open, DoorStatusLight.SetOpen...")]
    public UnityEvent onPowered;

    public bool IsPowered { get; private set; }
    public int  FuseCount => manager != null ? manager.InsertedCount : 0;

    static readonly int UnlitColorId = Shader.PropertyToID("_UnlitColor");

    SfxPlayer  sfx;
    float      nextSpark;
    Material[] slotMats;

    void Start()
    {
        if (manager == null) manager = FuseSequenceManager.FindOrCreate();
        sfx = SfxPlayer.Create(gameObject, spatialBlend: 1f);
        SetupSlots();
        SetupInsertedVisuals();
        ScheduleSpark();
    }

    protected override string PromptText
    {
        get
        {
            if (IsPowered || manager == null) return null;
            string count = $"({manager.InsertedCount}/{manager.RequiredCount})";
            return manager.HeldCount > 0 ? $"[E] Sigortayı Tak  {count}"
                                         : $"Sigorta gerekiyor  {count}";
        }
    }

    protected override void OnInteract()
    {
        if (IsPowered || manager == null) return;

        if (!manager.TryInsert())
        {
            sfx?.Play(deniedClip, volume);
            return;
        }

        int n = manager.InsertedCount;
        SetSlot(n - 1, true);
        if (insertedVisuals != null && n - 1 < insertedVisuals.Length && insertedVisuals[n - 1] != null)
            insertedVisuals[n - 1].SetActive(true);
        sfx?.Play(insertClip, volume);
        onFuseInserted?.Invoke(n);

        if (manager.IsComplete) PowerOn();
    }

    void PowerOn()
    {
        IsPowered = true;
        ClearPrompt();
        if (sparks != null) sparks.Stop();
        sfx?.Play(powerOnClip, volume);
        Debug.Log("[FusePanel] Güç açıldı — onPowered tetikleniyor.", this);
        onPowered?.Invoke();
    }

    protected override void Update()
    {
        base.Update();

        if (!IsPowered && sparks != null && Time.time >= nextSpark)
        {
            sparks.Play();
            ScheduleSpark();
        }
    }

    void ScheduleSpark() =>
        nextSpark = Time.time + Random.Range(sparkInterval.x, sparkInterval.y);

    // ── Yuvalar ────────────────────────────────────────────────────────

    void SetupSlots()
    {
        if (slotIndicators == null || slotIndicators.Length == 0)
            slotIndicators = FindChildren<Renderer>("Slot");

        // Her lambaya kendi Unlit materyali: prefab'daki Lit materyal karanlıkta parlayamaz,
        // paylaşılan materyali boyamak da tüm lambaları birlikte yakardı.
        var shader = Shader.Find("HDRP/Unlit");
        slotMats = new Material[slotIndicators.Length];
        for (int i = 0; i < slotIndicators.Length; i++)
        {
            if (slotIndicators[i] == null || shader == null) continue;
            slotMats[i] = new Material(shader) { name = "FuseSlotLamp" };
            slotIndicators[i].sharedMaterial = slotMats[i];
            SetSlot(i, false);
        }
    }

    void SetSlot(int index, bool on)
    {
        if (slotMats == null || index < 0 || index >= slotMats.Length || slotMats[index] == null) return;
        slotMats[index].SetColor(UnlitColorId, on ? slotOnColor * slotOnIntensity : slotOffColor);
    }

    void SetupInsertedVisuals()
    {
        if (insertedVisuals == null || insertedVisuals.Length == 0)
        {
            var found = FindChildren<Transform>("Takili");
            insertedVisuals = new GameObject[found.Length];
            for (int i = 0; i < found.Length; i++) insertedVisuals[i] = found[i].gameObject;
        }
        foreach (var v in insertedVisuals)
            if (v != null) v.SetActive(false);
    }

    // Ada göre sıralı: Slot1, Slot2... hiyerarşi sırası değişse de yuvalar karışmaz.
    T[] FindChildren<T>(string prefix) where T : Component
    {
        var list = new System.Collections.Generic.List<T>();
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t == transform || !t.name.StartsWith(prefix)) continue;
            var c = t.GetComponent<T>();
            if (c != null) list.Add(c);
        }
        list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return list.ToArray();
    }

    void OnDestroy()
    {
        if (slotMats == null) return;
        foreach (var m in slotMats)
            if (m != null) Destroy(m);
    }
}
}

using UnityEngine;
using UnityEngine.Events;
using Bloodrush.UI;
using Bloodrush.Shared.Audio;

namespace Bloodrush.Flow
{
// ADIM 3+4 — Güç paneli. Gereken sigorta takılınca güç gelir.
//
// Kapıyı KENDİSİ açmaz: onPowered event'i üzerinden bağlanır (ör. ElevatorDoor.Open).
// Böylece panel neyin açılacağını bilmek zorunda kalmaz — kapı, ışık, ses, hepsi
// Inspector'dan takılır ve panel bozulmadan değiştirilebilir.
public class FusePanel : ProximityInteractable
{
    [Header("Panel")]
    [SerializeField] int requiredFuses = 2;
    [Tooltip("Güç gelmeden önce aralıklarla patlayan kıvılcım efekti (opsiyonel).")]
    [SerializeField] ParticleSystem sparks;
    [Tooltip("Kıvılcımlar arası bekleme aralığı (min, max sn).")]
    [SerializeField] Vector2 sparkInterval = new Vector2(1.5f, 4f);

    [Header("Ses")]
    [SerializeField] AudioClip insertClip;
    [SerializeField] AudioClip powerOnClip;
    [SerializeField] [Range(0f,2f)] float volume = 1f;

    [Header("Güç gelince")]
    [Tooltip("Kapıyı buraya bağla: ElevatorDoor.Open (ADIM 6).")]
    public UnityEvent onPowered;

    public bool IsPowered { get; private set; }
    public int  FuseCount { get; private set; }

    SfxPlayer sfx;
    float     nextSpark;

    void Start()
    {
        sfx = SfxPlayer.Create(gameObject, spatialBlend: 1f);
        ScheduleSpark();
    }

    protected override string PromptText =>
        IsPowered ? null : $"[E] Güç Panelini İncele  ({FuseCount}/{requiredFuses})";

    protected override void OnInteract()
    {
        if (IsPowered) return;
        int missing = requiredFuses - FuseCount;
        Notification.Show(missing <= 0
            ? "PANEL HAZIR"
            : $"SİGORTA YUVASI BOŞ — {missing} SİGORTA GEREKLİ");
    }

    // FuseItem çağırır.
    public void AddFuse()
    {
        if (IsPowered) return;
        FuseCount++;
        sfx?.Play(insertClip, volume);
        Notification.Show($"SİGORTA TAKILDI  {FuseCount}/{requiredFuses}");

        if (FuseCount >= requiredFuses) PowerOn();
    }

    void PowerOn()
    {
        IsPowered = true;
        ClearPrompt();
        if (sparks != null) sparks.Stop();
        sfx?.Play(powerOnClip, volume);
        Notification.Show("GÜÇ GERİ GELDİ");
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
}
}

using UnityEngine;
using UnityEngine.Events;
using Bloodrush.Shared.Audio;

namespace Bloodrush.Flow
{
// ADIM 4 — Yerden alınan sigorta. Alınınca oyuncunun ELİNE geçer; kutuya takmak ayrı bir
// adım (FusePanel). Kutuyu tanımaz: FuseSequenceManager'a "alındım" der, gerisi orada.
public class FuseItem : ProximityInteractable
{
    [Header("Sigorta")]
    [SerializeField] string displayName = "SİGORTA";
    [Tooltip("Boşsa sahnedeki FuseSequenceManager bulunur, o da yoksa kendiliğinden kurulur.")]
    [SerializeField] FuseSequenceManager manager;
    [Tooltip("Elde gösterilecek görsel. Boşsa bu objenin kendisi (altındaki placeholder ya da " +
             "gerçek model) kopyalanır — modeli değiştirince eldeki de kendiliğinden değişir.")]
    [SerializeField] Transform handVisual;

    [Header("Ses")]
    [SerializeField] AudioClip pickupClip;
    [SerializeField] [Range(0f,2f)] float pickupVolume = 1f;

    [Tooltip("Alındığı AN tetiklenir — silüet anını (SilhouetteScare.Show) buraya bağla.")]
    public UnityEvent onCollected;

    bool taken;

    protected override string PromptText => taken ? null : $"[E] {displayName} Al";

    void Start()
    {
        if (manager == null) manager = FuseSequenceManager.FindOrCreate();
    }

    protected override void OnInteract()
    {
        if (taken) return;
        taken = true;

        SfxPlayer.PlayAtPoint(pickupClip, transform.position, pickupVolume);

        if (manager == null) manager = FuseSequenceManager.FindOrCreate();
        // Kapatmadan ÖNCE: el modeli bu anda görseli kopyalıyor.
        manager.Collect(handVisual != null ? handVisual : transform);

        onCollected?.Invoke();
        gameObject.SetActive(false);
    }
}
}

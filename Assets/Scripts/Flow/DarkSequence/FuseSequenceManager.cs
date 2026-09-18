using System;
using UnityEngine;

namespace Bloodrush.Flow
{
// Sigorta sekansinin TEK durum sahibi ve araci. FuseItem ile FusePanel birbirini tanimaz;
// ikisi de buraya konusur: sigorta "alindim" der, kutu "bir tane tak" ister. Sayac HUD'u ve
// eldeki model de buradaki event'leri dinler — mantik sunumu hic bilmez.
//
// Mimari kurallar §2 — yeni singleton sorulari:
//  1) Gercekten tek mi olmali? Sahne basina TEK sigorta sekansi var ama static Instance
//     TUTULMUYOR; FuseItem/FusePanel sahnedeki ornegi bir kez bulup referansini saklar.
//  2) Sahne gecisinde yasamali mi? HAYIR — kullanici karari: bolum yeniden yuklenince durum
//     sifirlansin. DontDestroyOnLoad yok, sahneyle birlikte yok olur.
//  3) Sahneye elle konmak zorunda mi? Hayir — ilk ihtiyac aninda kendini kurar. Istersen
//     sahneye bir objeye ekleyip "Required Count" vb. degerleri Inspector'dan degistirirsin.
[DisallowMultipleComponent]
public class FuseSequenceManager : MonoBehaviour
{
    [Tooltip("Kutunun calismasi icin takilmasi gereken sigorta sayisi.")]
    [SerializeField] int requiredCount = 2;

    [Header("Sunum")]
    [Tooltip("Sol altta los 'SIGORTA x/y' sayaci.")]
    [SerializeField] bool showCounter = true;
    [Tooltip("Alinan sigortanin modeli oyuncunun sol elinde gorunsun.")]
    [SerializeField] bool showInHand  = true;

    public int  RequiredCount  => requiredCount;
    public int  HeldCount      { get; private set; }
    public int  InsertedCount  { get; private set; }
    public int  CollectedCount => HeldCount + InsertedCount;
    public bool IsComplete     => InsertedCount >= requiredCount;

    // Her durum degisiminde (alma/takma).
    public event Action            Changed;
    // Sigorta alindi — arguman: alinan sigortanin gorseli (el modeli bunu kopyalar).
    public event Action<Transform> Collected;
    // Sigorta takildi — arguman: takili toplam sigorta sayisi.
    public event Action<int>       Inserted;

    // Sahnedeki yoneticiyi bulur, yoksa kurar. Cagiran taraf sonucu SAKLAMALI (her karede
    // cagirmak icin degil).
    public static FuseSequenceManager FindOrCreate()
    {
        var m = FindFirstObjectByType<FuseSequenceManager>();
        if (m == null) m = new GameObject("FuseSequenceManager").AddComponent<FuseSequenceManager>();
        return m;
    }

    void Awake()
    {
        // Sunum bilesenleri ayni objede yasar; sahneden bir sey suruklemek gerekmez.
        if (showCounter)
        {
            var hud = GetComponent<FuseCounterHUD>();
            if (hud == null) hud = gameObject.AddComponent<FuseCounterHUD>();
            hud.Bind(this);
        }
        if (showInHand)
        {
            var hand = GetComponent<FuseHandView>();
            if (hand == null) hand = gameObject.AddComponent<FuseHandView>();
            hand.Bind(this);
        }
    }

    public void Collect(Transform visual)
    {
        if (IsComplete) return;
        HeldCount++;
        Debug.Log($"[FuseSequence] Sigorta alindi — elde {HeldCount}, takili {InsertedCount}/{requiredCount}.", this);
        Collected?.Invoke(visual);
        Changed?.Invoke();
    }

    // Elde sigorta yoksa false. Basariliysa takili sayi bir artar.
    public bool TryInsert()
    {
        if (HeldCount <= 0 || IsComplete) return false;
        HeldCount--;
        InsertedCount++;
        Debug.Log($"[FuseSequence] Sigorta takildi — {InsertedCount}/{requiredCount}.", this);
        Inserted?.Invoke(InsertedCount);
        Changed?.Invoke();
        return true;
    }
}
}

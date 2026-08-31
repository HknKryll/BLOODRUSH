using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using Bloodrush.Enemy;
using Bloodrush.Player;

// Sıralı güvenlik kilidi denetleyicisi. Konsollar kendini Register eder. Oyuncu
// konsolları HEDEF SIRADA aktive etmeli: doğru adım o konsolun düşman dalgasını
// çağırır; yanlış konsol sırayı sıfırlar + ceza dalgası getirir. Hepsi doğru
// sırada bitince boss kapısı açılır. Sıra ekranda (screenSlots renkleri) sürekli
// görünür. Bu objede bir trigger collider (oda hacmi) olmalı — oyuncu girince
// giriş mühürlenir.
namespace Bloodrush.Flow
{
public class SequenceLock : MonoBehaviour
{
    [Header("Bariyerler")]
    [Tooltip("Oyuncu odaya girince kapanır (mühür). Ölünce/bitince açılır.")]
    [SerializeField] GameObject entryBarrier;
    [Tooltip("Bulmaca bitince açılır (boss odasına geçiş).")]
    [SerializeField] GameObject bossDoorBarrier;

    [Header("Hedef sıra (konsol Index değerleri)")]
    [Tooltip("Örn. [1,0,2] = önce Index'i 1 olan konsol, sonra 0, sonra 2. Boşsa oyun başında rastgele permütasyon üretilir.")]
    [SerializeField] int[] targetOrder;

    [Header("Sıra ekranı (renk göstergeleri — hedef sırayla)")]
    [Tooltip("i. slot, hedef sıradaki i. konsolun rengini gösterir.")]
    [SerializeField] Renderer[] screenSlots;

    [Header("Yanlış ceza dalgası")]
    [SerializeField] GameObject  penaltyEnemyPrefab;
    [SerializeField] int         penaltyCount = 3;
    [SerializeField] Transform[] penaltySpawnPoints;

    [Header("Arkadan mühür (performans + kaçış yok)")]
    [Tooltip("Oyuncu odaya girince kapatılacak dövüş salonu kökü (IndustrialHall). Opsiyonel.")]
    [SerializeField] GameObject combatHallToDisable;

    public UnityEvent onComplete;

    public bool Finished { get; private set; }

    readonly List<PuzzleConsole> consoles = new();
    int  step;
    bool inited;
    bool entryOpened;
    bool sealedBehind;

    public void Register(PuzzleConsole c)
    {
        if (!consoles.Contains(c)) consoles.Add(c);
    }

    // PuzzleRoomBuilder kurulum-zamanında çağırır (referansları bağlar)
    public void Configure(GameObject entry, GameObject bossDoor, Renderer[] slots, Transform[] penaltySpawns)
    {
        entryBarrier       = entry;
        bossDoorBarrier    = bossDoor;
        screenSlots        = slots;
        penaltySpawnPoints = penaltySpawns;
    }

    void Start()
    {
        if (bossDoorBarrier) bossDoorBarrier.SetActive(true);   // boss kapısı kapalı
        if (entryBarrier)    entryBarrier.SetActive(true);      // giriş KAPALI — 4 terminal bitince açılır
    }

    void LateUpdate()
    {
        // İlk kez: TÜM konsol Start()'ları (Register'lar) bitmiş olur → sırayı kur
        if (!inited)
        {
            inited = true;
            if (targetOrder == null || targetOrder.Length == 0)
            {
                var idx = new List<int>();
                foreach (var c in consoles) idx.Add(c.Index);
                for (int i = idx.Count - 1; i > 0; i--)   // Fisher-Yates karıştır
                {
                    int j = Random.Range(0, i + 1);
                    (idx[i], idx[j]) = (idx[j], idx[i]);
                }
                targetOrder = idx.ToArray();
            }
            RefreshScreen();
        }

        // Giriş kapısı: WaveDirector'ın 4 terminali bitince aç (bir kez).
        // Sahnede WaveDirector yoksa (izole test) hemen aç.
        if (!entryOpened && entryBarrier)
        {
            var wd = WaveDirector.Instance;
            if (wd == null || wd.IsFinished)
            {
                entryBarrier.SetActive(false);
                entryOpened = true;
            }
        }
    }

    // Oda-hacmi trigger'ı: kapı AÇILDIKTAN sonra oyuncu içeri girince kapıyı arkadan
    // kapat (kaçış yok) + dövüş salonunu kapat (performans). Bir kez.
    void OnTriggerEnter(Collider o)
    {
        if (sealedBehind || !entryOpened) return;
        if (o.GetComponentInParent<PlayerMovement>() == null) return;

        sealedBehind = true;
        if (entryBarrier) entryBarrier.SetActive(true);            // kapıyı arkadan kapat
        if (combatHallToDisable) combatHallToDisable.SetActive(false); // salonu kapat
    }

    // PuzzleConsole çağırır
    public void OnConsoleActivated(PuzzleConsole c)
    {
        if (Finished || targetOrder == null || targetOrder.Length == 0) return;

        if (c.Index == targetOrder[step])
        {
            // Doğru
            c.SetActivated(true);
            SpawnWave(c.EnemyPrefab, c.EnemyCount, c.SpawnPoints);
            step++;
            RefreshScreen();
            if (step >= targetOrder.Length) Complete();
        }
        else
        {
            // Yanlış → sıfırla + ceza
            step = 0;
            foreach (var con in consoles) con.SetActivated(false);
            RefreshScreen();
            SpawnWave(penaltyEnemyPrefab, penaltyCount, penaltySpawnPoints);
        }
    }

    void Complete()
    {
        Finished = true;
        if (bossDoorBarrier) bossDoorBarrier.SetActive(false);   // boss kapısı açılır (ileri)
        // Giriş kapısı KAPALI kalır — arkadan mühürlendi (kaçış yok, salon zaten kapalı).
        onComplete?.Invoke();
    }

    // WaveDirector.Spawn deseni: doğur → NavMesh'e yapıştır → hemen avlamaya başlat
    void SpawnWave(GameObject prefab, int count, Transform[] points)
    {
        if (prefab == null || points == null || points.Length == 0) return;
        for (int i = 0; i < count; i++)
        {
            var pt = points[Random.Range(0, points.Length)];
            var go = Instantiate(prefab, pt.position, pt.rotation);

            if (go.TryGetComponent(out NavMeshAgent agent) &&
                NavMesh.SamplePosition(pt.position, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                agent.Warp(hit.position);

            if (go.TryGetComponent(out EnemyAI ai))
                ai.AlertNow();
        }
    }

    void RefreshScreen()
    {
        if (screenSlots == null || targetOrder == null) return;
        for (int i = 0; i < screenSlots.Length && i < targetOrder.Length; i++)
        {
            if (screenSlots[i] == null) continue;
            var con = ByIndex(targetOrder[i]);
            Color c = con != null ? con.Color : Color.gray;
            if (i < step) c *= 0.2f;   // tamamlanan adım sönük
            SetRendererColor(screenSlots[i], c);
        }
    }

    PuzzleConsole ByIndex(int idx)
    {
        foreach (var c in consoles) if (c.Index == idx) return c;
        return null;
    }

    static void SetRendererColor(Renderer r, Color c)
    {
        var m = r.material;
        if      (m.HasProperty("_UnlitColor"))    m.SetColor("_UnlitColor", c);
        else if (m.HasProperty("_EmissiveColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissiveColor", c); }
        else if (m.HasProperty("_BaseColor"))      m.SetColor("_BaseColor", c);
        else                                        m.color = c;
    }
}
}

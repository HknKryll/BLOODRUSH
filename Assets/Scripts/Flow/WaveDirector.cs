using UnityEngine;

// Ch3 Server Core dalga yönetmeni. Spawn'lar "heat" değerine bağlı: heat hem
// zamanla (kamp cezası) hem her terminal tamamlandıkça (ilerleme) yükselir.
// Heat arttıkça spawn sıklaşır, canlı düşman tavanı ve zor düşman oranı artar.
// Tüm terminaller bitince spawn durur, çıkış bariyeri açılır.
using Bloodrush.Shared;

namespace Bloodrush.Flow
{
public class WaveDirector : MonoBehaviour
{
    public static WaveDirector Instance { get; private set; }

    [Header("Spawn Noktaları (4 merdiven)")]
    [SerializeField] Transform[] spawnPoints;

    [Header("Düşman Prefabları")]
    [SerializeField] GameObject smallEnemy;
    [SerializeField] GameObject bigEnemy;
    [SerializeField] GameObject armoredEnemy;
    [SerializeField] GameObject fastEnemy;

    [Header("Heat / Escalation")]
    [SerializeField] float timeHeatRate     = 0.05f;   // saniyede heat artışı (oyalanma cezası)
    [SerializeField] float terminalHeatStep = 1.5f;    // her terminal +heat
    [SerializeField] float heatMax          = 10f;     // ölçekleme tavanı
    [SerializeField] float baseSpawnInterval = 3.5f;
    [SerializeField] float minSpawnInterval  = 0.8f;
    [SerializeField] int   baseAliveCap      = 6;
    [SerializeField] int   maxAliveCap       = 20;

    [Header("Bitiş")]
    [SerializeField] int        totalTerminals = 4;
    [SerializeField] GameObject exitBarrier;

    float elapsed;
    int   terminalsDone;
    float spawnTimer;
    int   aliveCount;
    bool  finished;

    void Awake() => Instance = this;

    void Start()
    {
        if (exitBarrier) exitBarrier.SetActive(true);
    }

    void Update()
    {
        if (finished) return;

        elapsed += Time.deltaTime;
        float heat = timeHeatRate * elapsed + terminalsDone * terminalHeatStep;
        float t    = Mathf.Clamp01(heat / heatMax);

        float interval = Mathf.Lerp(baseSpawnInterval, minSpawnInterval, t);
        int   cap      = Mathf.RoundToInt(Mathf.Lerp(baseAliveCap, maxAliveCap, t));

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f && aliveCount < cap)
        {
            spawnTimer = interval;
            Spawn(heat);
        }
    }

    void Spawn(float heat)
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return;
        GameObject prefab = PickEnemy(heat);
        if (prefab == null) return;

        var pt = spawnPoints[Random.Range(0, spawnPoints.Length)];
        var go = Instantiate(prefab, pt.position, pt.rotation);
        aliveCount++;

        if (go.TryGetComponent(out Health h))
            h.onDeath.AddListener(() => aliveCount = Mathf.Max(0, aliveCount - 1));
    }

    // Heat düştükçe küçük ağırlıklı; arttıkça zırhlı/hızlı/büyük katılır
    GameObject PickEnemy(float heat)
    {
        float r = Random.value;
        if (heat < 2f) return smallEnemy;
        if (heat < 4f) return r < 0.7f ? smallEnemy : armoredEnemy;
        if (heat < 6f) return r < 0.5f ? smallEnemy : (r < 0.75f ? fastEnemy : armoredEnemy);
        return r < 0.35f ? smallEnemy
             : r < 0.55f ? fastEnemy
             : r < 0.75f ? armoredEnemy
             :             bigEnemy;
    }

    // DataTerminal tamamlanınca çağırır
    public void OnTerminalDone()
    {
        terminalsDone++;
        if (terminalsDone >= totalTerminals) Finish();
    }

    void Finish()
    {
        finished = true;
        if (exitBarrier) exitBarrier.SetActive(false);   // çıkış açılır
    }

    // Null prefab varsa spawn atlar; eksik prefab'lar için güvenli
}
}

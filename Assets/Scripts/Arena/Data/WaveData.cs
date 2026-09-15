using UnityEngine;

namespace Bloodrush.Arena
{
// Dalga/spawn dengesi verisi — HEM WaveManager'in sabit-dalga modelini HEM
// WaveDirector'in heat-bazli eskalasyon modelini destekleyecek sekilde tek
// semada birlesir (bkz. BLOODRUSH_YENIDEN_YAPILANDIRMA_PLANI.md, Faz 3).
//
// Henuz hicbir sisteme baglanmadi — sadece veri semasi. WaveManager ve
// WaveDirector birlesip bu veriyi tuketecek sekilde guncellenmesi Faz 5'te.
[CreateAssetMenu(fileName = "WaveData", menuName = "Bloodrush/Wave Data")]
public class WaveData : ScriptableObject
{
    [Header("Sabit Dalga Modeli (WaveManager tarzı)")]
    public int   maxWaves          = 10;
    public int   baseEnemyCount    = 3;
    public int   enemyCountPerWave = 1;
    public float timeBetweenWaves  = 5f;

    [Header("Heat / Eskalasyon Modeli (WaveDirector tarzı)")]
    public float timeHeatRate      = 0.05f;
    public float terminalHeatStep  = 1.5f;
    public float heatMax           = 10f;
    public float baseSpawnInterval = 3.5f;
    public float minSpawnInterval  = 0.8f;
    public int   baseAliveCap      = 6;
    public int   maxAliveCap       = 20;
    public int   totalTerminals    = 4;

    [Header("Düşman Tipi Ağırlıkları")]
    public EnemyWeight[] enemyWeights;

    [Header("Spawn Noktası Kategorileri")]
    [Tooltip("Menzilli/zırhlı tiplerin yükseltilmiş (köprü/üst kat) spawn noktasını tercih etme olasılığı.")]
    [Range(0f, 1f)] public float elevatedSpawnChance = 0.7f;

    [Header("Pooling (Faz 6)")]
    [Tooltip("Bu dalga verisiyle eşleşen düşman havuzu için ön-ayırma boyutu. " +
             "Faz 6'dan önce hiçbir sistem okumuyor.")]
    public int enemyPoolSize = 20;
}

[System.Serializable]
public struct EnemyWeight
{
    public GameObject enemyPrefab;
    [Tooltip("Bu tipin seçilme ağırlığı (heat-bazlı modelde ayrıca minHeat eşiğiyle kısıtlanır).")]
    public float weight;
    [Tooltip("Bu tipin devreye girdiği minimum heat değeri — sabit-dalga modelinde yok sayılır.")]
    public float minHeat;
    [Tooltip("Doğarken yükseltilmiş spawn noktasını tercih etsin mi (ör. zırhlı/menzilli tipler).")]
    public bool  preferElevatedSpawn;
}
}

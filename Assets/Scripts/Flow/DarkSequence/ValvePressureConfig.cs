using UnityEngine;

namespace Bloodrush.Flow
{
// Valf mini oyunu + basinc gerilimi icin TUM sayilar ve ses klipleri. Bilesenler sahnede degil
// ValveSequence tarafindan calisma zamaninda eklendigi icin ayarlar burada durur;
// Assets/Resources/DarkSequence/ValvePressureConfig.asset'ten kendiliginden yuklenir.
// Play sirasinda degistirilen degerler aninda etki eder.
[CreateAssetMenu(menuName = "Bloodrush/Valf Basinc Ayarlari")]
public class ValvePressureConfig : ScriptableObject
{
    public const string ResourcePath = "DarkSequence/ValvePressureConfig";

    [System.Serializable]
    public class Tier
    {
        [Tooltip("Valfin acilmasi icin gereken basarili basis sayisi.")]
        public int   checks     = 3;
        [Tooltip("Yesil bolge genisligi (derece): ilk basista -> son basista.")]
        public float zoneStart  = 70f;
        public float zoneEnd    = 30f;
        [Tooltip("Ibre hizi (derece/sn): ilk basista -> son basista.")]
        public float speedStart = 180f;
        public float speedEnd   = 300f;
    }

    [Header("Sure penceresi")]
    [Tooltip("Acilan valfin kendiliginden kapanmadan once acik kalma suresi (sn). Mini oyun bu sureye dahil.")]
    public float windowSeconds = 22f;

    [Header("Mini oyun")]
    [Tooltip("0 = ilk valf, 1 = diger valf acikken (sayac islerken).")]
    public Tier[] tiers =
    {
        new Tier { checks = 3, zoneStart = 70f, zoneEnd = 30f, speedStart = 180f, speedEnd = 300f },
        new Tier { checks = 4, zoneStart = 60f, zoneEnd = 26f, speedStart = 215f, speedEnd = 360f },
    };
    [Tooltip("Iskadan sonra ibrenin durdugu sure.")]
    public float missLockout = 0.6f;
    [Tooltip("Kameranin carka donme suresi.")]
    public float cameraTurnTime = 0.25f;

    [Header("Ambiyans — siklik")]
    [Tooltip("Mini oyun disinda (kosu) iki olay arasi bekleme (min, max sn).")]
    public Vector2 calmInterval    = new Vector2(5f, 9f);
    [Tooltip("Mini oyun sirasinda iki olay arasi bekleme.")]
    public Vector2 intenseInterval = new Vector2(2.5f, 5f);
    [Tooltip("Olaylarin fisirti (0) ya da patlama/darbe (1) olma orani.")]
    [Range(0f, 1f)] public float bangRatio = 0.55f;
    [Tooltip("Darbe olaylarinin kaci BUYUK patlama olsun (geri kalani metal boru darbesi).")]
    [Range(0f, 1f)] public float bigExplosionChance = 0.25f;

    [Header("Ambiyans — mesafe")]
    public Vector2 nearDistance = new Vector2(3f, 7f);
    public Vector2 farDistance  = new Vector2(12f, 25f);
    [Tooltip("Darbe olaylarinin kaci yakinda olsun.")]
    [Range(0f, 1f)] public float nearChance = 0.45f;

    [Header("Sinsi zamanlama")]
    [Tooltip("Ibre yesil bolgeye girerken yakin bir patlama olma olasiligi.")]
    [Range(0f, 1f)] public float sneakyChance = 0.35f;
    public float sneakyCooldown = 6f;

    [Header("Yakin patlama etkileri")]
    public float shakeNear  = 0.22f;
    public float shakeBig   = 0.35f;
    [Range(0f, 1f)] public float flickerChance = 0.25f;
    public float   flickerCooldown = 14f;
    public Vector2 flickerDuration = new Vector2(0.2f, 0.45f);
    public bool    ceilingDust     = true;
    public int     dustParticles   = 60;

    [Header("Alarm")]
    [Tooltip("Pencere islerken bip araligi: sure dolu -> sure bitiyor.")]
    public float beepIntervalStart = 1.2f;
    public float beepIntervalEnd   = 0.4f;

    [Header("Klipler")]
    public AudioClip   hissLoop;
    public AudioClip[] hissOneShots;
    public AudioClip   steamBurst;
    public AudioClip[] pipeBangs;
    public AudioClip[] explosionsNear;
    public AudioClip[] explosionsFar;
    public AudioClip   alarmBeep;
    public AudioClip   turnStep;
    public AudioClip   releaseHiss;

    [Header("Ses seviyeleri")]
    [Range(0f, 1f)] public float hissLoopVolume  = 0.55f;
    [Range(0f, 1f)] public float hissVolume      = 0.8f;
    [Range(0f, 1f)] public float burstVolume     = 1f;
    [Range(0f, 1f)] public float bangVolume      = 0.9f;
    [Range(0f, 1f)] public float explosionVolume = 1f;
    [Range(0f, 1f)] public float alarmVolume     = 0.35f;
    [Range(0f, 1f)] public float turnVolume      = 0.9f;

    public Tier GetTier(int index) =>
        tiers == null || tiers.Length == 0 ? new Tier() : tiers[Mathf.Clamp(index, 0, tiers.Length - 1)];

    static ValvePressureConfig cached;

    // Bulunamazsa varsayilan degerlerle bir ornek uretir (klipler bos) — hicbir sey kirilmaz.
    public static ValvePressureConfig Load()
    {
        if (cached != null) return cached;
        cached = Resources.Load<ValvePressureConfig>(ResourcePath);
        if (cached == null)
        {
            Debug.LogWarning($"[ValvePressureConfig] Resources/{ResourcePath} bulunamadi — varsayilanlar, sessiz.");
            cached = CreateInstance<ValvePressureConfig>();
        }
        return cached;
    }

    public static AudioClip Pick(AudioClip[] clips) =>
        clips == null || clips.Length == 0 ? null : clips[Random.Range(0, clips.Length)];
}
}

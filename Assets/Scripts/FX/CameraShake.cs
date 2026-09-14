using System.Collections;
using UnityEngine;

namespace Bloodrush.FX
{
// Kamera sarsıntısı — SÜRÜKLENMEZ. Eski sürüm her DoShake başında
// "origin = localPosition" yakalıyordu; üst üste binen shake'lerde (ateş, parry,
// terminal) ikinci coroutine zaten kaymış konumu origin sanıp oraya döndürüyor,
// kamera gerçek merkezine dönemeyip kalıcı kayıyordu. Yeni model: merkez BİR KEZ
// yakalanır (basePos), her kare localPosition = basePos + gürültü*trauma yazılır,
// üst üste binen shake'ler sadece trauma'yı artırır (tavanla sınırlı). Böylece
// kamera her zaman basePos'a döner — kalıcı kayma imkânsız.
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    [Tooltip("Trauma'nın saniyede azalma hızı (büyük = daha kısa sarsıntı).")]
    [SerializeField] float traumaDecay = 2.5f;
    [Tooltip("Ofset tavanı (m) — üst üste binen shake'ler bunu aşamaz.")]
    [SerializeField] float maxTrauma   = 0.5f;
    [Tooltip("Gürültü hızı (titreşim sıklığı).")]
    [SerializeField] float frequency   = 40f;

    Vector3 basePos;
    float   trauma;      // 0..maxTrauma — aynı zamanda ofsetin metre büyüklüğü
    float   seedX, seedY;
    bool    haveBase;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        seedX = Random.value * 100f;
        seedY = Random.value * 100f;
    }

    void Start()
    {
        // Merkez bir kez, tüm Start'lardan sonra yakalanır (kamera yerel konumu sabit)
        basePos  = transform.localPosition;
        haveBase = true;
    }

    // intensity = eklenecek tepe ofset (m). Üst üste binenler toplanır, tavanla sınırlı.
    // duration parametresi artık yok sayılır — sönümleme traumaDecay ile yapılır (API uyumu için kalıyor).
    // Ayarlardaki "kamera sarsıntısı" kaydırıcısı (0..1). 0 = sarsıntı tamamen kapalı.
    // Oyun kanca ve hızlı hareket içerdiği için bazı oyuncularda mide bulantısı
    // yapabiliyor — bu yüzden sıfıra kadar inebilmeli (bkz. SettingsApplier).
    public static float ShakeScale = 1f;

    public static void Shake(float intensity = 0.15f, float duration = 0.15f)
    {
        intensity *= ShakeScale;
        if (intensity <= 0f) return;
        if (Instance) Instance.trauma = Mathf.Min(Instance.maxTrauma, Instance.trauma + intensity);
    }

    // Kamera taban konumunun SAHİBİ dışarısıdır (PlayerMovement) — flip'te göz tavanın
    // altına iner. CameraShake yalnızca bu tabanın üstüne sarsıntı ofseti ekler; kendi
    // yakaladığı sabit basePos'u dayatıp flip kamerasını ezmez. Her kare çağrılabilir.
    public void SetBaseLocalPos(Vector3 p) { basePos = p; haveBase = true; }

    public static void HitPause(float duration = 0.06f, float scale = 0.12f)
    {
        if (Instance && Time.timeScale > 0f)
            Instance.StartCoroutine(Instance.DoHitPause(duration, scale));
    }

    IEnumerator DoHitPause(float duration, float scale)
    {
        Time.timeScale = scale;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
    }

    void LateUpdate()
    {
        if (!haveBase) return;

        if (trauma <= 0f)
        {
            transform.localPosition = basePos;   // her zaman gerçek merkez
            return;
        }

        // unscaledTime → hit-pause/slow-mo sırasında da sarsıntı akıcı kalır
        float t = Time.unscaledTime * frequency;
        float x = (Mathf.PerlinNoise(seedX, t) - 0.5f) * 2f * trauma;
        float y = (Mathf.PerlinNoise(seedY, t) - 0.5f) * 2f * trauma;
        transform.localPosition = basePos + new Vector3(x, y, 0f);

        trauma = Mathf.MoveTowards(trauma, 0f, traumaDecay * Time.unscaledDeltaTime);
    }
}
}

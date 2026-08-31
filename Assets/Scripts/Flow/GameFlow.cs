using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// Her kampanya sahnesine boş bir GameObject olarak konur.
// Checkpoint = sahne başı: ölünce sahne yeniden yüklenir, upload barı sahne
// başındaki değerine geri çekilir.
using Bloodrush.Shared;
using Bloodrush.Player;
using Bloodrush.UI;

namespace Bloodrush.Flow
{
public class GameFlow : MonoBehaviour
{
    [SerializeField] float deathFadeDuration = 1.5f;

    [Header("Loadout")]
    [Tooltip("Bu bölümde launcher (sağ tık: bomba/flash/anchor) açık mı? Ch2'de kapat.")]
    [SerializeField] bool launcherEnabled = true;
    [Tooltip("BEDEN / YÜKLEME barları görünsün mü? Ch1-Ch2'de kapat, Ch3+'te aç.")]
    [SerializeField] bool showHUDBars = true;

    [Header("Silah İlerlemesi")]
    [Tooltip("Yeni oyun başı — silah kilidini sıfırla (sadece revolver). SADECE CH1'de TRUE.")]
    [SerializeField] bool resetProgressOnStart = false;
    [Tooltip("Bu sahnede tüm silahlar açık olsun (izole test kolaylığı; ilerlemeyi yok sayar).")]
    [SerializeField] bool allWeaponsThisScene = false;

    static float uploadAtSceneStart;

    static GameFlow  instance;
    static Vector3    cpPos;
    static Quaternion cpRot;
    static bool       hasCheckpoint;

    bool respawning;

    void Awake()
    {
        instance = this;
        Time.timeScale = 1f;   // hit-pause/slow-mo ortasında ölüm ihtimaline karşı
        uploadAtSceneStart = GameHUD.UploadProgress;
        hasCheckpoint = false; // yeni sahne temiz başlar (checkpoint önceki sahneden taşınmasın)
        if (resetProgressOnStart) GameProgress.ResetRun();   // yeni oyun: sadece revolver
    }

    // Checkpoint trigger'ı çağırır
    public static void SetCheckpoint(Transform t)
    {
        cpPos = t.position;
        cpRot = Quaternion.Euler(0f, t.eulerAngles.y, 0f);
        hasCheckpoint = true;
        Debug.Log($"[GameFlow] Checkpoint kaydedildi: {cpPos}");
    }

    // KillVolume (düşme) çağırır. Checkpoint yoksa sahne başına döner (0,0,0'a ışınlamaz).
    public static void RespawnAtCheckpoint()
    {
        Debug.Log($"[GameFlow] RespawnAtCheckpoint — instance={(instance != null)}, hasCheckpoint={hasCheckpoint}, respawning={(instance != null && instance.respawning)}");
        if (instance != null && !instance.respawning)
        {
            instance.respawning = true;
            instance.StartCoroutine(instance.RespawnRoutine(hasCheckpoint));
        }
    }

    void Start()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        if (player.TryGetComponent(out Health health))
            health.onDeath.AddListener(OnPlayerDied);

        // Bölüm loadout'u — launcher açık/kapalı + silah kilidi (ilerlemeye göre)
        var ps = player.GetComponentInChildren<PlayerShoot>(true);
        if (ps)
        {
            ps.LauncherEnabled = launcherEnabled;
            ps.SetUnlocked(PlayerShoot.Firearm.Shotgun, GameProgress.ShotgunUnlocked || allWeaponsThisScene);
            ps.SetUnlocked(PlayerShoot.Firearm.Lmg,     GameProgress.LmgUnlocked     || allWeaponsThisScene);
        }

        // BEDEN / YÜKLEME barları görünürlüğü
        if (GameHUD.Instance) GameHUD.Instance.SetVisible(showHUDBars);
    }

    void OnPlayerDied()
    {
        if (respawning) return;
        respawning = true;
        StartCoroutine(RespawnRoutine(hasCheckpoint));
    }

    IEnumerator RespawnRoutine(bool useCheckpoint)
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        PlayerMovement pm = player ? player.GetComponent<PlayerMovement>() : null;
        PlayerShoot    ps = player ? player.GetComponent<PlayerShoot>()    : null;
        if (pm) pm.enabled = false;
        if (ps) ps.enabled = false;

        // Siyah fade (checkpoint respawn daha hızlı)
        float fadeDur = useCheckpoint ? 0.4f : deathFadeDuration;
        var img = CreateOverlay(Color.black);
        float elapsed = 0f;
        while (elapsed < fadeDur)
        {
            elapsed += Time.unscaledDeltaTime;
            img.color = new Color(0f, 0f, 0f, elapsed / fadeDur);
            yield return null;
        }

        Time.timeScale = 1f;

        Debug.Log($"[GameFlow] RespawnRoutine — useCheckpoint={useCheckpoint}, player={(player != null)}. " +
                  (useCheckpoint && player != null ? $"Checkpoint'e ışınlanıyor: {cpPos}" : "Sahne baştan yükleniyor."));

        if (useCheckpoint && player != null)
        {
            // Sahneyi yeniden yüklemeden checkpoint'te canlandır
            player.GetComponent<Health>()?.Revive();
            if (pm) { pm.enabled = true; pm.Teleport(cpPos, cpRot); }
            if (ps) ps.enabled = true;

            // fade geri aç
            elapsed = 0f;
            while (elapsed < 0.4f)
            {
                elapsed += Time.unscaledDeltaTime;
                img.color = new Color(0f, 0f, 0f, 1f - elapsed / 0.4f);
                yield return null;
            }
            Destroy(img.canvas.gameObject);
            respawning = false;
        }
        else
        {
            GameHUD.UploadProgress = uploadAtSceneStart;   // sahne başına geri sar
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    public static void LoadNext()
    {
        int next = SceneManager.GetActiveScene().buildIndex + 1;
        if (next >= SceneManager.sceneCountInBuildSettings)
            next = 0;   // liste bitti → ana menü
        Time.timeScale = 1f;
        SceneManager.LoadScene(next);
    }

    public static Image CreateOverlay(Color baseColor)
    {
        var cgo    = new GameObject("FadeOverlay");
        var canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        cgo.AddComponent<CanvasScaler>();

        var img = new GameObject("Fade").AddComponent<Image>();
        img.transform.SetParent(cgo.transform, false);
        img.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
        img.raycastTarget = false;
        var rt = img.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return img;
    }
}
}

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// Ch1_Salon açılış yöneticisi. Oyuncuyu kısıtlı moda alır (silah yok,
// zıplama/slide yok, sakin yürüyüş), itiraf metni bitince hedef yazısını
// gösterir, koltuğa bakınca [E] Otur prompt'u çıkarır; E'ye basınca kamera
// oturma pozuna kayar → fade → sonraki sahne.
using Bloodrush.UI;
using Bloodrush.Player;

namespace Bloodrush.Flow
{
public class IntroSalonController : MonoBehaviour
{
    [Header("Referanslar")]
    [SerializeField] GameObject weaponModel;    // gizlenecek silah mesh'i
    [SerializeField] Transform  chair;          // koltuk (yakınlık + bakış hedefi)
    [SerializeField] Transform  sitCameraPose;  // kamera oturunca kayacağı hedef

    [Header("Ayarlar")]
    [Tooltip("Oturunca yüklenecek sahne. Boşsa sıradaki build index'i yüklenir. Sahne Build Settings'te olmalı!")]
    [SerializeField] string nextSceneName      = "";
    [SerializeField] float walkSpeedMultiplier = 0.32f;   // 14 * 0.32 ≈ 4.5
    [SerializeField] float sitRange            = 2.5f;
    [SerializeField] float sitLookDot          = 0.6f;
    [SerializeField] float sitCameraMoveTime   = 1f;

    [Header("Metinler")]
    [SerializeField] string objectiveText = "bir yer bul... ve otur";
    [SerializeField] string promptText    = "[E] Otur";

    GameObject     player;
    Camera         playerCam;
    PlayerMovement movement;

    Text objectiveLabel;
    Text promptLabel;

    bool controlsReady;   // itiraf metni bitti, oyuncu yürüyebilir
    bool sitting;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) { enabled = false; return; }

        playerCam = player.GetComponentInChildren<Camera>();
        movement  = player.GetComponent<PlayerMovement>();

        // Silahı gizle, savaş scriptlerini kapat
        if (weaponModel) weaponModel.SetActive(false);
        DisableComponent<PlayerShoot>();
        DisableComponent<GrapplingHook>();
        DisableComponent<StimulantSystem>();

        // Hareketi kısıtla
        if (movement)
        {
            movement.SpeedMultiplier = walkSpeedMultiplier;
            movement.JumpEnabled     = false;
            movement.SlideEnabled    = false;
        }

        BuildUI();

        // İtiraf metni varsa bitişini bekle, yoksa kontrolü hemen aç
        if (FindObjectOfType<IntroTextSequence>() != null)
            IntroTextSequence.OnFinished += OnIntroFinished;
        else
            OnIntroFinished();
    }

    void OnDestroy()
    {
        IntroTextSequence.OnFinished -= OnIntroFinished;
    }

    void OnIntroFinished()
    {
        IntroTextSequence.OnFinished -= OnIntroFinished;
        controlsReady = true;
        if (objectiveLabel) objectiveLabel.enabled = true;
    }

    void Update()
    {
        if (!controlsReady || sitting) return;
        if (chair == null || playerCam == null) return;

        Vector3 toChair = chair.position - playerCam.transform.position;
        float   dist    = toChair.magnitude;
        float   dot     = Vector3.Dot(playerCam.transform.forward, toChair.normalized);

        bool canSit = dist <= sitRange && dot >= sitLookDot;
        if (promptLabel) promptLabel.enabled = canSit;

        if (canSit && Input.GetKeyDown(KeyCode.E))
            StartCoroutine(SitAndExit());
    }

    IEnumerator SitAndExit()
    {
        sitting = true;
        if (objectiveLabel) objectiveLabel.enabled = false;
        if (promptLabel)    promptLabel.enabled    = false;

        if (movement) movement.enabled = false;   // yürüyüş + bakış dursun

        // Kamerayı oturma pozuna yumuşakça kaydır
        if (playerCam != null && sitCameraPose != null)
        {
            Vector3    startPos = playerCam.transform.position;
            Quaternion startRot = playerCam.transform.rotation;
            float t = 0f;
            while (t < sitCameraMoveTime)
            {
                t += Time.deltaTime;
                float p = Mathf.SmoothStep(0f, 1f, t / sitCameraMoveTime);
                playerCam.transform.position = Vector3.Lerp(startPos, sitCameraPose.position, p);
                playerCam.transform.rotation = Quaternion.Slerp(startRot, sitCameraPose.rotation, p);
                yield return null;
            }
        }

        yield return new WaitForSeconds(0.4f);

        // Fade → sonraki sahne
        var img = GameFlow.CreateOverlay(Color.black);
        float f = 0f;
        while (f < 1.2f)
        {
            f += Time.deltaTime;
            img.color = new Color(0f, 0f, 0f, f / 1.2f);
            yield return null;
        }

        Time.timeScale = 1f;
        if (!string.IsNullOrEmpty(nextSceneName))
            SceneManager.LoadScene(nextSceneName);
        else
            GameFlow.LoadNext();
    }

    void DisableComponent<T>() where T : MonoBehaviour
    {
        var c = player.GetComponentInChildren<T>(true);
        if (c) c.enabled = false;
    }

    void BuildUI()
    {
        var cgo    = new GameObject("IntroSalonUI");
        var canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        cgo.AddComponent<CanvasScaler>();

        // Hedef yazısı — sol üstte (WASD tutorialı alt-ortada çıktığı için çakışmasın)
        objectiveLabel = MakeLabel(cgo.transform, objectiveText, 18,
            new Color(0.75f, 0.75f, 0.75f, 0.85f),
            new Vector2(0.02f, 0.86f), new Vector2(0.5f, 0.97f), TextAnchor.UpperLeft);
        objectiveLabel.enabled = false;

        // Prompt — ekran ortasının biraz altında, ortalı
        promptLabel = MakeLabel(cgo.transform, promptText, 22,
            new Color(1f, 1f, 1f, 0.9f),
            new Vector2(0f, 0.34f), new Vector2(1f, 0.42f), TextAnchor.MiddleCenter);
        promptLabel.enabled = false;
    }

    Text MakeLabel(Transform parent, string text, int size, Color color, Vector2 anchorMin, Vector2 anchorMax, TextAnchor align)
    {
        var go  = new GameObject("Label");
        go.transform.SetParent(parent, false);
        var lbl = go.AddComponent<Text>();
        lbl.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        lbl.text      = text;
        lbl.fontSize  = size;
        lbl.color     = color;
        lbl.alignment = align;
        lbl.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return lbl;
    }
}
}

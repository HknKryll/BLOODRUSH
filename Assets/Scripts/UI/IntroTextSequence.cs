using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class IntroTextSequence : MonoBehaviour
{
    [SerializeField] float charDelay = 0.04f;
    [SerializeField] float linePause = 0.3f;
    [SerializeField] float endPause  = 2.5f;

    static readonly string[] Lines =
    {
        "Takıntım beni yıllarca kör etti.",
        "",
        "Ne yaptığımı biliyordum ama aklım ne yaptığımda değil,",
        "yaptığım şeyin başarısındaydı.",
        "",
        "Fakat yaptığım şeyin ne denli tehlikeli,",
        "ne denli ölümcül olduğunu görünce kafama dank etti.",
        "",
        "İşte o an ben kararımı verdim..."
    };

    public static event System.Action OnFinished;

    bool skipRequested;

    void Update()
    {
        if (Input.anyKeyDown) skipRequested = true;
    }

    void Start()
    {
        var movement = FindObjectOfType<PlayerMovement>();
        if (movement) movement.enabled = false;

        StartCoroutine(PlaySequence());
    }

    IEnumerator PlaySequence()
    {
        // Siyah ekran canvas
        var cgo    = new GameObject("IntroCanvas");
        var canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        cgo.AddComponent<CanvasScaler>();

        // Siyah arka plan
        var bgImg = new GameObject("BG").AddComponent<Image>();
        bgImg.transform.SetParent(cgo.transform, false);
        bgImg.color = Color.black;
        var bgRT = bgImg.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;

        // İtiraf metni
        var txt = new GameObject("ConfessionText").AddComponent<Text>();
        txt.transform.SetParent(cgo.transform, false);
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = 20;
        txt.color     = new Color(0.88f, 0.88f, 0.88f, 1f);
        txt.alignment = TextAnchor.MiddleCenter;
        txt.lineSpacing = 1.4f;
        txt.text      = "";
        var txtRT = txt.GetComponent<RectTransform>();
        txtRT.anchorMin = new Vector2(0.12f, 0.2f);
        txtRT.anchorMax = new Vector2(0.88f, 0.8f);
        txtRT.offsetMin = txtRT.offsetMax = Vector2.zero;

        // Atlama ipucu
        var hint = new GameObject("SkipHint").AddComponent<Text>();
        hint.transform.SetParent(cgo.transform, false);
        hint.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        hint.fontSize  = 12;
        hint.color     = new Color(0.45f, 0.45f, 0.45f, 1f);
        hint.alignment = TextAnchor.LowerRight;
        hint.text      = "herhangi bir tuş — atla";
        hint.raycastTarget = false;
        var hintRT = hint.GetComponent<RectTransform>();
        hintRT.anchorMin = new Vector2(0f, 0f);
        hintRT.anchorMax = new Vector2(1f, 1f);
        hintRT.offsetMin = new Vector2(0f, 16f);
        hintRT.offsetMax = new Vector2(-20f, 0f);

        // Karakteri tek tek yaz
        string full = "";
        foreach (var line in Lines)
        {
            if (skipRequested) break;
            foreach (char c in line)
            {
                if (skipRequested) break;
                full += c;
                txt.text = full;
                yield return new WaitForSecondsRealtime(charDelay);
            }
            if (!skipRequested)
            {
                full += "\n";
                txt.text = full;
                yield return new WaitForSecondsRealtime(linePause);
            }
        }

        if (!skipRequested)
            yield return new WaitForSecondsRealtime(endPause);

        // Fade out
        float elapsed = 0f;
        Color textColor = txt.color;
        while (elapsed < 0.7f)
        {
            elapsed += Time.unscaledDeltaTime;
            float a = 1f - elapsed / 0.7f;
            bgImg.color = new Color(0f, 0f, 0f, a);
            txt.color   = new Color(textColor.r, textColor.g, textColor.b, a);
            if (hint) hint.color = new Color(0.45f, 0.45f, 0.45f, a);
            yield return null;
        }

        // Oyuncuyu etkinleştir
        var pm = FindObjectOfType<PlayerMovement>();
        if (pm) pm.enabled = true;

        OnFinished?.Invoke();

        Destroy(cgo);
        Destroy(gameObject);
    }
}

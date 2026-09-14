using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Bloodrush.Player;

namespace Bloodrush.UI
{
public class CrosshairHUD : MonoBehaviour
{
    public static CrosshairHUD Instance { get; private set; }

    [SerializeField] Color color       = Color.white;
    [SerializeField] Color hitColor    = Color.red;
    [SerializeField] float lineSize    = 10f;
    [SerializeField] float thickness   = 2f;
    [SerializeField] float gap         = 5f;
    [SerializeField] bool  centerDot   = true;
    [SerializeField] float hitDuration = 0.12f;

    [Header("Kanca Cooldown")]
    [SerializeField] Color cooldownColor = new Color(0.5f, 0.85f, 1f, 0.9f);

    readonly List<GameObject> hitLines = new();

    GameObject hookBarRoot;
    Image      hookBarFill;

    readonly List<GameObject> normalLines = new List<GameObject>();
    GameObject dotLine;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        gameObject.AddComponent<CanvasScaler>();
        gameObject.AddComponent<GraphicRaycaster>();

        // Normal crosshair (+) — referanslar saklanıyor ki ayarlardan tip/renk değişebilsin
        normalLines.Add(MakeLine(new Vector2( gap + lineSize * 0.5f,  0), new Vector2(lineSize, thickness), 0f, color));
        normalLines.Add(MakeLine(new Vector2(-(gap + lineSize * 0.5f), 0), new Vector2(lineSize, thickness), 0f, color));
        normalLines.Add(MakeLine(new Vector2( 0,  gap + lineSize * 0.5f),  new Vector2(lineSize, thickness), 90f, color));
        normalLines.Add(MakeLine(new Vector2( 0, -(gap + lineSize * 0.5f)), new Vector2(lineSize, thickness), 90f, color));

        // Nokta her zaman ÜRETİLİR, sadece görünürlüğü değişir — sonradan açılabilsin diye.
        dotLine = MakeLine(Vector2.zero, new Vector2(thickness, thickness), 0f, color, centerDot);

        // Hit marker (X) — başta gizli
        float d = (gap + lineSize * 0.5f) * 0.707f;
        hitLines.Add(MakeLine(new Vector2( d,  d), new Vector2(lineSize, thickness),  45f, hitColor, false));
        hitLines.Add(MakeLine(new Vector2(-d,  d), new Vector2(lineSize, thickness), -45f, hitColor, false));
        hitLines.Add(MakeLine(new Vector2( d, -d), new Vector2(lineSize, thickness), -45f, hitColor, false));
        hitLines.Add(MakeLine(new Vector2(-d, -d), new Vector2(lineSize, thickness),  45f, hitColor, false));

        BuildHookBar();
    }

    void BuildHookBar()
    {
        // Nişangâhın ~16px altında küçük cooldown çubuğu
        const float barW = 26f, barH = 3f;
        float barY = -(gap + lineSize + 8f);

        hookBarRoot = new GameObject("_hookBar");
        hookBarRoot.transform.SetParent(transform, false);
        var rootRT = hookBarRoot.AddComponent<RectTransform>();
        rootRT.anchorMin = rootRT.anchorMax = new Vector2(0.5f, 0.5f);
        rootRT.anchoredPosition = new Vector2(0f, barY);
        rootRT.sizeDelta = new Vector2(barW, barH);

        // Arka plan
        var bg = new GameObject("bg").AddComponent<Image>();
        bg.transform.SetParent(hookBarRoot.transform, false);
        bg.color = new Color(0f, 0f, 0f, 0.5f);
        var bgRT = bg.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = new Vector2(-1f, -1f); bgRT.offsetMax = new Vector2(1f, 1f);

        // Dolum
        hookBarFill = new GameObject("fill").AddComponent<Image>();
        hookBarFill.transform.SetParent(hookBarRoot.transform, false);
        hookBarFill.color      = cooldownColor;
        hookBarFill.type       = Image.Type.Filled;
        hookBarFill.fillMethod = Image.FillMethod.Horizontal;
        hookBarFill.fillOrigin = 0;
        hookBarFill.fillAmount = 1f;
        var fillRT = hookBarFill.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = fillRT.offsetMax = Vector2.zero;

        hookBarRoot.SetActive(false);   // hazırken gizli
    }

    // GrapplingHook her karede çağırır: 1 = hazır (gizli), <1 = dolan bar
    public void SetHookCooldown(float normalized)
    {
        if (hookBarRoot == null) return;
        bool ready = normalized >= 1f;
        if (hookBarRoot.activeSelf == ready) hookBarRoot.SetActive(!ready);
        if (!ready) hookBarFill.fillAmount = Mathf.Clamp01(normalized);
    }

    GameObject MakeLine(Vector2 offset, Vector2 size, float rotation, Color col, bool active = true)
    {
        var go = new GameObject("_line");
        go.transform.SetParent(transform, false);
        go.SetActive(active);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = offset;
        rt.sizeDelta        = size;
        rt.localRotation    = Quaternion.Euler(0f, 0f, rotation);

        go.AddComponent<Image>().color = col;
        return go;
    }

    // Ayarlar panelinden çağrılır (SettingsApplier.ApplyGameplay).
    // 0 = çizgi, 1 = çizgi + nokta, 2 = sadece nokta, 3 = kapalı.
    public void ApplyStyle(int type, Color c)
    {
        bool linesOn = type == 0 || type == 1;
        bool dotOn   = type == 1 || type == 2;

        foreach (var l in normalLines)
        {
            if (l == null) continue;
            if (l.activeSelf != linesOn) l.SetActive(linesOn);
            if (l.TryGetComponent(out Image img)) img.color = c;
        }

        if (dotLine != null)
        {
            if (dotLine.activeSelf != dotOn) dotLine.SetActive(dotOn);
            if (dotLine.TryGetComponent(out Image dimg)) dimg.color = c;
        }
    }

    public void ShowHitMarker()
    {
        StopAllCoroutines();
        StartCoroutine(HitFlash());
    }

    IEnumerator HitFlash()
    {
        foreach (var l in hitLines) l.SetActive(true);
        yield return new WaitForSecondsRealtime(hitDuration);
        foreach (var l in hitLines) l.SetActive(false);
    }
}
}

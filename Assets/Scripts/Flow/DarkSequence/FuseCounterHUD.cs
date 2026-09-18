using UnityEngine;
using UnityEngine.UI;

namespace Bloodrush.Flow
{
// Karanlik sekans icin LOS sigorta sayaci: sol altta kucuk "SİGORTA  1/2". Ilk sigorta
// alininca yumusakca belirir, hepsi takilinca kisa bir sure sonra soner. Parlak Notification
// yerine bu kullaniliyor — karanlikta goz almasin.
//
// FuseSequenceManager'in Changed event'ini dinler; kendi canvas'ini kurar, sahneye bir sey
// koymak gerekmez. Sort 12: oyun HUD'u bandinda, pause arka planinin (190) altinda.
public class FuseCounterHUD : MonoBehaviour
{
    const int SortingOrder = 12;

    [SerializeField] string  label       = "SİGORTA";
    [SerializeField] Color   textColor   = new Color(0.86f, 0.83f, 0.76f);
    [SerializeField] Color   markColor   = new Color(1f, 0.62f, 0.22f);
    [Tooltip("Gorunurken ulasilan opaklik. Dusuk = daha los.")]
    [Range(0f, 1f)]
    [SerializeField] float   opacity     = 0.55f;
    [SerializeField] int     fontSize    = 22;
    [Tooltip("Sol alt koseden bosluk (1920x1080 referansinda).")]
    [SerializeField] Vector2 margin      = new Vector2(56f, 48f);
    [SerializeField] float   fadeDuration = 0.4f;
    [Tooltip("Hepsi takilinca sayac bu kadar sn gorunur kalir, sonra soner.")]
    [SerializeField] float   hideDelayOnComplete = 1.5f;

    FuseSequenceManager manager;
    Text        text;
    CanvasGroup group;
    GameObject  canvasGo;
    float       target;
    float       hideAt = -1f;

    public void Bind(FuseSequenceManager m)
    {
        if (manager != null) manager.Changed -= Refresh;
        manager = m;
        manager.Changed += Refresh;
        if (canvasGo == null) Build();
        Refresh();
    }

    void OnDestroy()
    {
        if (manager != null) manager.Changed -= Refresh;
        if (canvasGo != null) Destroy(canvasGo);
    }

    void Refresh()
    {
        if (manager == null || text == null) return;
        text.text = $"{label}  {manager.CollectedCount}/{manager.RequiredCount}";

        if (manager.IsComplete)
        {
            target = opacity;
            hideAt = Time.unscaledTime + hideDelayOnComplete;
        }
        else
        {
            target = manager.CollectedCount > 0 ? opacity : 0f;
            hideAt = -1f;
        }
    }

    void Update()
    {
        if (group == null) return;
        if (hideAt >= 0f && Time.unscaledTime >= hideAt) { target = 0f; hideAt = -1f; }

        float step = fadeDuration > 0f ? Time.unscaledDeltaTime / fadeDuration : 1f;
        group.alpha = Mathf.MoveTowards(group.alpha, target, step);
    }

    void Build()
    {
        canvasGo = new GameObject("FuseCounterHUD");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;

        var row = new GameObject("Row", typeof(RectTransform));
        row.transform.SetParent(canvasGo.transform, false);
        var rowRt = (RectTransform)row.transform;
        rowRt.anchorMin = rowRt.anchorMax = rowRt.pivot = Vector2.zero;
        rowRt.anchoredPosition = margin;
        rowRt.sizeDelta = new Vector2(360f, fontSize + 8f);

        group = row.AddComponent<CanvasGroup>();
        group.alpha          = 0f;
        group.interactable   = false;
        group.blocksRaycasts = false;

        // Kucuk amber isaret — sigortanin isimasiyla ayni renk, sayacin neyi saydigini baglar.
        var mark = new GameObject("Mark", typeof(RectTransform));
        mark.transform.SetParent(row.transform, false);
        var markRt = (RectTransform)mark.transform;
        markRt.anchorMin = markRt.anchorMax = new Vector2(0f, 0.5f);
        markRt.pivot     = new Vector2(0f, 0.5f);
        markRt.anchoredPosition = Vector2.zero;
        markRt.sizeDelta = new Vector2(6f, fontSize * 0.7f);
        var img = mark.AddComponent<Image>();
        img.color = markColor;
        img.raycastTarget = false;

        var lbl = new GameObject("Label", typeof(RectTransform));
        lbl.transform.SetParent(row.transform, false);
        var lblRt = (RectTransform)lbl.transform;
        lblRt.anchorMin = Vector2.zero;
        lblRt.anchorMax = Vector2.one;
        lblRt.offsetMin = new Vector2(18f, 0f);
        lblRt.offsetMax = Vector2.zero;

        text = lbl.AddComponent<Text>();
        text.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize      = fontSize;
        text.color         = textColor;
        text.alignment     = TextAnchor.MiddleLeft;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow   = VerticalWrapMode.Overflow;
    }
}
}

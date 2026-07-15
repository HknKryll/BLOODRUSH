using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Duvardaki sunum ekranı: dünya-uzayı canvas'ta slayt slayt yazılar döngüler.
// Kullanım: boş GO'yu duvarda, ekranın olacağı yere koy; ekran DÜZ karşıya
// baksın diye GO'yu duvara paralel çevir (yazı +Z yönüne bakar). Bu script'i ekle.
// Görsel/video istersen aşağıdaki nota bak.
public class PresentationScreen : MonoBehaviour
{
    [Header("Ekran Boyutu (metre)")]
    [SerializeField] float width  = 6f;
    [SerializeField] float height = 3.5f;

    [Header("Slaytlar")]
    [TextArea]
    [SerializeField] string[] slides =
    {
        "PROJE ATLAS\nSAHA RAPORU",
        "OPERASYON BAŞARILI",
        "HEDEF NÖTRALİZE:  %100",
        "ETKİNLİK: BEKLENENİN ÜZERİNDE",
        "TEBRİKLER",
    };
    [SerializeField] float slideDuration = 4f;
    [SerializeField] float fadeDuration  = 0.6f;

    [Header("Renkler")]
    [Tooltip("Orta tonda tut. Çok parlak → Bloom beyaza yakar; çok koyu → simsiyah. 0.1-0.3 arası iyi.")]
    [SerializeField] Color screenColor = new Color(0.13f, 0.17f, 0.30f);  // görünür koyu mavi panel
    [SerializeField] Color textColor   = new Color(0.6f, 0.85f, 1f);      // okunur, hafif parlar

    const float PixelsPerMeter = 100f;

    Text      slideText;
    CanvasGroup textGroup;

    void Start()
    {
        BuildScreen();
        StartCoroutine(CycleSlides());
    }

    void BuildScreen()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        gameObject.AddComponent<CanvasScaler>();
        gameObject.AddComponent<GraphicRaycaster>();

        var rt = GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width, height) * PixelsPerMeter;
        // Dünya-uzayında 100px = 1m olacak şekilde ölçekle
        transform.localScale = Vector3.one * (1f / PixelsPerMeter);

        // Ekran arka planı (parlak → Bloom ile ışıldar)
        var bg = new GameObject("Screen").AddComponent<Image>();
        bg.transform.SetParent(transform, false);
        bg.color = screenColor;
        var bgRT = bg.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;

        // Slayt yazısı
        var txtGO = new GameObject("SlideText");
        txtGO.transform.SetParent(transform, false);
        textGroup = txtGO.AddComponent<CanvasGroup>();
        slideText = txtGO.AddComponent<Text>();
        slideText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        slideText.fontSize  = 64;
        slideText.fontStyle = FontStyle.Bold;
        slideText.color     = textColor;
        slideText.alignment = TextAnchor.MiddleCenter;
        slideText.horizontalOverflow = HorizontalWrapMode.Wrap;
        slideText.verticalOverflow   = VerticalWrapMode.Overflow;
        var txtRT = slideText.GetComponent<RectTransform>();
        txtRT.anchorMin = new Vector2(0.06f, 0.06f);
        txtRT.anchorMax = new Vector2(0.94f, 0.94f);
        txtRT.offsetMin = txtRT.offsetMax = Vector2.zero;
    }

    IEnumerator CycleSlides()
    {
        if (slides == null || slides.Length == 0) yield break;

        int i = 0;
        while (true)
        {
            slideText.text = slides[i];

            yield return Fade(0f, 1f);                              // belir
            yield return new WaitForSeconds(slideDuration);         // bekle
            yield return Fade(1f, 0f);                              // kaybol

            i = (i + 1) % slides.Length;
        }
    }

    IEnumerator Fade(float from, float to)
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            textGroup.alpha = Mathf.Lerp(from, to, t / fadeDuration);
            yield return null;
        }
        textGroup.alpha = to;
    }
}

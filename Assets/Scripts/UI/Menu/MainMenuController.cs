using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Bloodrush.Flow;
using Bloodrush.Settings;
using Bloodrush.Shared.Audio;

namespace Bloodrush.UI.Menu
{
// VERITAS ana menusu. Kurumsal sunum estetigi: krem zemin, bol bosluk, ince ayrac,
// geniş harf aralikli baslik. Kutu/cerceve yok — butonlar metin, vurgu solda ince
// altin cubuk.
//
// Eski MainMenuManager SILINMEDI; bu bilesen sahnede onun yerine konur ve calisirken
// eskisini bulup kapatir (ikisi birden UI kurmasin).
public class MainMenuController : MonoBehaviour
{
    [Header("Akış")]
    [SerializeField] string firstSceneName    = "CH1";
    [SerializeField] float  exitFadeDuration  = 1.5f;
    [SerializeField] float  nextSceneFadeIn   = 0.6f;

    [Header("Müzik")]
    [Tooltip("Boş bırakılırsa sahnede aranır.")]
    [SerializeField] RoomMusic menuMusic;

    MenuTheme theme;
    Canvas    canvas;
    RectTransform root;
    CanvasGroup   group;

    readonly List<MenuButton> buttons = new List<MenuButton>();
    bool starting;

    void Start()
    {
        // Eski menu ayni sahnedeyse sustur — iki UI ust uste binmesin.
        var legacy = FindFirstObjectByType<MainMenuManager>();
        if (legacy != null && legacy.enabled)
        {
            legacy.enabled = false;
            Debug.Log("[MainMenuController] Eski MainMenuManager kapatıldı.");
        }

        theme = MenuTheme.Load();
        MenuNavigation.Ensure();
        UISounds.Ensure();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        Build();
        StartCoroutine(AnimateIn());
    }

    void Build()
    {
        canvas = MenuUI.CreateCanvas("MainMenuCanvas", 0);
        canvas.transform.SetParent(transform, false);

        MenuUI.Fill(canvas.transform, "BG", theme.background);

        root  = MenuUI.Stretch(canvas.transform, "Root");
        group = root.gameObject.AddComponent<CanvasGroup>();

        // ── Sol blok: üst etiket + başlık ──
        float left = 160f;

        var capRT = MenuUI.Box(root, "Caption", new Vector2(0f, 0.5f),
                               new Vector2(left + 140f, 190f), new Vector2(280f, 20f));
        MenuUI.Text(capRT, "VERITAS INITIATIVE", theme.sizeCaption, theme.textSecondary,
                    theme.trackingCaption, TextAlignmentOptions.Left, theme.bodyFont);

        var titleRT = MenuUI.Box(root, "Title", new Vector2(0f, 0.5f),
                                 new Vector2(left + 330f, 130f), new Vector2(660f, 80f));
        MenuUI.Text(titleRT, "BLOODRUSH", theme.sizeTitle, theme.textPrimary,
                    theme.trackingTitle, TextAlignmentOptions.Left, theme.displayFont,
                    FontStyles.Normal);

        // İnce ayraç — başlıkla menüyü ayırır
        MenuUI.Hairline(root, theme, new Vector2(0f, 0.5f),
                        new Vector2(left + 200f, 80f), 400f);

        // ── Butonlar ──
        float y = 20f;
        AddButton("OYNA", ref y, StartGame);

        var continueBtn = AddButton("DEVAM ET", ref y, ContinueGame);
        // Kayıt yoksa soluk ve tıklanamaz — yalancı buton olmasın.
        continueBtn.SetInteractable(SettingsStore.HasContinue);

        AddButton("AYARLAR", ref y, OpenSettings);
        AddButton("KREDİLER", ref y, OpenCredits);
        AddButton("ÇIKIŞ", ref y, QuitGame);

        var sels = new Selectable[buttons.Count];
        for (int i = 0; i < buttons.Count; i++) sels[i] = buttons[i].GetComponent<Selectable>();
        MenuNavigation.LinkVertical(sels);

        // Sağ altta sürüm/kod etiketi — kurumsal belge hissi
        var verRT = MenuUI.Box(root, "Ver", new Vector2(1f, 0f),
                               new Vector2(-190f, 48f), new Vector2(320f, 18f));
        MenuUI.Text(verRT, "SEC.1  ·  INTERNAL BUILD", theme.sizeCaption, theme.textSecondary,
                    theme.trackingCaption, TextAlignmentOptions.Right, theme.bodyFont);
    }

    MenuButton AddButton(string label, ref float y, System.Action action)
    {
        var btn = MenuUI.Button(root, theme, label, new Vector2(0f, 0.5f),
                                new Vector2(160f + theme.buttonSize.x * 0.5f, y),
                                theme.buttonSize, action);
        btn.OnHighlighted += UISounds.Hover;
        buttons.Add(btn);
        y -= theme.buttonSize.y + theme.U1;
        return btn;
    }

    IEnumerator AnimateIn()
    {
        // Başlık önce, butonlar kademeli — hepsi fade + kısa kayma, zıplama yok.
        yield return null;
        for (int i = 0; i < buttons.Count; i++)
        {
            var cg = buttons[i].gameObject.GetComponent<CanvasGroup>();
            if (cg == null) cg = buttons[i].gameObject.AddComponent<CanvasGroup>();
            StartCoroutine(MenuUI.FadeSlideIn(cg, buttons[i].GetComponent<RectTransform>(),
                theme.slideDistance, theme.enterDuration, theme.stagger * i));
        }

        yield return new WaitForSecondsRealtime(theme.stagger * buttons.Count);
        MenuNavigation.Focus(buttons.Count > 0 ? buttons[0].gameObject : null);
    }

    // ───────────────── Aksiyonlar ─────────────────

    void StartGame()
    {
        UISounds.Click();
        GameHUD.ResetProgress();
        LoadScene(firstSceneName);
    }

    void ContinueGame()
    {
        if (!SettingsStore.HasContinue) return;
        UISounds.Click();
        LoadScene(SettingsStore.Current.lastChapter);
    }

    void OpenSettings()
    {
        UISounds.Click();
        SettingsPanel.Open(() => MenuNavigation.Focus(buttons.Count > 2 ? buttons[2].gameObject : null));
    }

    void OpenCredits()
    {
        UISounds.Click();
        CreditsPanel.Open(() => MenuNavigation.Focus(buttons.Count > 3 ? buttons[3].gameObject : null));
    }

    void QuitGame()
    {
        UISounds.Click();
        ConfirmDialog.Ask("ÇIKIŞ", "Oyundan çıkmak istediğine emin misin?", "ÇIK", () =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });
    }

    // ───────────────── Sahne geçişi ─────────────────

    void LoadScene(string sceneName)
    {
        if (starting) return;
        starting = true;
        StartCoroutine(LoadRoutine(sceneName));
    }

    // Müzik sönerken sahne ARKA PLANDA yüklenir — 1.5 sn bir bekleme değil, zaten
    // harcanacak yükleme süresinin üstüne biniyor. Perde SceneFadeIn ile taşınır.
    IEnumerator LoadRoutine(string sceneName)
    {
        var music = menuMusic != null ? menuMusic : FindFirstObjectByType<RoomMusic>();
        if (music != null) music.FadeOut(exitFadeDuration);

        var op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null)
        {
            Debug.LogError($"[MainMenuController] '{sceneName}' yüklenemedi — Build Settings'te ekli mi?", this);
            starting = false;
            yield break;
        }
        op.allowSceneActivation = false;

        var img = GameFlow.CreateOverlay(Color.black);
        float t = 0f;
        while (t < exitFadeDuration)
        {
            t += Time.unscaledDeltaTime;
            img.color = new Color(0f, 0f, 0f, Mathf.Clamp01(t / exitFadeDuration));
            yield return null;
        }
        img.color = Color.black;

        while (op.progress < 0.9f) yield return null;

        SceneFadeIn.CarryOverlay(img, 0f, nextSceneFadeIn);
        op.allowSceneActivation = true;
    }
}
}

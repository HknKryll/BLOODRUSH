using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Bloodrush.Flow;

namespace Bloodrush.UI.Menu
{
// VERITAS pause menusu — ana menuyle ayni dil, daha kompakt.
//
// KENDINI KURAR: menuyu prefab'a eklemek yerine [RuntimeInitializeOnLoadMethod] ile
// kendimiz olusturuyoruz. Boylece HICBIR sahneye ve prefab'a dokunmak gerekmiyor.
// (Eski, Player.prefab uzerindeki legacy PauseMenu bileseni ve onu kapatan
// DisableLegacy() mantigi kaldirildi — legacy bilesen artik hic mevcut degil.)
public class PauseMenuController : MonoBehaviour
{
    const int SortingOrder = 200;

    static PauseMenuController instance;

    MenuTheme     theme;
    Canvas        canvas;
    RectTransform panel;
    CanvasGroup   group;
    GameObject    backdropGO;
    PauseBackdrop backdrop;

    readonly List<MenuButton> buttons = new List<MenuButton>();
    bool isPaused;
    bool busy;

    public static bool IsPaused => instance != null && instance.isPaused;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject("PauseMenuController");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<PauseMenuController>();
    }

    void Awake()
    {
        theme = MenuTheme.Load();
    }

    void Update()
    {
        // Ana menude pause olmaz.
        if (SceneManager.GetActiveScene().name == "MainMenu") return;
        if (busy) return;

        // UST KATMAN ACIKKEN ESC ONUN. Eskiden sadece SettingsPanel'e bakiliyordu:
        //  - Onay penceresinde ESC hem pencereyi kapatiyor hem oyunu devam ettiriyordu.
        //  - Kitap okurken ESC pause'u acip, kitabin kapanisi imleci KILITLIYORDU.
        // BookSession Flow domain'inde; UI -> Flow bagimliligi projede zaten mevcut
        // (MainMenuController GameFlow/SceneFadeIn kullaniyor).
        if (SettingsPanel.IsOpen || ConfirmDialog.IsOpen || BookSession.IsOpen) return;

        var kb = UnityEngine.InputSystem.Keyboard.current;
        var gp = UnityEngine.InputSystem.Gamepad.current;
        bool pressed = (kb != null && kb.escapeKey.wasPressedThisFrame) ||
                       (gp != null && gp.startButton.wasPressedThisFrame);
        if (!pressed) return;

        // Update sirasi tanimsiz: bir modal bu karede ONCE kosup ESC'yi alip kapandiysa
        // IsOpen artik false gorunur — hakem o durumda ESC'yi bize vermez.
        if (!InteractionInput.TryConsumeEscape()) return;

        if (isPaused) Resume(); else Pause();
    }

    // ───────────────── Aç / Kapat ─────────────────

    void Pause()
    {
        isPaused = true;
        Time.timeScale   = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        MenuNavigation.Ensure();
        UISounds.Ensure();
        Build();
    }

    void Resume()
    {
        if (busy) return;
        busy = true;
        UISounds.Back();
        StartCoroutine(ResumeRoutine());
    }

    IEnumerator ResumeRoutine()
    {
        // Panel ve arka plan AYNI ANDA soner; uzun olan beklenir. Imlec ancak ikisi de
        // bittikten sonra kilitlenir — menu hala gorunurken fare kaybolmasin.
        var panelFade = StartCoroutine(MenuUI.FadeOut(group, theme.exitDuration));
        if (backdrop != null) yield return backdrop.FadeOut();
        yield return panelFade;

        Teardown();
        Time.timeScale   = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        isPaused = false;
        busy = false;
    }

    void Teardown()
    {
        if (canvas != null) Destroy(canvas.gameObject);
        if (backdropGO != null) Destroy(backdropGO);
        canvas = null; backdropGO = null; backdrop = null; panel = null; group = null;
        buttons.Clear();
    }

    // ───────────────── Arayüz ─────────────────

    void Build()
    {
        // Arka plan KENDI canvas'inda (sortingOrder 190) — HUD'un onunde, panelin arkasinda.
        // Duz renk / sprite / opaklik / fade suresi MenuTheme asset'inden gelir.
        backdropGO = new GameObject("PauseBackdrop");
        backdropGO.transform.SetParent(transform, false);
        backdrop = backdropGO.AddComponent<PauseBackdrop>();
        backdrop.Build(theme);
        backdrop.FadeIn();

        canvas = MenuUI.CreateCanvas("PauseCanvas", SortingOrder);
        canvas.transform.SetParent(transform, false);

        panel = MenuUI.Box(canvas.transform, "Panel", new Vector2(0.5f, 0.5f),
                           Vector2.zero, new Vector2(420f, 440f));
        panel.gameObject.AddComponent<Image>().color = theme.background;
        group = panel.gameObject.AddComponent<CanvasGroup>();

        MenuUI.Box(panel, "TopRule", new Vector2(0.5f, 1f), new Vector2(0f, -1f), new Vector2(420f, 2f))
              .gameObject.AddComponent<Image>().color = theme.accent;

        var capRT = MenuUI.Box(panel, "Caption", new Vector2(0.5f, 1f),
                               new Vector2(0f, -theme.U5), new Vector2(320f, 18f));
        MenuUI.Text(capRT, "VERITAS INITIATIVE", theme.sizeCaption, theme.textSecondary,
                    theme.trackingCaption, TextAlignmentOptions.Center, theme.bodyFont);

        var titleRT = MenuUI.Box(panel, "Title", new Vector2(0.5f, 1f),
                                 new Vector2(0f, -theme.U8 - 6f), new Vector2(360f, 40f));
        MenuUI.Text(titleRT, "DURAKLATILDI", theme.sizeHeading * 0.78f, theme.textPrimary,
                    theme.trackingCaption, TextAlignmentOptions.Center, theme.displayFont);

        MenuUI.Hairline(panel, theme, new Vector2(0.5f, 1f), new Vector2(0f, -140f), 320f);

        buttons.Clear();
        float y = -190f;
        Add("DEVAM ET",       ref y, Resume);
        Add("AYARLAR",        ref y, OpenSettings);
        Add("ANA MENÜYE DÖN", ref y, AskMainMenu);
        Add("ÇIKIŞ",          ref y, AskQuit);

        var sels = new Selectable[buttons.Count];
        for (int i = 0; i < buttons.Count; i++) sels[i] = buttons[i].GetComponent<Selectable>();
        MenuNavigation.LinkVertical(sels);

        StartCoroutine(MenuUI.FadeSlideIn(group, panel, theme.slideDistance, theme.enterDuration));
        MenuNavigation.Focus(buttons[0].gameObject);
    }

    void Add(string label, ref float y, System.Action action)
    {
        var btn = MenuUI.Button(panel, theme, label, new Vector2(0.5f, 1f),
                                new Vector2(0f, y), new Vector2(300f, 44f), action,
                                TextAlignmentOptions.Center);
        btn.OnHighlighted += UISounds.Hover;
        buttons.Add(btn);
        y -= 52f;
    }

    // ───────────────── Aksiyonlar ─────────────────

    void OpenSettings()
    {
        UISounds.Click();
        // AYNI panel — ana menüdeki ile birebir aynı sınıf, ayrı kopya yok.
        SettingsPanel.Open(() => MenuNavigation.Focus(buttons.Count > 1 ? buttons[1].gameObject : null));
    }

    void AskMainMenu()
    {
        UISounds.Click();
        ConfirmDialog.Ask("ANA MENÜ",
            "Ana menüye dönmek istediğine emin misin?\nKaydedilmemiş ilerleme kaybolur.",
            "DÖN", () =>
            {
                Teardown();
                isPaused = false;
                Time.timeScale = 1f;
                SceneManager.LoadScene("MainMenu");
            });
    }

    void AskQuit()
    {
        UISounds.Click();
        ConfirmDialog.Ask("ÇIKIŞ", "Oyundan çıkmak istediğine emin misin?", "ÇIK", () =>
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });
    }
}
}

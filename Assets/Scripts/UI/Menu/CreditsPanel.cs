using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Bloodrush.UI.Menu
{
// Krediler — ayarlar paneliyle ayni gorsel dilde, tek sutun, bol bosluk.
// Icerigi degistirmek icin asagidaki Entries dizisini duzenlemen yeterli.
public class CreditsPanel : MonoBehaviour
{
    const int SortingOrder = 300;

    static CreditsPanel active;

    static readonly (string role, string name)[] Entries =
    {
        ("TASARIM & PROGRAMLAMA", "Hakan Paçal"),
        ("GELİŞTİRME",           "BLOODRUSH Ekibi"),
        ("MOTOR",                "Unity 2022.3 · HDRP"),
        ("SES",                  "Freesound (CC0)"),
        ("İKONLAR",              "game-icons.net"),
    };

    MenuTheme   theme;
    CanvasGroup group;
    RectTransform panel;
    Action onClosed;
    GameObject previousSelection;

    public static void Open(Action onClosed = null)
    {
        if (active != null) return;
        var go = new GameObject("CreditsPanel");
        var cp = go.AddComponent<CreditsPanel>();
        cp.onClosed = onClosed;
        cp.Build();
        active = cp;
    }

    void Build()
    {
        theme = MenuTheme.Load();
        MenuNavigation.Ensure();

        previousSelection = UnityEngine.EventSystems.EventSystem.current != null
            ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject : null;

        var canvas = MenuUI.CreateCanvas("CreditsCanvas", SortingOrder);
        canvas.transform.SetParent(transform, false);

        MenuUI.Fill(canvas.transform, "Scrim", new Color(0.165f, 0.165f, 0.157f, 0.35f))
              .raycastTarget = true;

        panel = MenuUI.Box(canvas.transform, "Panel", new Vector2(0.5f, 0.5f),
                           Vector2.zero, new Vector2(640f, 560f));
        panel.gameObject.AddComponent<Image>().color = theme.background;
        group = panel.gameObject.AddComponent<CanvasGroup>();

        MenuUI.Box(panel, "TopRule", new Vector2(0.5f, 1f), new Vector2(0f, -1f), new Vector2(640f, 2f))
              .gameObject.AddComponent<Image>().color = theme.accent;

        var capRT = MenuUI.Box(panel, "Caption", new Vector2(0.5f, 1f), new Vector2(0f, -theme.U5), new Vector2(400f, 18f));
        MenuUI.Text(capRT, "VERITAS INITIATIVE", theme.sizeCaption, theme.textSecondary,
                    theme.trackingCaption, TextAlignmentOptions.Center, theme.bodyFont);

        var titleRT = MenuUI.Box(panel, "Title", new Vector2(0.5f, 1f), new Vector2(0f, -theme.U8 - 10f), new Vector2(500f, 44f));
        MenuUI.Text(titleRT, "KREDİLER", theme.sizeHeading, theme.textPrimary,
                    theme.trackingCaption, TextAlignmentOptions.Center, theme.displayFont);

        float y = -170f;
        foreach (var (role, name) in Entries)
        {
            var roleRT = MenuUI.Box(panel, "Role", new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(520f, 16f));
            MenuUI.Text(roleRT, role, theme.sizeCaption, theme.accentText, theme.trackingCaption,
                        TextAlignmentOptions.Center, theme.bodyFont);
            y -= 24f;

            var nameRT = MenuUI.Box(panel, "Name", new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(520f, 24f));
            MenuUI.Text(nameRT, name, theme.sizeLabel, theme.textPrimary, 2f,
                        TextAlignmentOptions.Center, theme.bodyFont);
            y -= 46f;
        }

        MenuUI.Hairline(panel, theme, new Vector2(0.5f, 0f), new Vector2(0f, 80f), 520f);

        var back = MenuUI.Button(panel, theme, "◄  GERİ", new Vector2(0.5f, 0f),
                                 new Vector2(0f, theme.U5), new Vector2(220f, 42f), Close,
                                 TextAlignmentOptions.Center);
        MenuNavigation.Focus(back.gameObject);

        StartCoroutine(MenuUI.FadeSlideIn(group, panel, theme.slideDistance, theme.enterDuration));
    }

    void Update()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        var gp = UnityEngine.InputSystem.Gamepad.current;
        // Diger modallarla ayni kural: ESC hakemden alinir (bkz. InteractionInput).
        bool esc = kb != null && kb.escapeKey.wasPressedThisFrame &&
                   Flow.InteractionInput.TryConsumeEscape();
        if (esc || (gp != null && gp.buttonEast.wasPressedThisFrame))
            Close();
    }

    void Close()
    {
        if (active == this) active = null;
        UISounds.Back();
        StartCoroutine(CloseRoutine());
    }

    IEnumerator CloseRoutine()
    {
        yield return MenuUI.FadeOut(group, theme.exitDuration);
        var cb = onClosed;
        if (previousSelection != null) MenuNavigation.Focus(previousSelection);
        Destroy(gameObject);
        cb?.Invoke();
    }
}
}

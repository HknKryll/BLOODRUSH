using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace Bloodrush.UI.Menu
{
// Tek bir yeniden kullanilabilir onay penceresi. Iki isi var:
//   1) Yikici aksiyonlar (Ana Menuye Don, Cikis) — "emin misin?"
//   2) Geri donusu zor ayarlar (cozunurluk, ekran modu) — 10 sn geri sayan
//      "eski haline don". Kullanici hicbir sey yapmazsa OTOMATIK geri alinir;
//      ekran bozuk acilip oyuncuyu kilitlemesin diye.
//
// Kendi canvas'ini kurar (sortingOrder yuksek), acikken odagi kendine alir ve
// kapaninca cagirana geri verir.
public class ConfirmDialog : MonoBehaviour
{
    const int SortingOrder = 400;

    static ConfirmDialog active;

    // PauseMenuController acikken ESC'yi dinlemesin diye (onay penceresinde ESC hem
    // pencereyi kapatip hem oyunu devam ettiriyordu).
    public static bool IsOpen => active != null;

    MenuTheme  theme;
    CanvasGroup group;
    GameObject  previousSelection;
    Coroutine   countdown;

    TextMeshProUGUI bodyText;
    string          bodyTemplate;
    Action          onConfirm;
    Action          onCancel;

    // Yikici aksiyon onayi.
    public static void Ask(string title, string body, string confirmLabel, Action onConfirm,
                           bool destructive = true)
        => Create(title, body, confirmLabel, "VAZGEÇ", onConfirm, null, 0f, destructive);

    // Geri sayimli geri alma. Sure dolunca onRevert calisir.
    public static void AskWithRevert(string title, string bodyWithSeconds, string keepLabel,
                                     Action onKeep, Action onRevert, float seconds)
        => Create(title, bodyWithSeconds, keepLabel, "GERİ AL", onKeep, onRevert, seconds, false);

    static void Create(string title, string body, string confirmLabel, string cancelLabel,
                       Action onConfirm, Action onCancel, float seconds, bool destructive)
    {
        if (active != null) active.Close();

        var go = new GameObject("ConfirmDialog");
        var dlg = go.AddComponent<ConfirmDialog>();
        dlg.Build(title, body, confirmLabel, cancelLabel, onConfirm, onCancel, seconds, destructive);
        active = dlg;
    }

    void Build(string title, string body, string confirmLabel, string cancelLabel,
               Action confirm, Action cancel, float seconds, bool destructive)
    {
        theme        = MenuTheme.Load();
        onConfirm    = confirm;
        onCancel     = cancel;
        bodyTemplate = body;

        previousSelection = EventSystem.current != null
            ? EventSystem.current.currentSelectedGameObject : null;

        var canvas = MenuUI.CreateCanvas("ConfirmCanvas", SortingOrder);
        canvas.transform.SetParent(transform, false);

        // Perde — arkadaki menuyu bastirir (skill: modal scrim).
        var scrim = MenuUI.Fill(canvas.transform, "Scrim", new Color(0.165f, 0.165f, 0.157f, 0.55f));
        scrim.raycastTarget = true;

        var panel = MenuUI.Box(canvas.transform, "Panel", new Vector2(0.5f, 0.5f),
                               Vector2.zero, new Vector2(560f, 260f));
        var panelBg = panel.gameObject.AddComponent<Image>();
        panelBg.color = theme.surface;

        group = panel.gameObject.AddComponent<CanvasGroup>();

        // Ust altin cizgi — VERITAS dili
        var top = MenuUI.Box(panel, "TopRule", new Vector2(0.5f, 1f), new Vector2(0f, -1f), new Vector2(560f, 2f));
        top.gameObject.AddComponent<Image>().color = theme.accent;

        var titleRT = MenuUI.Box(panel, "Title", new Vector2(0.5f, 1f), new Vector2(0f, -theme.U5), new Vector2(480f, 32f));
        MenuUI.Text(titleRT, title, theme.sizeHeading * 0.62f, theme.textPrimary,
                    theme.trackingCaption, TextAlignmentOptions.Center, theme.displayFont);

        var bodyRT = MenuUI.Box(panel, "Body", new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(480f, 72f));
        bodyText = MenuUI.Text(bodyRT, body, theme.sizeBody, theme.textSecondary, 1f,
                               TextAlignmentOptions.Center, theme.bodyFont);
        bodyText.enableWordWrapping = true;

        MenuUI.Hairline(panel, theme, new Vector2(0.5f, 0f), new Vector2(0f, 86f), 480f);

        var confirmBtn = MenuUI.Button(panel, theme, confirmLabel, new Vector2(0.5f, 0f),
            new Vector2(-120f, theme.U5), new Vector2(200f, 44f), Confirm,
            TextAlignmentOptions.Center, destructive ? theme.destructive : theme.textPrimary);

        var cancelBtn = MenuUI.Button(panel, theme, cancelLabel, new Vector2(0.5f, 0f),
            new Vector2(120f, theme.U5), new Vector2(200f, 44f), Cancel,
            TextAlignmentOptions.Center);

        MenuNavigation.LinkHorizontal(confirmBtn.GetComponent<Selectable>(),
                                      cancelBtn.GetComponent<Selectable>());

        // Yikici aksiyonlarda varsayilan odak VAZGEÇ'te olsun — yanlislikla Enter'a
        // basan oyuncu oyunundan olmasin.
        MenuNavigation.Focus(destructive ? cancelBtn.gameObject : confirmBtn.gameObject);

        StartCoroutine(MenuUI.FadeSlideIn(group, panel, theme.slideDistance, theme.enterDuration));

        if (seconds > 0f) countdown = StartCoroutine(Countdown(seconds));
    }

    // Geri sayim: sure dolunca CANCEL yolu (yani geri alma) calisir.
    IEnumerator Countdown(float seconds)
    {
        float left = seconds;
        while (left > 0f)
        {
            if (bodyText != null)
                bodyText.text = bodyTemplate.Replace("{0}", Mathf.CeilToInt(left).ToString());
            left -= Time.unscaledDeltaTime;
            yield return null;
        }
        countdown = null;
        Cancel();
    }

    void Confirm()
    {
        UISounds.Click();
        var cb = onConfirm; onConfirm = null; onCancel = null;
        Close();
        cb?.Invoke();
    }

    void Cancel()
    {
        UISounds.Back();
        var cb = onCancel; onConfirm = null; onCancel = null;
        Close();
        cb?.Invoke();
    }

    void Update()
    {
        // Esc / gamepad B ile iptal — modal escape (skill: escape-routes).
        // TryConsumeEscape: bu ESC'nin ayni karede pause'u da kapatip oyunu devam
        // ettirmesini engeller (bkz. InteractionInput).
        if (UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame &&
            Bloodrush.Flow.InteractionInput.TryConsumeEscape())
            Cancel();
        else if (UnityEngine.InputSystem.Gamepad.current != null &&
                 UnityEngine.InputSystem.Gamepad.current.buttonEast.wasPressedThisFrame)
            Cancel();
    }

    void Close()
    {
        if (countdown != null) { StopCoroutine(countdown); countdown = null; }
        if (active == this) active = null;
        if (previousSelection != null) MenuNavigation.Focus(previousSelection);
        Destroy(gameObject);
    }
}
}

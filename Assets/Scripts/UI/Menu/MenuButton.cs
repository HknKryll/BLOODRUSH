using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace Bloodrush.UI.Menu
{
// VERITAS butonu: kutu yok, sadece metin + hover'da solda beliren ince altın çubuk ve
// çok hafif bir dolgu. Fare, klavye ve gamepad için AYNI görsel dili kullanır —
// EventSystem seçimi (ISelectHandler) ile fare hover'ı aynı duruma bağlıdır, böylece
// gamepad'le gezerken de nerede olduğun bellidir.
[RequireComponent(typeof(Button))]
public class MenuButton : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    Image           fill;
    Image           bar;
    CanvasGroup     ring;
    TextMeshProUGUI label;
    MenuTheme       theme;
    Button          button;

    bool hovered, selected, activeTab;
    Color baseTextColor;
    float t;                 // 0..1 vurgu miktarı

    public event Action OnHighlighted;   // UISounds buna bağlanır

    public void Init(MenuTheme theme, Image fill, Image bar, CanvasGroup ring, TextMeshProUGUI label)
    {
        this.theme = theme;
        this.fill  = fill;
        this.bar   = bar;
        this.ring  = ring;
        this.label = label;
        button     = GetComponent<Button>();
        baseTextColor = label != null ? label.color : Color.black;
        ApplyInstant(0f);
    }

    // Sekme listesinde AKTIF sekmeyi kalici olarak isaretler: altin cubuk hep acik,
    // metin altin. Dolu blok DEGIL — kurumsal dil ince kalmali.
    public void SetActiveTab(bool value)
    {
        activeTab = value;
        Apply(t);
    }

    void Update()
    {
        bool on = (hovered || selected) && button != null && button.interactable;
        // Hızlı ve yumuşak: zıplama/esneme yok, sadece doğrusal bir yaklaşma.
        float speed = 1f / Mathf.Max(0.01f, on ? theme.enterDuration : theme.exitDuration);
        t = Mathf.MoveTowards(t, on ? 1f : 0f, Time.unscaledDeltaTime * speed);
        Apply(t);
    }

    void ApplyInstant(float v) { t = v; Apply(v); }

    void Apply(float v)
    {
        // Aktif sekme, fare/odak olmasa da vurgulu görünür.
        float vis = Mathf.Max(v, activeTab ? 1f : 0f);

        if (fill != null)
        {
            var c = theme.hoverFill; c.a = theme.hoverFill.a * vis;
            fill.color = c;
        }
        if (bar != null)
        {
            var c = theme.accent; c.a = vis;
            bar.color = c;
            // Çubuk yukarıdan aşağı açılır — küçük ama anlamlı bir hareket.
            bar.rectTransform.localScale = new Vector3(1f, Mathf.Lerp(0.2f, 1f, vis), 1f);
        }
        // Odak halkası SADECE klavye/gamepad seçiminde — farede değil.
        if (ring != null) ring.alpha = selected ? 1f : 0f;

        if (label != null)
            label.color = Color.Lerp(baseTextColor, theme.accentText, vis);
    }

    public void SetInteractable(bool value)
    {
        if (button != null) button.interactable = value;
        var group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = value ? 1f : theme.disabledAlpha;
        group.blocksRaycasts = value;
    }

    void Highlight()
    {
        if (button != null && !button.interactable) return;
        OnHighlighted?.Invoke();
    }

    public void OnPointerEnter(PointerEventData e)
    {
        hovered = true;
        // Fareyle üstüne gelmek EventSystem seçimini de taşısın: gamepad ve fare
        // arasında gidip gelirken odak zıplamasın.
        if (button != null && button.interactable && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(gameObject);
        Highlight();
    }

    public void OnPointerExit(PointerEventData e)  => hovered = false;

    public void OnSelect(BaseEventData e)
    {
        selected = true;
        Highlight();
    }

    public void OnDeselect(BaseEventData e) => selected = false;
}
}

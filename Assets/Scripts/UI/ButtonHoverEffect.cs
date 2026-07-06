using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ButtonHoverEffect : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler,  IPointerUpHandler
{
    public Image fillImage;
    public Color fillColor;

    float current;
    float target;

    void Update()
    {
        current = Mathf.Lerp(current, target, Time.unscaledDeltaTime * 10f);
        if (fillImage)
            fillImage.color = new Color(fillColor.r, fillColor.g, fillColor.b, current);
    }

    public void OnPointerEnter(PointerEventData e) => target = fillColor.a;
    public void OnPointerExit(PointerEventData e)  => target = 0f;
    public void OnPointerDown(PointerEventData e)  => target = fillColor.a * 1.6f;
    public void OnPointerUp(PointerEventData e)    => target = fillColor.a;
}

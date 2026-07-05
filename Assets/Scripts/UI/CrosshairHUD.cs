using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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

    readonly List<GameObject> hitLines = new();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        gameObject.AddComponent<CanvasScaler>();
        gameObject.AddComponent<GraphicRaycaster>();

        // Normal crosshair (+)
        MakeLine(new Vector2( gap + lineSize * 0.5f,  0), new Vector2(lineSize, thickness), 0f, color);
        MakeLine(new Vector2(-(gap + lineSize * 0.5f), 0), new Vector2(lineSize, thickness), 0f, color);
        MakeLine(new Vector2( 0,  gap + lineSize * 0.5f),  new Vector2(lineSize, thickness), 90f, color);
        MakeLine(new Vector2( 0, -(gap + lineSize * 0.5f)), new Vector2(lineSize, thickness), 90f, color);

        if (centerDot)
            MakeLine(Vector2.zero, new Vector2(thickness, thickness), 0f, color);

        // Hit marker (X) — başta gizli
        float d = (gap + lineSize * 0.5f) * 0.707f;
        hitLines.Add(MakeLine(new Vector2( d,  d), new Vector2(lineSize, thickness),  45f, hitColor, false));
        hitLines.Add(MakeLine(new Vector2(-d,  d), new Vector2(lineSize, thickness), -45f, hitColor, false));
        hitLines.Add(MakeLine(new Vector2( d, -d), new Vector2(lineSize, thickness), -45f, hitColor, false));
        hitLines.Add(MakeLine(new Vector2(-d, -d), new Vector2(lineSize, thickness),  45f, hitColor, false));
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

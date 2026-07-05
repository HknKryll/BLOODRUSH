using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WeaponHUD : MonoBehaviour
{
    [SerializeField] PlayerShoot shoot;

    TextMeshProUGUI ammoText;    // revolver: "10 | 50"
    TextMeshProUGUI grenText;    // "GRN  x6"
    TextMeshProUGUI flashText;   // "FLS  x3"
    TextMeshProUGUI modeText;    // "[ ANCHOR ]"
    TextMeshProUGUI reloadText;  // "RELOADING..."

    static readonly Color ColActive  = Color.yellow;
    static readonly Color ColCyan    = new Color(0.4f, 0.9f, 1f);
    static readonly Color ColDim     = new Color(0.35f, 0.35f, 0.35f);
    static readonly Color ColWarn    = new Color(1f, 0.6f, 0f);
    static readonly Color ColDanger  = Color.red;
    static readonly Color ColWhite   = Color.white;

    void Start()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        gameObject.AddComponent<CanvasScaler>();
        gameObject.AddComponent<GraphicRaycaster>();

        // Sağ alt köşe — yukarıdan aşağı: mod, reload, grenade, flash, revolver
        modeText   = MakeText(new Vector2(-18, 130), 15, TextAlignmentOptions.Right);
        reloadText = MakeText(new Vector2(-18,  98), 20, TextAlignmentOptions.Right);
        grenText   = MakeText(new Vector2(-18,  72), 18, TextAlignmentOptions.Right);
        flashText  = MakeText(new Vector2(-18,  46), 18, TextAlignmentOptions.Right);
        ammoText   = MakeText(new Vector2(-18,  14), 28, TextAlignmentOptions.Right);

        reloadText.text  = "";
        reloadText.color = ColWarn;
    }

    void Update()
    {
        if (shoot == null) return;

        var m = shoot.CurrentMode;

        // Revolver
        if (shoot.IsReloading)
        {
            reloadText.text  = "RELOADING...";
            ammoText.text    = $"{shoot.CurrentAmmo}  |  {shoot.TotalAmmo}";
            ammoText.color   = ColDim;
        }
        else
        {
            reloadText.text  = "";
            ammoText.text    = $"{shoot.CurrentAmmo}  |  {shoot.TotalAmmo}";
            ammoText.color   = shoot.CurrentAmmo == 0   ? ColDanger
                             : shoot.CurrentAmmo <= 3   ? ColWarn
                             : ColWhite;
        }

        // Launcher ammo
        grenText.text  = $"GRN   x{shoot.GrenadeAmmo}";
        flashText.text = $"FLS   x{shoot.FlashAmmo}";

        grenText.color  = m == PlayerShoot.LauncherMode.Grenade ? ColActive : ColDim;
        flashText.color = m == PlayerShoot.LauncherMode.Flash   ? ColCyan   : ColDim;

        // Mod göstergesi (anchor aktifken)
        modeText.text  = m == PlayerShoot.LauncherMode.Anchor ? "[ ANCHOR ]" : "";
        modeText.color = new Color(0.5f, 1f, 0.5f);
    }

    TextMeshProUGUI MakeText(Vector2 pos, float size, TextAlignmentOptions align)
    {
        var go = new GameObject("_hud");
        go.transform.SetParent(transform, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(1f, 0f);
        rt.anchorMax        = new Vector2(1f, 0f);
        rt.pivot            = new Vector2(1f, 0f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = new Vector2(220f, 40f);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize        = size;
        tmp.alignment       = align;
        tmp.color           = ColWhite;
        tmp.fontStyle       = FontStyles.Bold;
        tmp.characterSpacing = 3f;
        return tmp;
    }
}

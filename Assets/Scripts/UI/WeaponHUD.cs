using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Bloodrush.Player;

namespace Bloodrush.UI
{
public class WeaponHUD : MonoBehaviour
{
    [SerializeField] PlayerShoot shoot;

    Canvas          canvas;
    Image           weaponIcon;  // hangi silah — artık yazı yok, sadece ikon
    TextMeshProUGUI ammoText;    // "10 | 50"
    Image           grenIcon;
    TextMeshProUGUI grenText;    // "x6"
    Image           flashIcon;
    TextMeshProUGUI flashText;   // "x3"
    TextMeshProUGUI reloadText;  // "RELOADING..."

    static readonly Color ColActive  = Color.yellow;
    static readonly Color ColCyan    = new Color(0.4f, 0.9f, 1f);
    static readonly Color ColDim     = new Color(0.35f, 0.35f, 0.35f);
    static readonly Color ColWarn    = new Color(1f, 0.6f, 0f);
    static readonly Color ColDanger  = Color.red;
    static readonly Color ColWhite   = Color.white;

    void Start()
    {
        // Referans kopmuşsa (PlayerShoot başka objeye taşınmış olabilir) otomatik bul
        if (shoot == null) shoot = FindFirstObjectByType<PlayerShoot>();

        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        gameObject.AddComponent<CanvasScaler>();
        gameObject.AddComponent<GraphicRaycaster>();

        // Sağ alt köşe — yukarıdan aşağı: silah ikonu, reload, grenade, flash, mermi.
        // Mermi sayısı ekranın gerçek dibinde (y çok küçük); ikonlar onun ÜSTÜNDE.
        weaponIcon = MakeIcon(new Vector2(-18, 152), 56);
        reloadText = MakeText(new Vector2(-18,  98), 20, TextAlignmentOptions.Right, 220f);

        grenIcon = MakeIcon(new Vector2(-58, 68), 26);
        grenText = MakeText(new Vector2(-18, 72), 20, TextAlignmentOptions.Right, 36f);

        flashIcon = MakeIcon(new Vector2(-58, 42), 26);
        flashText = MakeText(new Vector2(-18, 46), 20, TextAlignmentOptions.Right, 36f);

        ammoText = MakeText(new Vector2(-18, 8), 28, TextAlignmentOptions.Right, 220f);

        reloadText.text  = "";
        reloadText.color = ColWarn;
    }

    void Update()
    {
        if (shoot == null) return;

        // Silahsızken (asansör kazası) tüm silah HUD'ı kapanır — aksi halde PlayerShoot
        // devre dışıyken bile bayat mermi sayısı ekranda kalırdı. PUSH değil PULL: canvas
        // burada, Start()'ta AddComponent ile yaratılıyor ve Start sırası garanti değil,
        // dışarıdan yazmak yarış yaratırdı.
        bool armed = PlayerLoadout.ShowWeaponHUD;
        if (canvas != null && canvas.enabled != armed) canvas.enabled = armed;
        if (!armed) return;

        var m = shoot.CurrentMode;

        var icon = shoot.CurrentFirearm switch
        {
            PlayerShoot.Firearm.Revolver => UIIcons.Revolver,
            PlayerShoot.Firearm.Shotgun  => UIIcons.Shotgun,
            PlayerShoot.Firearm.Lmg      => UIIcons.Lmg,
            _ => null,
        };
        weaponIcon.sprite  = icon;
        weaponIcon.enabled = icon != null;

        // Aktif ateşli silah
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

        // Launcher göstergeleri — launcher kapalıysa (Ch2) tamamen gizle
        bool launcher = shoot.LauncherEnabled;
        if (grenText.gameObject.activeSelf != launcher)
        {
            grenText.gameObject.SetActive(launcher);
            grenIcon.gameObject.SetActive(launcher);
        }
        if (flashText.gameObject.activeSelf != launcher)
        {
            flashText.gameObject.SetActive(launcher);
            flashIcon.gameObject.SetActive(launcher);
        }

        if (launcher)
        {
            grenText.text  = $"x{shoot.GrenadeAmmo}";
            flashText.text = $"x{shoot.FlashAmmo}";

            var grenActive  = m == PlayerShoot.LauncherMode.Grenade;
            var flashActive = m == PlayerShoot.LauncherMode.Flash;

            grenText.color  = grenActive  ? ColActive : ColDim;
            flashText.color = flashActive ? ColCyan   : ColDim;

            grenIcon.sprite   = UIIcons.Grenade;
            grenIcon.enabled  = UIIcons.Grenade != null;
            grenIcon.color    = grenActive ? ColActive : ColDim;

            flashIcon.sprite  = UIIcons.Flash;
            flashIcon.enabled = UIIcons.Flash != null;
            flashIcon.color   = flashActive ? ColCyan : ColDim;
        }
    }

    // MakeText ile aynı anchor/pivot deseni (sağ-alt köşeden büyüyen HUD) — kare ikon kutusu.
    Image MakeIcon(Vector2 pos, float size)
    {
        var go = new GameObject("_hudIcon");
        go.transform.SetParent(transform, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(1f, 0f);
        rt.anchorMax        = new Vector2(1f, 0f);
        rt.pivot            = new Vector2(1f, 0f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = new Vector2(size, size);

        var img = go.AddComponent<Image>();
        img.raycastTarget  = false;
        img.preserveAspect = true;
        img.enabled        = false;
        return img;
    }

    TextMeshProUGUI MakeText(Vector2 pos, float size, TextAlignmentOptions align, float width)
    {
        var go = new GameObject("_hud");
        go.transform.SetParent(transform, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(1f, 0f);
        rt.anchorMax        = new Vector2(1f, 0f);
        rt.pivot            = new Vector2(1f, 0f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = new Vector2(width, 40f);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize        = size;
        tmp.alignment       = align;
        tmp.color           = ColWhite;
        tmp.fontStyle       = FontStyles.Bold;
        tmp.characterSpacing = 3f;
        return tmp;
    }
}
}

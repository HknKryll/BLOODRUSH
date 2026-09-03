using UnityEngine;

namespace Bloodrush.UI
{
// Etkileşim ikonlarının merkezi önbelleği — UITheme.Load()'daki statik-cache deseninin
// aynısı. İkonlar Assets/Resources/Icons/ altında (game-icons.net'ten indirilip PNG'ye
// çevrilmiş, delapouite serisi — tutarlı görsel dil).
//
// Kullanıcı PNG'leri Sprite tipine çevirmediyse Resources.Load null döner — bu SORUN
// DEĞİL: DialogueUI/BookUI ikon null geldiğinde o alanı gizler, metin/kutu normal
// çalışmaya devam eder.
public static class UIIcons
{
    static Sprite book, chair, chat, exit, navigate, next;
    static Sprite revolver, shotgun, lmg, grenade, flash;

    public static Sprite Book     => book     ??= Resources.Load<Sprite>("Icons/icon_book");
    public static Sprite Chair    => chair    ??= Resources.Load<Sprite>("Icons/icon_chair");
    public static Sprite Chat     => chat     ??= Resources.Load<Sprite>("Icons/icon_chat");
    public static Sprite Exit     => exit     ??= Resources.Load<Sprite>("Icons/icon_exit");
    public static Sprite Navigate => navigate ??= Resources.Load<Sprite>("Icons/icon_navigate");
    public static Sprite Next     => next     ??= Resources.Load<Sprite>("Icons/icon_next");
    public static Sprite Revolver => revolver ??= Resources.Load<Sprite>("Icons/icon_revolver");
    public static Sprite Shotgun  => shotgun  ??= Resources.Load<Sprite>("Icons/icon_shotgun");
    public static Sprite Lmg      => lmg      ??= Resources.Load<Sprite>("Icons/icon_lmg");
    public static Sprite Grenade  => grenade  ??= Resources.Load<Sprite>("Icons/icon_grenade");
    public static Sprite Flash    => flash    ??= Resources.Load<Sprite>("Icons/icon_flash");
}
}

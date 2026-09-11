using UnityEngine;
using Bloodrush.Flow;
using Bloodrush.UI;
using Bloodrush.Shared.Audio;

namespace Bloodrush.Player
{
// Yerdeki silah — üstüne gidince o silahı KALICI açar (CH2 boss'undan düşer).
// PlayerPickup her kare OverlapSphere(1.5m) ile toplar (AmmoPickup deseni).
public class WeaponPickup : MonoBehaviour
{
    [Tooltip("Bu pickup hangi silahı açar (Shotgun / Lmg).")]
    [SerializeField] PlayerShoot.Firearm weapon = PlayerShoot.Firearm.Shotgun;
    [Tooltip("Ekranda gösterilecek ad (boşsa silah adı). Ör: 'POMPALI'.")]
    [SerializeField] string    displayName = "";
    [SerializeField] AudioClip pickupClip;
    [Tooltip("Görsel için hafif dönme (derece/sn). 0 = kapalı.")]
    [SerializeField] float     spinSpeed = 45f;

    bool collected;

    public void Collect(PlayerShoot shoot)
    {
        if (collected || shoot == null) return;
        collected = true;

        GameProgress.Unlock(weapon);        // kalıcı — CH2 kalanı + CH3'e taşınır
        shoot.SetUnlocked(weapon, true);    // hemen 2/3 ile seçilebilir

        // Aktif bir ekipman kısıtı varsa (asansör kazası) maskeyi de aç — yoksa bir sonraki
        // PlayerLoadout.Apply() yeni alınan silahı sessizce geri kilitlerdi. Yerden silah
        // almak her zaman kısıttan güçlüdür: oyuncu onu fiilen eline aldı.
        PlayerLoadout.Grant(GearOf(weapon));

        string label = string.IsNullOrEmpty(displayName) ? weapon.ToString().ToUpper() : displayName;
        Notification.Show($"{label} ELE GEÇİRİLDİ");
        SfxPlayer.PlayAtPoint(pickupClip, transform.position);
        Destroy(gameObject);
    }

    static PlayerLoadout.Gear GearOf(PlayerShoot.Firearm w) => w switch
    {
        PlayerShoot.Firearm.Shotgun => PlayerLoadout.Gear.Shotgun,
        PlayerShoot.Firearm.Lmg     => PlayerLoadout.Gear.Lmg,
        _                           => PlayerLoadout.Gear.Revolver,
    };

    void Update()
    {
        if (spinSpeed != 0f) transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 1.5f);
    }
}
}

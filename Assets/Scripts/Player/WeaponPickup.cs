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

        string label = string.IsNullOrEmpty(displayName) ? weapon.ToString().ToUpper() : displayName;
        Notification.Show($"{label} ELE GEÇİRİLDİ");
        SfxPlayer.PlayAtPoint(pickupClip, transform.position);
        Destroy(gameObject);
    }

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

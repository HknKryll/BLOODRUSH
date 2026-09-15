using UnityEngine;

namespace Bloodrush.Weapons
{
// Hitscan silahlarin (Revolver/Shotgun/LMG) denge + ses verisi. Kod degismeden
// asset uzerinden duzenlenebilsin diye PlayerShoot'un monolitik alanlarindan
// tasindi (bkz. BLOODRUSH_YENIDEN_YAPILANDIRMA_PLANI.md, Faz 3).
//
// Sahne-bagimli referanslar (namlu ucu, muzzleFlash ParticleSystem, kamera)
// burada YOK — bir ScriptableObject sahne objesine referans tutamaz, bu
// yuzden onlar PlayerShoot'ta scene-level alan olarak kaliyor.
[CreateAssetMenu(fileName = "WeaponData", menuName = "Bloodrush/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("Kimlik")]
    public string displayName = "WEAPON";

    [Header("Hasar")]
    public float damageNear = 10f;
    public float damageFar  = 10f;
    public float range      = 60f;

    [Header("Ateş")]
    public float fireRate    = 0.3f;
    public int   pelletCount = 1;
    public float spreadAngle = 0f;
    [Tooltip("Tepme çarpanı — ProceduralWeaponMotion'daki temel recoil değerleriyle çarpılır.")]
    public float recoilScale = 1f;

    [Header("Mühimmat")]
    public int   magazineSize    = 10;
    public int   startingReserve = 60;
    public float reloadTime      = 3f;

    [Header("Ses")]
    public AudioClip fireClip;
    [Range(0f, 1f)] public float fireVolume = 1f;
    public AudioClip emptyClickClip;
    [Range(0f, 1f)] public float emptyClickVolume = 0.6f;
    public AudioClip reloadClip;
    [Range(0f, 1f)] public float reloadVolume = 0.8f;

    [Header("Pooling (Faz 6)")]
    [Tooltip("Bu silahla ilişkili efekt/mermi havuzu için ön-ayırma boyutu. " +
             "Faz 6'dan önce hiçbir sistem okumuyor.")]
    public int poolSize = 8;
}
}

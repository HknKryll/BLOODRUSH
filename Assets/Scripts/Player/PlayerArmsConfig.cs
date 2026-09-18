using UnityEngine;

namespace Bloodrush.Player
{
// Birinci sahis kol/yumruk gorunumunun ayarlari. PlayerArmsView bunu
// Resources/PlayerArms/PlayerArmsConfig'ten yukler; Play'de degistirilen degerler aninda etki eder.
//
// Model: "PSX First Person Arms" — Drillimpact (itch.io), CC0 (kaynak gostermek gerekmez).
// Animasyonlar: jab.L / jab.R / push / grab / guard_idle / relax / rest ... Paketle gelen siyah
// eldiven dokusu ileride eldiven icin hazir: Use Gloves.
[CreateAssetMenu(menuName = "Bloodrush/Kol Gorunumu Ayarlari")]
public class PlayerArmsConfig : ScriptableObject
{
    public const string ResourcePath = "PlayerArms/PlayerArmsConfig";

    [Header("Model")]
    [Tooltip("Resources altindaki FBX yolu (uzantisiz).")]
    public string   modelPath = "PlayerArms/arms_rig";
    public Material bareMaterial;
    public Material gloveMaterial;
    [Tooltip("ACIK: eldivenli doku. Ileride eldiven bulununca ac.")]
    public bool     useGloves = false;
    [Tooltip("PS1 gorunumu: doku pikselleri yumusatilmasin.")]
    public bool     pointFilter = true;

    [Header("Kamera hizalama")]
    [Tooltip("Modelin tasarlandigi goz yuksekligi (m, model ayaginin ustunde).")]
    public float   eyeHeight = 1.70f;
    [Tooltip("Kollarin kameraya gore ek kaydirmasi (m). Y eksi = asagi, Z eksi = geri.")]
    public Vector3 viewOffset = new Vector3(0f, -0.03f, -0.02f);
    [Tooltip("Modelin 'rest' pozunda iki ust kol arasi (m) — olcek kalibrasyonu icin, degistirme.")]
    public float   referenceArmWidth = 0.394f;

    [Header("Yumruk")]
    [Tooltip("Klip adi; sonuna .L / .R eklenir.")]
    public string punchClip = "jab";
    [Tooltip("Parry'de oynayacak klip (.L/.R eklenir). ULTRAKILL'de parry de bir yumruktur.")]
    public string parryClip = "jab";
    [Tooltip("Klibin baslangicindaki yavas hazirlanma atlanir (sn, klip zamaninda).")]
    public float  clipStartTime = 0.2f;
    [Tooltip("Oynatma hizi. 1.8 ile yumruk ~0.17 sn'de uzanir.")]
    public float  playbackSpeed = 1.8f;
    [Tooltip("Parry icin hiz (daha sert/hizli hissettirsin).")]
    public float  parrySpeed    = 2.2f;

    [Header("Ses")]
    [Tooltip("Her yumruk/parry'de calan savurma sesi (isabette darbe sesi ayrica PlayerParry'den gelir).")]
    public AudioClip whooshClip;
    [Range(0f, 1f)] public float whooshVolume = 0.7f;
    [Tooltip("Her basista hafif farkli duyulsun: pitch bu aralikta rastgele.")]
    public Vector2 whooshPitch = new Vector2(0.92f, 1.08f);

    [Header("Hangi kol")]
    [Tooltip("Elde silah varken yalnizca SOL kol yumruk atar (silah sagda) — ULTRAKILL gibi.")]
    public bool leftArmWhenArmed = true;
    [Tooltip("Silahsizken sirayla sol-sag yumruk; iki kol da gorunur.")]
    public bool alternateWhenUnarmed = true;

    [Header("Belirme")]
    [Tooltip("Kol ekrana asagidan girer / cikar.")]
    public float slideDistance = 0.35f;
    public float slideInTime   = 0.05f;
    public float slideOutTime  = 0.12f;

    static PlayerArmsConfig cached;

    public static PlayerArmsConfig Load()
    {
        if (cached != null) return cached;
        cached = Resources.Load<PlayerArmsConfig>(ResourcePath);
        if (cached == null)
        {
            Debug.LogWarning($"[PlayerArmsConfig] Resources/{ResourcePath} bulunamadi — varsayilanlar.");
            cached = CreateInstance<PlayerArmsConfig>();
        }
        return cached;
    }
}
}

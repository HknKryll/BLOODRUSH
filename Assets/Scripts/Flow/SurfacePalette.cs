using UnityEngine;

namespace Bloodrush.Flow
{
// Bir bölümün TÜM görünümü tek asset'te: yüzey materyalleri + ambient/sis + ışık rengi.
// SceneDresser bunu okuyup sahneye uygular. Bölüm başına bir tane oluştur
// (PaletteCH1 temiz kurumsal → PaletteCH2 endüstriyel → PaletteCH3 kirli/paslı),
// böylece "temiz cephe → altındaki dehşet" yayı görsel olarak da ilerler.
//
// Oluşturma: Project'te sağ tık → Create → Bloodrush → Yüzey Paleti
[CreateAssetMenu(fileName = "SurfacePalette", menuName = "Bloodrush/Yüzey Paleti")]
public class SurfacePalette : ScriptableObject
{
    [Header("Yüzey Materyalleri (boş bırakılan tür atlanır)")]
    [Tooltip("Duvar_*, Wall_*, Kanat_*, Lento")]
    public Material wallMat;
    [Tooltip("Zemin, DipZemin, Doseme, Floor")]
    public Material floorMat;
    [Tooltip("Tavan, TepeKapak, Ceiling")]
    public Material ceilingMat;
    [Tooltip("Basamak, Merdiven")]
    public Material stairMat;
    [Tooltip("Kolon, Raf, Kopru, Ledge, Platform")]
    public Material metalMat;

    [Header("Atmosfer (SceneVolumeSetup'a yazılır)")]
    public bool  applyAtmosphere  = true;
    [Tooltip("İç mekan dolgu ışığı rengi — köşeler simsiyah kalmasın.")]
    public Color ambientColor     = new Color(0.85f, 0.90f, 1f);
    [Range(0f, 3f)]
    public float ambientIntensity = 0.8f;
    [Tooltip("Sis: KÜÇÜK = daha yoğun sis. 120 ince (CH1), 40-60 yoğun (CH3).")]
    public float fogMeanFreePath  = 120f;

    [Header("Nokta/Spot Işıkları")]
    public bool  applyLightColor     = true;
    public Color lightColor          = new Color(0.90f, 0.85f, 0.75f);
    [Tooltip("AÇIK ise ışıklar bu şiddete AYARLANIR (ölçekleme değil — tekrar çalıştırınca katlanmaz).")]
    public bool  applyLightIntensity = false;
    public float lightIntensityLumen = 2600f;
}
}

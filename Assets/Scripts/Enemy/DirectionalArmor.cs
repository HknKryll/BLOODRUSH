using UnityEngine;

// Yönlü zırh: önden gelen atışlar zırha çarpar (az hasar), yan/arkadan tam hasar.
// PlayerShoot hasar uygularken bu component'i arayıp çarpanı uygular.
public class DirectionalArmor : MonoBehaviour
{
    [Tooltip("Önden gelen atışın hasar çarpanı (0.1 = %90 emilir)")]
    [SerializeField] float frontMult = 0.1f;
    [Tooltip("Bu açıya kadar 'ön' sayılır (derece)")]
    [SerializeField] float frontAngle = 70f;

    // shotDir: merminin gittiği yön (kameradan hedefe). Önden geliyorsa düşük çarpan.
    public float Multiplier(Vector3 shotDir)
    {
        shotDir.Normalize();
        // Atış düşmanın önüne çarpıyorsa: -shotDir (yüzeye gelen yön) ile forward hizalı
        float angle = Vector3.Angle(transform.forward, -shotDir);
        return angle <= frontAngle ? frontMult : 1f;
    }
}

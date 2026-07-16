using UnityEngine;
using Bloodrush.FX;

namespace Bloodrush.Enemy
{
// DeathEffect.prefab'ın parçacık materyalini çalışma zamanında yumuşak
// kenarlı, saydam bir dokuya çevirir — DeadMaterial opak/texture'suz olduğu
// için parçacıklar kare görünüyordu.
public class DeathEffectVisual : MonoBehaviour
{
    [SerializeField] Color tint = new Color(0.7f, 0.05f, 0.05f);

    void Awake()
    {
        var r = GetComponent<ParticleSystemRenderer>();
        if (r != null) r.material = SoftDotVFX.CreateMaterial(tint);
    }
}
}

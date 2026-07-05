using UnityEngine;

public class PlayerParry : MonoBehaviour
{
    [SerializeField] KeyCode        parryKey   = KeyCode.F;
    [SerializeField] float          parryRange = 5f;
    [SerializeField] float          parryStun  = 2f;
    [SerializeField] WeaponAnimator weaponAnim;

    void Update()
    {
        if (!Input.GetKeyDown(parryKey)) return;

        Collider[] cols = Physics.OverlapSphere(transform.position, parryRange);
        foreach (var col in cols)
        {
            var enemy = col.GetComponent<EnemyAI>();
            if (enemy != null && enemy.IsParryable)
            {
                enemy.Parry(parryStun);
                CameraShake.Shake(0.2f, 0.15f);
                weaponAnim?.TriggerParry();
                return;
            }
        }
    }
}

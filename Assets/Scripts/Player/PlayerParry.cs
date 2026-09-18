using UnityEngine;
using Bloodrush.Shared;
using Bloodrush.Shared.Audio;
using Bloodrush.Enemy;
using Bloodrush.FX;

namespace Bloodrush.Player
{
public class PlayerParry : MonoBehaviour
{
    [SerializeField] float          parryRange = 5f;
    [SerializeField] float          parryStun  = 2f;
    [SerializeField] WeaponAnimator weaponAnim;

    [Header("Yumruk (parry olmayınca)")]
    [SerializeField] float punchRange  = 2.5f;
    [SerializeField] float punchDamage = 15f;
    [SerializeField] float punchForce  = 7f;

    [Header("Ses")]
    [SerializeField] AudioClip parryClip;
    [SerializeField] [Range(0f,1f)] float parryVolume = 1f;
    [SerializeField] AudioClip punchClip;
    [SerializeField] [Range(0f,1f)] float punchVolume = 0.8f;

    SfxPlayer sfx;

    // Gorunum (PlayerArmsView) bunlari dinler; mekanik bunlardan etkilenmez.
    public event System.Action<bool> Punched;   // arguman: bir dusmana isabet etti mi
    public event System.Action       Parried;

    void Start()
    {
        sfx = SfxPlayer.Create(gameObject, spatialBlend: 0f);

        // Birinci sahis yumruk animasyonu — sahneye/prefab'a bir sey eklemeden kurulur.
        var arms = GetComponent<PlayerArmsView>();
        if (arms == null) arms = gameObject.AddComponent<PlayerArmsView>();
        arms.Bind(this);
    }

    void Update()
    {
        if (!KeyBindings.Down(KeyBindings.Action.ParryPunch)) return;

        // 1) Önce parry dene — telegraph yapan düşman/boss varsa
        Collider[] cols = Physics.OverlapSphere(transform.position, parryRange);
        foreach (var col in cols)
        {
            var parryable = col.GetComponentInParent<IParryable>();
            if (parryable != null && parryable.IsParryable)
            {
                parryable.Parry(parryStun);
                CameraShake.Shake(0.2f, 0.15f);
                weaponAnim?.TriggerParry();
                sfx.Play(parryClip, parryVolume);
                Parried?.Invoke();
                return;
            }
        }

        // 2) Parry yoksa yumruk at — önündeki en yakın KÜÇÜK düşmana
        TryPunch();
    }

    void TryPunch()
    {
        EnemyAI target = null;
        float   bestDist = float.MaxValue;

        Collider[] cols = Physics.OverlapSphere(transform.position, punchRange);
        foreach (var col in cols)
        {
            var enemy = col.GetComponentInParent<EnemyAI>();
            if (enemy == null || enemy.IsLarge) continue;   // büyük düşmana işlemez

            Vector3 to = enemy.transform.position - transform.position;
            if (Vector3.Dot(transform.forward, to.normalized) < 0.3f) continue;  // önümüzde mi

            float d = to.sqrMagnitude;
            if (d < bestDist) { bestDist = d; target = enemy; }
        }

        if (target == null)
        {
            Punched?.Invoke(false);   // bosa yumruk: hasar yok ama kol yine savrulur
            return;
        }

        target.GetComponent<Health>()?.TakeDamage(punchDamage);
        target.Knockback(target.transform.position - transform.position, punchForce);
        weaponAnim?.TriggerParry();
        CameraShake.Shake(0.08f, 0.1f);
        sfx.Play(punchClip, punchVolume);
        Punched?.Invoke(true);
    }
}
}

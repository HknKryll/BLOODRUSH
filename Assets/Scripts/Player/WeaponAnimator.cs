using UnityEngine;

public class WeaponAnimator : MonoBehaviour
{
    [SerializeField] Animator animator;

    PlayerMovement  movement;
    GrapplingHook   hook;

    void Start()
    {
        movement = GetComponentInParent<PlayerMovement>();
        hook     = GetComponentInParent<GrapplingHook>();
    }

    void Update()
    {
        if (animator == null) return;

        Vector3 vel   = movement.Velocity;
        float   speed = new Vector2(vel.x, vel.z).magnitude;

        animator.SetFloat("Speed",       speed);
        animator.SetBool ("IsGrounded",  movement.IsGrounded);
        animator.SetBool ("IsSliding",   movement.IsSliding);
        animator.SetBool ("IsGrappling", hook != null && hook.IsHooked);
    }

    public void TriggerFire()         => animator?.SetTrigger("Fire");
    public void TriggerFireLauncher() => animator?.SetTrigger("FireLauncher");
    public void TriggerParry()        => animator?.SetTrigger("Parry");
}

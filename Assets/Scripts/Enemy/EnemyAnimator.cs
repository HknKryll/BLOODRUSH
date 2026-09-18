using UnityEngine;

namespace Bloodrush.Enemy
{
// Dusman AI'lari ile Animator arasindaki ince katman. Iki isi var:
//  1) Parametreleri SADECE DEGISTIGINDE yazar — her karede SetFloat/SetBool spam'i yok.
//  2) Controller'da o parametre yoksa sessizce atlar. Eski KucukDusmanController/BuyukDusman
//     controller'larinda sadece Speed + Attack var; yeni Boss/Ranged controller'larinda
//     IsAiming/Fire/Dodge de var. Ayni kod ikisiyle de log spam'i uretmeden calisir.
//
// Adlar string olarak DEGIL, Animator.StringToHash ile bir kez hash'lenip saklanir.
public class EnemyAnimator
{
    public static readonly int SpeedId   = Animator.StringToHash("Speed");
    public static readonly int AimingId  = Animator.StringToHash("IsAiming");
    public static readonly int FireId    = Animator.StringToHash("Fire");
    public static readonly int DodgeId   = Animator.StringToHash("Dodge");
    public static readonly int AttackId  = Animator.StringToHash("Attack");
    public static readonly int StunnedId = Animator.StringToHash("Stunned");

    readonly Animator anim;
    readonly bool hasSpeed, hasAiming, hasFire, hasDodge, hasAttack, hasStunned;

    float lastSpeed = -1f;
    bool  lastAiming, lastStunned;

    public bool HasAnimator   => anim != null;
    public Animator Raw       => anim;
    public bool HasFireParam  => hasFire;
    public bool HasDodgeParam => hasDodge;
    public bool HasStunParam  => hasStunned;
    public bool HasAimParam   => hasAiming;

    public EnemyAnimator(Animator animator)
    {
        anim = animator;
        if (anim == null) return;

        foreach (var p in anim.parameters)
        {
            if      (p.nameHash == SpeedId)   hasSpeed   = true;
            else if (p.nameHash == AimingId)  hasAiming  = true;
            else if (p.nameHash == FireId)    hasFire    = true;
            else if (p.nameHash == DodgeId)   hasDodge   = true;
            else if (p.nameHash == AttackId)  hasAttack  = true;
            else if (p.nameHash == StunnedId) hasStunned = true;
        }
    }

    // Blend tree'yi suren deger. Esik altindaki degisimler yazilmaz (kucuk titresimler icin
    // her karede yazmanin anlami yok).
    public void SetSpeed(float value)
    {
        if (!hasSpeed || anim == null) return;
        if (lastSpeed >= 0f && Mathf.Abs(value - lastSpeed) < 0.02f) return;
        lastSpeed = value;
        anim.SetFloat(SpeedId, value);
    }

    public void SetAiming(bool value)
    {
        if (!hasAiming || anim == null || value == lastAiming) return;
        lastAiming = value;
        anim.SetBool(AimingId, value);
    }

    public bool SetStunned(bool value)
    {
        if (!hasStunned || anim == null || value == lastStunned) return hasStunned;
        lastStunned = value;
        anim.SetBool(StunnedId, value);
        return true;
    }

    public bool Fire()
    {
        if (!hasFire || anim == null) return false;
        anim.SetTrigger(FireId);
        return true;
    }

    public bool Dodge()
    {
        if (!hasDodge || anim == null) return false;
        anim.SetTrigger(DodgeId);
        return true;
    }

    public bool Attack()
    {
        if (!hasAttack || anim == null) return false;
        anim.SetTrigger(AttackId);
        return true;
    }

    public void ResetAttack()
    {
        if (hasAttack && anim != null) anim.ResetTrigger(AttackId);
    }

    public void SetPlaybackSpeed(float s)
    {
        if (anim != null) anim.speed = s;
    }
}
}

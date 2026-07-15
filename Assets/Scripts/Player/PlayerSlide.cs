using UnityEngine;

namespace Bloodrush.Player
{
// Slide state machine (giriş/çıkış, çömelme collider boyutu, kayma hareketi).
// Dikey hız/ses çalma kararları çağıran (PlayerMovement) tarafında kalır.
public class PlayerSlide
{
    readonly CharacterController cc;
    readonly float slideSpeed;
    readonly float slideDuration;
    readonly float moveSpeed;

    readonly float   normalHeight;
    readonly Vector3 normalCenter;

    Vector3 slideDir;
    float   slideTimer;
    float   slideEndTime = -1f;

    public bool  IsSliding         { get; private set; }
    public float SlideJumpDeadline { get; private set; }

    public PlayerSlide(CharacterController cc, float slideSpeed, float slideDuration, float moveSpeed)
    {
        this.cc            = cc;
        this.slideSpeed    = slideSpeed;
        this.slideDuration = slideDuration;
        this.moveSpeed     = moveSpeed;
        normalHeight = cc.height;
        normalCenter = cc.center;
    }

    public bool CanStart(bool keyDown, bool keyHeld, bool grounded, Vector3 wish) =>
        !IsSliding && grounded && wish.magnitude > 0.1f &&
        (keyDown || (keyHeld && Time.time - slideEndTime < 0.15f));

    public void Start(Vector3 wishDir)
    {
        IsSliding  = true;
        slideTimer = slideDuration;
        slideDir   = wishDir.normalized;
        cc.height  = normalHeight * 0.5f;
        cc.center  = new Vector3(0f, normalCenter.y * 0.5f, 0f);
    }

    // Her frame çağrılır. Slide biterse ended=true; itilecek hız launchOut'ta,
    // zıplama tuşuyla mı bittiği jumpedOut'ta döner (velocity.y ataması çağıranda kalır).
    public void Tick(bool jumpPressed, out bool ended, out bool jumpedOut, out Vector3 launchOut)
    {
        ended = false;
        jumpedOut = false;
        launchOut = Vector3.zero;
        if (!IsSliding) return;

        slideTimer -= Time.deltaTime;
        cc.Move(slideDir * slideSpeed * Time.deltaTime);

        if (slideTimer <= 0f || jumpPressed)
        {
            launchOut = slideDir * moveSpeed;
            Stop();
            ended = true;
            if (jumpPressed)
                jumpedOut = true;
            else
                SlideJumpDeadline = Time.time + 0.3f;
        }
    }

    // Bir zıplama gerçekleştiğinde slide-sonrası zıplama toleransını tüketir
    // (Move()'daki ana zıplama bloğu tarafından çağrılır).
    public void ConsumeJumpDeadline() => SlideJumpDeadline = 0f;

    public void Stop()
    {
        IsSliding    = false;
        slideEndTime = Time.time;
        cc.height    = normalHeight;
        cc.center    = normalCenter;
    }
}
}

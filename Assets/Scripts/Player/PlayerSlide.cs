using UnityEngine;

namespace Bloodrush.Player
{
// Apex-tarzı slide: Ctrl basılı tutuldukça sürer, sürtünmeyle yavaşlar; tuş
// bırakılınca, min hızın altına düşünce, zeminden çıkınca veya zıplayınca biter.
// Momentum korunur (çıkışta kalan hız launchVelocity'ye beslenir).
// Dikey hız/ses kararları çağıranda (PlayerMovement) kalır.
public class PlayerSlide
{
    readonly CharacterController cc;
    readonly float slideSpeed;      // giriş boost hedefi
    readonly float slideFriction;   // yavaşlama /sn
    readonly float minSlideSpeed;   // bu hızın altında slide biter
    readonly float maxSlideTime;    // güvenlik üst sınırı (sonsuz slide'ı önler)
    readonly float moveSpeed;

    readonly float   normalHeight;
    readonly Vector3 normalCenter;

    Vector3 slideDir;
    float   slideCurrentSpeed;
    float   slideTimer;

    public bool  IsSliding         { get; private set; }
    public float SlideJumpDeadline { get; private set; }

    public PlayerSlide(CharacterController cc, float slideSpeed, float slideFriction,
                       float minSlideSpeed, float maxSlideTime, float moveSpeed)
    {
        this.cc            = cc;
        this.slideSpeed    = slideSpeed;
        this.slideFriction = slideFriction;
        this.minSlideSpeed = minSlideSpeed;
        this.maxSlideTime  = maxSlideTime;
        this.moveSpeed     = moveSpeed;
        normalHeight = cc.height;
        normalCenter = cc.center;
    }

    // Yalnızca taze basışta (keyDown) başlar — tutmak sadece Tick'te slide'ı sürdürür,
    // yeniden başlatmaz (yoksa Ctrl basılı tutunca sonsuz slide döngüsü olurdu).
    public bool CanStart(bool keyDown, bool grounded, Vector3 wish) =>
        !IsSliding && grounded && wish.magnitude > 0.1f && keyDown;

    public void Start(Vector3 wishDir, float entrySpeed)
    {
        IsSliding         = true;
        slideDir          = wishDir.normalized;
        slideCurrentSpeed = Mathf.Max(slideSpeed, entrySpeed);   // giriş hızı varsa boost korunur
        slideTimer        = maxSlideTime;
        // Çömel: yüksekliği yarıya indir ama collider TABANINI yerde tut. (Merkezi
        // körlemesine yarıya çekmek tabanı yerden kaldırıp isGrounded=false yapıyor,
        // oyuncu çömelince havada kalıyordu.)
        float crouchHeight = normalHeight * 0.5f;
        float bottom       = normalCenter.y - normalHeight * 0.5f;
        cc.height = crouchHeight;
        cc.center = new Vector3(normalCenter.x, bottom + crouchHeight * 0.5f, normalCenter.z);
    }

    // Her frame çağrılır. Biterse ended=true; itilecek hız launchOut'ta, zıplayarak
    // mı bittiği jumpedOut'ta döner (velocity.y ataması çağıranda kalır).
    public void Tick(bool jumpPressed, bool slideKeyHeld,
                     out bool ended, out bool jumpedOut, out Vector3 launchOut)
    {
        ended     = false;
        jumpedOut = false;
        launchOut = Vector3.zero;
        if (!IsSliding) return;

        slideTimer        -= Time.deltaTime;
        slideCurrentSpeed  = Mathf.Max(0f, slideCurrentSpeed - slideFriction * Time.deltaTime);
        cc.Move(slideDir * slideCurrentSpeed * Time.deltaTime);

        bool released = !slideKeyHeld;
        bool tooSlow  = slideCurrentSpeed <= minSlideSpeed;
        bool timedOut = slideTimer <= 0f;
        if (jumpPressed || released || tooSlow || timedOut)
        {
            launchOut = slideDir * Mathf.Max(slideCurrentSpeed, moveSpeed);   // momentum korunur
            Stop();
            ended = true;
            if (jumpPressed)
                jumpedOut = true;
            else
                SlideJumpDeadline = Time.time + 0.3f;
        }
    }

    // Bir zıplama gerçekleştiğinde slide-sonrası zıplama toleransını tüketir.
    public void ConsumeJumpDeadline() => SlideJumpDeadline = 0f;

    public void Stop()
    {
        IsSliding = false;
        cc.height = normalHeight;
        cc.center = normalCenter;
    }
}
}

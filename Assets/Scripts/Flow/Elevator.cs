using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Bloodrush.Player;
using Bloodrush.Shared.Audio;

// Fiziksel asansör: oyuncu platforma binince kısa bir "kapı kapanıyor" bekleme
// süresinin ardından otomatik olarak hedef noktaya iner. Sürüş boyunca oyuncu
// bu objeye parent'lanır (kabin hareketiyle birlikte taşınır) ve PlayerMovement
// kapatılır — yürüme/bakış donar, IntroSalonController'daki oturma sekansıyla
// aynı desen (bkz. Assets/Scripts/Flow/IntroSalonController.cs).
//
// Kullanım: bu script'i asansör kabininin/platformunun üzerine koy (görsel
// kabin modeli varsa bu objenin child'ı yap — birlikte hareket eder).
// BoxCollider (isTrigger) platform zeminini kaplasın. "destination" alanına
// asansörün varacağı noktayı işaretleyen boş bir GameObject ata. İniş
// bitince "onArrived" event'ini odanın/sunumun açılışına bağlayabilirsin.
namespace Bloodrush.Flow
{
[RequireComponent(typeof(BoxCollider))]
public class Elevator : MonoBehaviour
{
    [Header("Güzergah")]
    [SerializeField] Transform destination;
    [SerializeField] float     rideDuration   = 4f;
    [SerializeField] float     doorCloseDelay = 1.2f;   // oyuncu binince bekleme süresi

    [Header("Kapı (opsiyonel)")]
    [Tooltip("Atanırsa iniş başlamadan önce bu kapı kapatılır ve kapanması beklenir.")]
    [SerializeField] ElevatorDoor door;

    [Header("Ses")]
    [SerializeField] AudioClip rideLoopClip;
    [SerializeField] [Range(0f,1f)] float rideLoopVolume  = 0.5f;
    [SerializeField] AudioClip arriveClip;
    [SerializeField] [Range(0f,1f)] float arriveVolume    = 0.9f;

    [Header("Bitince")]
    public UnityEvent onArrived;

    bool           playerInside;
    bool           activated;
    float          insideTimer;
    PlayerMovement playerMovement;
    Transform      playerTransform;
    Transform      originalParent;
    SfxPlayer      sfx;
    AudioSource    rideLoopSource;

    void Start()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        sfx = SfxPlayer.Create(gameObject, spatialBlend: 0f);
    }

    void OnTriggerEnter(Collider other)
    {
        if (activated) return;
        var pm = other.GetComponentInParent<PlayerMovement>();
        if (pm == null) return;

        playerInside    = true;
        playerMovement  = pm;
        playerTransform = pm.transform;
        insideTimer     = 0f;
    }

    void OnTriggerExit(Collider other)
    {
        if (activated || playerMovement == null) return;
        if (other.GetComponentInParent<PlayerMovement>() == playerMovement)
        {
            playerInside = false;
            insideTimer  = 0f;
        }
    }

    void Update()
    {
        if (activated || !playerInside) return;

        insideTimer += Time.deltaTime;
        if (insideTimer >= doorCloseDelay) Activate();
    }

    void Activate()
    {
        if (destination == null) { Debug.LogWarning("Elevator: destination atanmadı.", this); return; }
        activated = true;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        // CharacterController açıkken parent'ın hareketi onu "ezip" fiziksel olarak
        // geri itebiliyor (bkz. PlayerMovement.Teleport() — aynı sebeple CC'yi kapatıyor).
        // Önce CC'yi kapat, sonra parent'la — yoksa oyuncu platformla birlikte gelmez.
        playerMovement.Controller.enabled = false;
        playerMovement.enabled = false;
        originalParent = playerTransform.parent;
        playerTransform.SetParent(transform, true);

        if (door != null) yield return door.CloseAndWait();   // kapı kapanana kadar bekle, sonra in

        if (rideLoopClip != null)
        {
            rideLoopSource = gameObject.AddComponent<AudioSource>();
            rideLoopSource.clip         = rideLoopClip;
            rideLoopSource.volume       = rideLoopVolume;
            rideLoopSource.loop         = true;
            rideLoopSource.spatialBlend = 0f;
            rideLoopSource.Play();
        }

        Vector3 start = transform.position;
        Vector3 end   = destination.position;
        float   t     = 0f;
        while (t < rideDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / rideDuration));
            transform.position = Vector3.Lerp(start, end, p);
            yield return null;
        }
        transform.position = end;

        if (rideLoopSource != null) Destroy(rideLoopSource);

        playerTransform.SetParent(originalParent, true);
        playerMovement.Controller.enabled = true;
        playerMovement.enabled = true;

        sfx.Play(arriveClip, arriveVolume);
        onArrived?.Invoke();
    }
}
}

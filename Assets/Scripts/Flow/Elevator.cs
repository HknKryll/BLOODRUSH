using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Bloodrush.Player;
using Bloodrush.Shared.Audio;

// Asansör (fade + ışınlama): oyuncu kabine binince kısa bir bekleme sonrası giriş
// kapısı kapanır, EKRAN KARARIR, karanlıkta asansör sesi çalar, oyuncu destination'a
// IŞINLANIR, (varsa) çıkış kapısı açılır ve ekran geri açılır. Kabin FİZİKSEL OLARAK
// HAREKET ETMEZ — parent/kapı-birlikte-inme dertleri yok.
//
// Kurulum: bu script kabin objesinde; BoxCollider (isTrigger) kabin zeminini kaplasın.
// destination = alt kattaki varış noktası (boş obje; MAVİ OK = oyuncunun bakacağı yön).
// Alt katta görsel istersen oraya ayrı bir kabin kopyası kur — exitDoor onun kapısı olur.
namespace Bloodrush.Flow
{
[RequireComponent(typeof(BoxCollider))]
public class Elevator : MonoBehaviour
{
    [Header("Varış")]
    [Tooltip("Işınlanma hedefi (boş obje). Mavi ok = oyuncunun bakış yönü.")]
    [SerializeField] Transform destination;
    [SerializeField] float     doorCloseDelay = 1.2f;   // oyuncu binince bekleme süresi
    [SerializeField] float     fadeDuration   = 0.8f;   // kararma/açılma süresi
    [Tooltip("Tam karanlıkta bekleme süresi (asansör sesi bu sırada çalar) — 'iniş' hissi.")]
    [SerializeField] float     blackHold      = 1.5f;

    [Header("Kapılar (opsiyonel)")]
    [Tooltip("Oyuncu binince AÇILIR, kararmadan önce KAPANIR (üst kattaki kapı).")]
    [UnityEngine.Serialization.FormerlySerializedAs("door")]
    [SerializeField] ElevatorDoor entryDoor;
    [Tooltip("Varışta AÇILIR (alt kattaki kabin kopyasının kapısı). Auto Open On Approach = KAPALI olsun.")]
    [SerializeField] ElevatorDoor exitDoor;

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

        playerInside   = true;
        playerMovement = pm;
        insideTimer    = 0f;

        if (entryDoor != null) entryDoor.Open();   // oyuncu binince giriş kapısı açılır
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
        playerMovement.enabled = false;            // karanlıkta yürüme/bakış donsun

        if (entryDoor != null) yield return entryDoor.CloseAndWait();   // kapı kapansın

        // Ekran kararır
        var img = GameFlow.CreateOverlay(Color.black);
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            img.color = new Color(0f, 0f, 0f, t / fadeDuration);
            yield return null;
        }
        img.color = Color.black;

        // Karanlıkta "iniş": asansör sesi + bekleme
        if (rideLoopClip != null)
        {
            rideLoopSource = gameObject.AddComponent<AudioSource>();
            rideLoopSource.clip         = rideLoopClip;
            rideLoopSource.volume       = rideLoopVolume;
            rideLoopSource.loop         = true;
            rideLoopSource.spatialBlend = 0f;
            rideLoopSource.Play();
        }
        yield return new WaitForSeconds(blackHold);
        if (rideLoopSource != null) Destroy(rideLoopSource);

        // Karanlıkta ışınla (yaw = destination'ın yönü; Teleport CC/hız/flip'i halleder)
        playerMovement.Teleport(destination.position,
                                Quaternion.Euler(0f, destination.eulerAngles.y, 0f));

        sfx.Play(arriveClip, arriveVolume);
        if (exitDoor != null) exitDoor.Open();     // varışta çıkış kapısı açılır

        // Ekran geri açılır
        t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            img.color = new Color(0f, 0f, 0f, 1f - t / fadeDuration);
            yield return null;
        }
        Destroy(img.canvas.gameObject);

        playerMovement.enabled = true;
        onArrived?.Invoke();
    }
}
}

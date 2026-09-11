using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
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
    [Tooltip("DOLUYSA: aynı sahnede ışınlamak yerine bu sahne yüklenir (Build Settings'te " +
             "olmalı). Karartma karşı sahneye taşınır, oyuncu oranın PlayerStartPoint'inde " +
             "başlar — destination'a gerek kalmaz. Boşsa eski davranış: aynı sahnede ışınlama.")]
    [SerializeField] string    destinationScene = "";
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

    [Header("Olay Sekansı (opsiyonel — varsayılanlar eski davranışı korur)")]
    [Tooltip("Kapalıysa ekran HİÇ kararmaz; ışınlanma sert kesme olur. Normalde açık kalsın.")]
    [SerializeField] bool  useFade          = true;
    [Tooltip("Kararma BAŞLAMADAN önce sekansa verilen ek süre (kaza kaosu açık ekranda başlasın diye).")]
    [SerializeField] float preFadeChaosHold = 0f;
    [Tooltip("Işınlandıktan SONRA, ekran açılmadan önceki sessiz karanlık (sn).")]
    [SerializeField] float postTeleportHold = 0f;

    [Header("Bitince")]
    public UnityEvent onArrived;

    bool           playerInside;
    bool           activated;
    float          insideTimer;
    PlayerMovement playerMovement;
    SfxPlayer      sfx;
    AudioSource    rideLoopSource;
    IElevatorSequenceHook hook;   // aynı objede varsa kaza sekansı; yoksa null = eski davranış

    // Sekansın binen oyuncuya ulaşması için (aramayı tekrarlamasın).
    public PlayerMovement Rider => playerMovement;

    void Start()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        sfx  = SfxPlayer.Create(gameObject, spatialBlend: 0f);
        hook = GetComponent<IElevatorSequenceHook>();   // yoksa null — davranış birebir eskisi
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
        if (destination == null && string.IsNullOrEmpty(destinationScene))
        {
            Debug.LogWarning("Elevator: destination veya destinationScene atanmadı.", this);
            return;
        }
        activated = true;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        playerMovement.enabled = false;            // karanlıkta yürüme/bakış donsun

        if (hook != null)
        {
            // Sekans varsa kapı kapanışı ile kamera dönüşü ÜST ÜSTE BİNER: oyuncu dönüp
            // kapıların üstüne kapandığını GÖRÜR. Bu yüzden CloseAndWait'i beklemiyoruz —
            // Close() ile kapatmayı başlatıp sinemayı hemen çalıştırıyoruz.
            if (entryDoor != null) entryDoor.Close();
            yield return hook.OnCabinCinematic(this);
        }
        else if (entryDoor != null)
        {
            yield return entryDoor.CloseAndWait();   // hook yoksa eski davranış birebir
        }

        // Kaos penceresi = kararma öncesi ek süre + kararmanın kendisi. Sekans efektlerini
        // buna göre ölçekler, tam siyaha varıldığında biter.
        hook?.OnBlackoutBegin(preFadeChaosHold + (useFade ? fadeDuration : 0f));
        if (preFadeChaosHold > 0f) yield return new WaitForSeconds(preFadeChaosHold);

        // Ekran kararır
        Image img = null;
        if (useFade)
        {
            img = GameFlow.CreateOverlay(Color.black);
            float ft = 0f;
            while (ft < fadeDuration)
            {
                ft += Time.deltaTime;
                img.color = new Color(0f, 0f, 0f, ft / fadeDuration);
                yield return null;
            }
            img.color = Color.black;
        }
        hook?.OnFullyBlack();

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

        // Sahne geçişi yolu: aynı sahnede ışınlamak yerine yeni sahneyi yükle. Karartma
        // SceneFadeIn ile karşı tarafa taşınır (yoksa yeni sahne bir kare tam parlaklıkta
        // patlardı); sessiz karanlık + açılış artık orada yaşanır. Bu obje sahneyle birlikte
        // yok olacağı için coroutine burada biter — sekansın temizliği OnDisable'da.
        if (!string.IsNullOrEmpty(destinationScene))
        {
            SceneFadeIn.CarryOverlay(img, postTeleportHold, fadeDuration);
            SceneManager.LoadScene(destinationScene);
            yield break;
        }

        // Karanlıkta ışınla (yaw = destination'ın yönü; Teleport CC/hız/flip'i halleder)
        playerMovement.Teleport(destination.position,
                                Quaternion.Euler(0f, destination.eulerAngles.y, 0f));

        sfx.Play(arriveClip, arriveVolume);
        if (exitDoor != null) exitDoor.Open();     // varışta çıkış kapısı açılır

        // Varış sonrası sessiz karanlık (kazada "uyanmadan önceki boşluk")
        if (postTeleportHold > 0f) yield return new WaitForSeconds(postTeleportHold);

        // Ekran geri açılır
        if (img != null)
        {
            float ft = 0f;
            while (ft < fadeDuration)
            {
                ft += Time.deltaTime;
                img.color = new Color(0f, 0f, 0f, 1f - ft / fadeDuration);
                yield return null;
            }
            Destroy(img.canvas.gameObject);
        }

        playerMovement.enabled = true;
        onArrived?.Invoke();
        hook?.OnRideEnd();
    }
}
}

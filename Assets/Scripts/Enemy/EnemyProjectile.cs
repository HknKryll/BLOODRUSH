using UnityEngine;

// Menzilli düşmanların attığı kaçılabilir mermi. İleri uçar, oyuncuya değince
// hasar verir, duvara değince/süresi dolunca yok olur.
using Bloodrush.Shared;
using Bloodrush.Shared.Pooling;
using Bloodrush.Player;

namespace Bloodrush.Enemy
{
public class EnemyProjectile : MonoBehaviour, IPoolable
{
    const float lifeDuration = 5f;

    float speed;
    float damage;
    float life = lifeDuration;

    [Tooltip("Carpma taramasinin yaricapi (m). Buyuk = daha affedici isabet.")]
    [SerializeField] float hitRadius = 0.22f;

    [Header("Iz (tracer)")]
    // KISA VE INCE OLMALI. Eskiden time 0.12 / startWidth 0.10 idi: 22 m/s'de bu ~2.6 m
    // uzunlugunda incelen bir seride demek ve iz merminin ARKASINDA kaliyor — temiz bir
    // iskada bile oyuncunun ustunden supuruluyordu. "Mermi icimden gecti ama hasar
    // vermedi" sikayetinin asil kaynagi buydu: mermi gercekten iskaliyordu, iz vurmus
    // gibi gosteriyordu.
    [Tooltip("Izin omru (sn). Uzunluk = hiz x bu sure. 0.05 ~ 1.1 m.")]
    [SerializeField] float trailTime  = 0.05f;
    [Tooltip("Izin baslangic kalinligi (m). Ucta 0'a iner.")]
    [SerializeField] float trailWidth = 0.05f;

    [Header("Teshis")]
    [Tooltip("Her merminin akibetini Console'a yazar: ISABET / ISKA-engel (engelin ADIYLA) / " +
             "ISKA-sure doldu (oyuncuya en yakin mesafeyle). Hasar sorunu cozulunce kapat.")]
    [SerializeField] bool logHits = true;

    static Material trailMat;

    // Paylasilan overlap buffer — mermiler ayni kare icinde sirayla Update oldugu icin
    // tek bir dizi yeterli.
    static readonly Collider[] overlapBuffer = new Collider[8];

    // Iska teshisi: mermi ucarken oyuncuya en fazla ne kadar yaklasti.
    float     closestApproach = float.MaxValue;
    Transform playerTr;
    Transform shooter;
    Vector3   prevPos;

    TrailRenderer trail;

    void Awake()
    {
        // Asset'siz parlak iz (tracer) — gelen ateş net görünür. Prefab'a zaten
        // TrailRenderer eklenmemişse ekle.
        trail = GetComponent<TrailRenderer>();
        if (trail == null)
        {
            trail = gameObject.AddComponent<TrailRenderer>();
            trail.time            = trailTime;
            trail.startWidth      = trailWidth;
            trail.endWidth        = 0f;
            trail.numCapVertices  = 2;
            trail.sharedMaterial  = GetTrailMat();
        }
    }

    // Havuzdan her alınışta sıfırdan kurulur (bkz. IPoolable) — bayat iz/mesafe
    // verisi önceki uçuştan taşınmasın.
    public void OnSpawned()
    {
        life = lifeDuration;
        closestApproach = float.MaxValue;
        shooter = null;
        trail.Clear();
    }

    public void OnDespawned() { }

    static Material GetTrailMat()
    {
        if (trailMat == null)
        {
            trailMat = new Material(Shader.Find("HDRP/Unlit"));
            trailMat.SetColor("_UnlitColor", new Color(1f, 0.5f, 0.2f) * 1.8f);   // hafif HDR turuncu (Bloom patlatmasın)
        }
        return trailMat;
    }

    // shooter: ates eden dusmanin Transform'u. Mermi kendi namlusunun icinde dogdugu icin
    // (origin = enemy.position + up*1.4, dusmanin BoxCollider'i y −0.10..1.91) atiyicinin
    // govdesini ATLAMASI sart. Eskiden bu `GetComponentInParent<EnemyAI>()` tahminine
    // dayaniyordu; hiyerarsi degisirse mermi kendi gövdesini DUVAR sanip aninda olurdu.
    // Artik dogrudan referansla atlanıyor.
    public void Launch(Vector3 direction, float projectileSpeed, float projectileDamage,
                       Transform shooter = null)
    {
        transform.forward = direction;
        speed       = projectileSpeed;
        damage      = projectileDamage;
        this.shooter = shooter;
        prevPos     = transform.position;
    }

    void Update()
    {
        float step = speed * Time.deltaTime;

        TrackClosestApproach();

        // SUPURULMUS ISABET KONTROLU — onceki konumdan simdikine kadar olan ARALIK.
        //
        // Tek noktada OverlapSphere yetmiyordu: 22 m/s'de mermi karede 0.37 m atliyor ve
        // oyuncu da hareket ediyor; iki kare arasinda kapsulun icinden gecip hicbir
        // karede "uzerinde" olmayabiliyor. Kapsul taramasi araligin TAMAMINI tarar,
        // yani tunelleme yapisal olarak imkansiz hale gelir.
        int nOverlap = Physics.OverlapCapsuleNonAlloc(prevPos, transform.position, hitRadius,
                                                      overlapBuffer, ~0,
                                                      QueryTriggerInteraction.Ignore);
        for (int i = 0; i < nOverlap; i++)
        {
            if (overlapBuffer[i].GetComponentInParent<PlayerMovement>() == null) continue;
            HitPlayer(overlapBuffer[i].GetComponentInParent<Health>());
            return;
        }

        // Yolda oyuncu/duvar var mi?
        //
        // INCE RAYCAST DEGIL SPHERECAST: mermi kucuk ve hizli (22 m/s). Tek bir cizgi,
        // oyuncunun kapsulunu kenardan siyirip gecebiliyor ve dusuk kare hizinda adim
        // buyudukce tunelleme riski artiyor. Kure taramasi bunu yapisal olarak kapatir.
        if (Physics.SphereCast(transform.position, hitRadius, transform.forward,
                out RaycastHit hit, step + hitRadius, ~0, QueryTriggerInteraction.Ignore))
        {
            var isPlayer = hit.collider.GetComponentInParent<PlayerMovement>() != null;
            if (isPlayer)
            {
                HitPlayer(hit.collider.GetComponentInParent<Health>());
                return;
            }

            // Ates edenin KENDI govdesi — dogrudan referansla atlanir (tahmin yok).
            bool isShooter = shooter != null && hit.collider.transform.IsChildOf(shooter);

            // Duvar — yok ol. Baska dusmanin icinden gecmeye devam eder (dost atesi yok).
            if (!isShooter && hit.collider.GetComponentInParent<EnemyAI>() == null)
            {
                if (logHits)
                    Debug.Log($"[EnemyProjectile] ISKA — '{hit.collider.name}' engeline çarptı " +
                              $"(layer {LayerMask.LayerToName(hit.collider.gameObject.layer)}, " +
                              $"{hit.distance:0.00} m sonra). " +
                              $"Oyuncuya en yakın mesafe: {closestApproach:0.00} m", this);
                PoolManager.Release(gameObject);
                return;
            }
        }

        prevPos = transform.position;
        transform.position += transform.forward * step;

        life -= Time.deltaTime;
        if (life <= 0f)
        {
            if (logHits)
                Debug.Log($"[EnemyProjectile] ISKA — suresi doldu. " +
                          $"Oyuncuya en yakin mesafe: {closestApproach:0.00} m", this);
            PoolManager.Release(gameObject);
        }
    }

    void HitPlayer(Health playerHealth)
    {
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage);
            if (logHits)
                Debug.Log($"[EnemyProjectile] ISABET — {damage} hasar. " +
                          $"Kalan can: {playerHealth.Current:0}/{playerHealth.Max:0}", this);
        }
        else if (logHits)
        {
            Debug.LogWarning("[EnemyProjectile] Oyuncuya degdi ama Health bulunamadi — " +
                             "hasar UYGULANMADI.", this);
        }
        PoolManager.Release(gameObject);
    }

    // Iska teshisi icin: oyuncuya en fazla ne kadar yaklasti. Sadece log acikken calisir.
    void TrackClosestApproach()
    {
        if (!logHits) return;
        if (playerTr == null)
        {
            var pm = FindAnyObjectByType<PlayerMovement>();
            if (pm == null) return;
            playerTr = pm.transform;
        }
        float d = Vector3.Distance(transform.position, playerTr.position + Vector3.up * 0.9f);
        if (d < closestApproach) closestApproach = d;
    }
}
}

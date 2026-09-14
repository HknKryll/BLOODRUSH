using UnityEngine;
using UnityEngine.Events;
using Bloodrush.Enemy;
using Bloodrush.Shared;
using Bloodrush.UI;

namespace Bloodrush.Flow
{
// "Aşırı Yük" bulmacasının kalbi: jeneratörü besleyen şarj bölgesi.
//
// KURAL: Jeneratör YALNIZCA bu bölgenin İÇİNDE ölen düşmanlardan şarj olur. Bölge
// dışında öldürülen düşman hiçbir şey vermez. Düşmanlar bölgeden uzaklaşmaya
// çalıştığı için (bkz. ZoneAvoidance) oyuncu onları kancayla İÇERİ ÇEKMEK zorunda —
// odanın bütün fikri bu. Kancanın düşman çekme özelliği oyunda zaten var
// (GrapplingHook: hookedEnemy.StartBeingPulled) ama neredeyse hiç kullanılmıyordu;
// burada ana araç hâline geliyor.
//
// İÇERİDE Mİ sorusu trigger defterine değil, ölüm ANINDA yapılan bir sınır testine
// dayanıyor (box.bounds.Contains). Trigger giriş/çıkış defteri tutmak, düşman ölürken
// collider'ı kapanırsa/sıra değişirse sessizce yanlış cevap verebilirdi; sınır testi
// tek satır ve her zaman doğru.
[RequireComponent(typeof(BoxCollider))]
public class ChargeZone : MonoBehaviour
{
    [Header("Şarj")]
    [Tooltip("Bölge içinde ölen düşman başına eklenen şarj.")]
    [SerializeField] float chargePerKill = 12f;
    [Tooltip("Kapının açılması için gereken toplam şarj.")]
    [SerializeField] float chargeNeeded = 100f;
    [Tooltip("Şarj zamanla sızsın mı? 0 = sızmaz. Oda çok kolaysa küçük bir değer ver.")]
    [SerializeField] float decayPerSecond = 0f;

    [Header("Görsel")]
    [Tooltip("Zemin halkasının parçaları (kenar çubukları + köşe direkleri). Hepsi TEK bir " +
             "materyal örneğini paylaşır, şarj arttıkça birlikte renk değiştirir.")]
    [SerializeField] Renderer[] ringRenderers;
    [SerializeField] Color emptyColor = new Color(0.9f, 0.25f, 0.15f);
    [SerializeField] Color fullColor  = new Color(0.3f, 1f, 0.5f);
    [Tooltip("Emissive parlaklık çarpanı (HDRP/Unlit renk çarpanı).")]
    [SerializeField] float glow = 2.5f;

    [Header("Geri bildirim")]
    [SerializeField] bool showNotifications = true;

    [Header("Olaylar")]
    [Tooltip("Şarj her değiştiğinde (0-1 normalize).")]
    public UnityEvent<float> onChargeChanged;
    [Tooltip("Şarj dolduğunda BİR KEZ.")]
    public UnityEvent onCharged;

    BoxCollider box;
    Material    ringMat;
    float       charge;
    bool        fullFired;

    public float Charge01 => chargeNeeded <= 0f ? 1f : Mathf.Clamp01(charge / chargeNeeded);
    public bool  IsFull   => fullFired;
    public Vector3 Center => box != null ? box.bounds.center : transform.position;

    void Reset()
    {
        var b = GetComponent<BoxCollider>();
        if (b != null)
        {
            b.isTrigger = true;
            b.size      = new Vector3(8f, 4f, 8f);
            b.center    = new Vector3(0f, 2f, 0f);
        }
    }

    void Awake()
    {
        box = GetComponent<BoxCollider>();
        if (box != null) box.isTrigger = true;   // asla oyuncuyu/düşmanı durdurmasın

        // TEK materyal örneği üretip halkanın bütün parçalarına dağıtıyoruz: hem tek
        // yerden boyanıyor hem de proje asset'ini (sharedMaterial) kirletmiyoruz.
        if (ringRenderers != null && ringRenderers.Length > 0)
        {
            foreach (var r in ringRenderers)
            {
                if (r == null) continue;
                if (ringMat == null) ringMat = r.material;   // ilkinde örnek oluşur
                else                 r.sharedMaterial = ringMat;
            }
        }
        ApplyVisual();
    }

    void Update()
    {
        if (decayPerSecond > 0f && charge > 0f && !fullFired)
        {
            charge = Mathf.Max(0f, charge - decayPerSecond * Time.deltaTime);
            ApplyVisual();
            onChargeChanged?.Invoke(Charge01);
        }
    }

    public bool Contains(Vector3 worldPoint)
        => box != null && box.bounds.Contains(worldPoint);

    // OverloadRoom doğurduğu HER düşman için bir kez çağırır. Abonelik düşmanın KENDİ
    // Health'ine bağlanıyor (WaveDirector'daki kanıtlanmış desen) — global bir olay
    // kullanmıyoruz, çünkü o oyuncunun ölümünü de tetiklerdi.
    public void Register(EnemyAI ai, Health health)
    {
        if (ai == null || health == null) return;
        health.onDeath.AddListener(() => OnEnemyDied(ai));
    }

    void OnEnemyDied(EnemyAI ai)
    {
        if (ai == null) return;

        // Ayak hizası değil GÖVDE merkezi: havada çekilirken ölen düşmanın ayağı
        // bölgenin altında/dışında kalabiliyor.
        Vector3 p = ai.transform.position + Vector3.up * 0.9f;

        if (!Contains(p))
        {
            if (showNotifications) Notification.Show("BÖLGE DIŞINDA — ŞARJ YOK");
            Debug.Log($"[ChargeZone] '{ai.name}' bölge DIŞINDA öldü, şarj eklenmedi.", this);
            return;
        }

        AddCharge(chargePerKill);
    }

    public void AddCharge(float amount)
    {
        if (fullFired || amount <= 0f) return;

        charge = Mathf.Min(chargeNeeded, charge + amount);
        ApplyVisual();
        onChargeChanged?.Invoke(Charge01);

        Debug.Log($"[ChargeZone] +{amount:0} şarj → {charge:0}/{chargeNeeded:0} " +
                  $"(%{Charge01 * 100f:0})", this);

        if (charge >= chargeNeeded)
        {
            fullFired = true;
            if (showNotifications) Notification.Show("JENERATÖR DOLU — ÇIKIŞ AÇILDI", 3f);
            onCharged?.Invoke();
        }
        else if (showNotifications)
        {
            Notification.Show($"ŞARJ %{Charge01 * 100f:0}");
        }
    }

    // Kurulum sırasında OverloadRoomBuilder çağırır.
    public void Configure(Renderer[] ring) => ringRenderers = ring;

    public void ResetCharge()
    {
        charge    = 0f;
        fullFired = false;
        ApplyVisual();
        onChargeChanged?.Invoke(0f);
    }

    void ApplyVisual()
    {
        if (ringMat == null) return;
        Color c = Color.Lerp(emptyColor, fullColor, Charge01) * glow;
        // HDRP/Unlit ve HDRP/Lit farklı özellik adları kullanıyor — hangisi varsa o.
        if (ringMat.HasProperty("_UnlitColor"))    ringMat.SetColor("_UnlitColor", c);
        if (ringMat.HasProperty("_EmissiveColor")) ringMat.SetColor("_EmissiveColor", c);
        if (ringMat.HasProperty("_BaseColor"))     ringMat.SetColor("_BaseColor", c);
    }

    void OnDrawGizmosSelected()
    {
        var b = GetComponent<BoxCollider>();
        if (b == null) return;
        Gizmos.color  = new Color(1f, 0.6f, 0.1f, 0.25f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(b.center, b.size);
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
        Gizmos.DrawWireCube(b.center, b.size);
    }
}
}

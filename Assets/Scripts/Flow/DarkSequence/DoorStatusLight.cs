using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Bloodrush.Flow
{
// Kapinin kilitli/acik oldugunu gosteren durum lambasi. Kilitliyken KIRMIZI ve nabiz gibi
// atar, acilinca YESILe doner ve sabitlenir — oyuncu bulmacayi cozdugunu uzaktan gorur.
//
// Kapiyi dinlemez, EVENT ile surulur: ElevatorDoor kendi durumunu disariya acmiyor ve
// zaten projedeki tum bulmaca baglantilari UnityEvent uzerinden gidiyor. Ayni event'e hem
// kapiyi hem bu lambayi bagla:
//   FusePanel.onPowered      -> ElevatorDoor.Open  +  DoorStatusLight.SetOpen
//   ValveSequence.onBothOpen -> ElevatorDoor.Open  +  DoorStatusLight.SetOpen
//   PushObstacle.onPushed    -> ElevatorDoor.Open  +  DoorStatusLight.SetOpen
public class DoorStatusLight : MonoBehaviour
{
    [Header("Isiklar")]
    [Tooltip("KOLAY YOL: isiklari tek tek surukleme; hepsini iceren KOK objeyi buraya " +
             "surukle, altindaki tum Light bilesenleri otomatik toplanir.")]
    [SerializeField] Transform lightsRoot;
    [Tooltip("Elle liste (Lights Root doluysa gerek yok).")]
    [SerializeField] Light[] lights;

    [Header("Kilitli")]
    [SerializeField] Color lockedColor = new Color(1f, 0.15f, 0.12f);
    [SerializeField] float lockedLumen = 500f;
    [Tooltip("Kilitliyken nabiz gibi atsin mi? Sabit kirmizidan daha cok dikkat ceker.")]
    [SerializeField] bool  pulseWhileLocked = true;
    [SerializeField] float pulseSpeed = 2.2f;
    [Tooltip("Nabizin en dusuk noktasi (kilitli parlakligin carpani).")]
    [Range(0f, 1f)]
    [SerializeField] float pulseFloor = 0.35f;

    [Header("Acik")]
    [SerializeField] Color openColor = new Color(0.25f, 1f, 0.35f);
    [SerializeField] float openLumen = 1400f;
    [Tooltip("Acilirken parlakligin oturma suresi (sn).")]
    [SerializeField] float openFade = 0.5f;

    HDAdditionalLightData[] hd;
    bool  isOpen;
    float openT;

    void Awake()
    {
        if (lightsRoot != null && (lights == null || lights.Length == 0))
            lights = lightsRoot.GetComponentsInChildren<Light>(true);

        if (lights == null || lights.Length == 0)
        {
            Debug.LogWarning("[DoorStatusLight] Isik bulunamadi — Lights Root ata.", this);
            return;
        }

        hd = new HDAdditionalLightData[lights.Length];
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] == null) continue;
            hd[i] = lights[i].GetComponent<HDAdditionalLightData>();
            if (hd[i] == null) hd[i] = lights[i].gameObject.AddComponent<HDAdditionalLightData>();
            hd[i].EnableShadows(false);
            lights[i].enabled = true;
        }
        Apply(lockedColor, lockedLumen);
    }

    // UnityEvent'ten cagir: kapi acildi.
    public void SetOpen()
    {
        if (isOpen) return;
        isOpen = true;
        openT  = 0f;
        Debug.Log($"[DoorStatusLight] Acik duruma gecti: {name}", this);
    }

    // UnityEvent'ten cagir: kapi tekrar kilitlendi (ihtiyac olursa).
    public void SetLocked()
    {
        isOpen = false;
        Apply(lockedColor, lockedLumen);
    }

    void Update()
    {
        if (lights == null || lights.Length == 0) return;

        if (isOpen)
        {
            // Kirmizidan yesile yumusak gecis — ani renk sicramasi ucuz durur.
            if (openT < 1f)
            {
                openT = openFade <= 0f ? 1f : Mathf.Min(1f, openT + Time.deltaTime / openFade);
                Apply(Color.Lerp(lockedColor, openColor, openT),
                      Mathf.Lerp(lockedLumen, openLumen, openT));
            }
            return;
        }

        if (!pulseWhileLocked) return;

        // Sinus 0..1 -> pulseFloor..1 araligina sikistirilir; isik hic sonmez, sadece zayiflar.
        float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        Apply(lockedColor, lockedLumen * Mathf.Lerp(pulseFloor, 1f, pulse));
    }

    void Apply(Color color, float lumen)
    {
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] == null) continue;
            lights[i].color = color;
            float scaled = lumen * DarkSceneExposure.LightScale;
            if (hd != null && hd[i] != null) hd[i].SetIntensity(scaled, LightUnit.Lumen);
            else                             lights[i].intensity = scaled;
        }
    }
}
}

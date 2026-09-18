using System.Collections;
using UnityEngine;
using Bloodrush.Shared.Audio;

namespace Bloodrush.Flow
{
// ADIM 7 — Tek bir valf/kol. Kendi başına hiçbir karar vermez: E'ye basılınca ValveSequence'tan
// çevirme (mini oyun) ister, açılma/kapanma emrini de ondan alır. Böylece iki valf birbirinden
// habersiz kalır ve mantık tek yerde toplanır.
public class ValveInteractable : ProximityInteractable
{
    [Header("Valf")]
    [SerializeField] string valveName = "VALF";
    [Tooltip("Çevrilince dönecek görsel parça. Boşsa altındaki 'Wheel' adlı obje kullanılır " +
             "(valf placeholder prefab'ında hazır); o da yoksa sadece durum değişir.")]
    [SerializeField] Transform wheel;
    [Tooltip("Çarkın döndüğü yerel eksen (yönü ve işareti kullanılır, büyüklüğü değil).")]
    [SerializeField] Vector3   wheelOpenEuler = new Vector3(0f, 0f, 90f);
    [Tooltip("Mini oyun boyunca çarkın toplam dönüşü (derece). Her başarılı basış bir parçası.")]
    [SerializeField] float     openTurnDegrees = 540f;
    [SerializeField] float     turnDuration   = 0.6f;

    [Header("Ses")]
    [Tooltip("Valf tamamen açıldığında.")]
    [SerializeField] AudioClip turnClip;
    [Tooltip("Süre dolup kendiliğinden kapandığında.")]
    [SerializeField] AudioClip closeClip;
    [SerializeField] [Range(0f,2f)] float volume = 1f;

    [Tooltip("Boşsa sahnedeki ilk ValveSequence otomatik bulunur.")]
    [SerializeField] ValveSequence sequence;

    public bool   IsOpen    { get; private set; }
    public string ValveName => valveName;
    public string DisplayName
    {
        get => string.IsNullOrEmpty(displayName) ? valveName : displayName;
        set => displayName = value;
    }
    // Kameranın döneceği ve seslerin çıkacağı nokta: çark, yoksa objenin görsel merkezi.
    public Vector3 AimTarget => wheel != null ? wheel.position : AimPoint();

    SfxPlayer  sfx;
    Quaternion closedRot;
    Coroutine  turning;
    string     displayName;
    float      wheelAngle;

    void Start()
    {
        sfx = SfxPlayer.Create(gameObject, spatialBlend: 1f);
        if (wheel == null)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t != transform && t.name == "Wheel") { wheel = t; break; }
        }
        if (wheel != null) closedRot = wheel.localRotation;
        if (sequence == null) sequence = FindFirstObjectByType<ValveSequence>();
    }

    protected override string PromptText =>
        IsOpen || (sequence != null && (sequence.AnyTurning || sequence.IsSolved)) ? null : $"[E] {DisplayName} Çevir";

    protected override void OnInteract()
    {
        if (IsOpen) return;
        if (sequence != null) sequence.RequestTurn(this, interactKey);
        else Debug.LogWarning("[ValveInteractable] ValveSequence bulunamadı.", this);
    }

    // ValveMiniGame çağırır: 0 = kapalı, 1 = tamamen çevrildi.
    public void SetTurnProgress(float progress)
    {
        TurnWheelTo(Mathf.Clamp01(progress) * openTurnDegrees, turnDuration * 0.45f);
    }

    // ValveSequence çağırır: mini oyun bitti, valf açık.
    public void SetOpen(bool open)
    {
        IsOpen = open;
        ClearPrompt();
        sfx?.Play(open ? turnClip : closeClip, volume);
        TurnWheelTo(open ? openTurnDegrees : 0f, turnDuration);
    }

    // ValveSequence çağırır: süre dolunca valf kendiliğinden kapanır.
    public void ForceClose()
    {
        if (!IsOpen) return;
        SetOpen(false);
    }

    void TurnWheelTo(float angle, float duration)
    {
        if (wheel == null) return;
        if (turning != null) StopCoroutine(turning);
        turning = StartCoroutine(TurnWheel(angle, duration));
    }

    // Açıyı skaler olarak sür: 180 dereceden büyük dönüşlerde Quaternion.Slerp kısa yoldan
    // geri dönerdi.
    IEnumerator TurnWheel(float target, float duration)
    {
        Vector3 axis = wheelOpenEuler.sqrMagnitude > 0.0001f ? wheelOpenEuler.normalized : Vector3.forward;
        float from = wheelAngle;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            wheelAngle = Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t / duration));
            wheel.localRotation = closedRot * Quaternion.AngleAxis(wheelAngle, axis);
            yield return null;
        }
        wheelAngle = target;
        wheel.localRotation = closedRot * Quaternion.AngleAxis(wheelAngle, axis);
        turning = null;
    }
}
}

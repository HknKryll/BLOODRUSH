using System.Collections;
using UnityEngine;
using Bloodrush.Shared.Audio;

namespace Bloodrush.Flow
{
// ADIM 7 — Tek bir valf/kol. Kendi başına hiçbir karar vermez: durumunu ValveSequence'a
// bildirir, kapanma emrini de ondan alır. Böylece iki valf birbirinden habersiz kalır ve
// mantık tek yerde toplanır.
public class ValveInteractable : ProximityInteractable
{
    [Header("Valf")]
    [SerializeField] string valveName = "VALF";
    [Tooltip("Çevrilince dönecek görsel parça (opsiyonel — boşsa sadece durum değişir).")]
    [SerializeField] Transform wheel;
    [SerializeField] Vector3   wheelOpenEuler = new Vector3(0f, 0f, 90f);
    [SerializeField] float     turnDuration   = 0.6f;

    [Header("Ses")]
    [SerializeField] AudioClip turnClip;
    [SerializeField] AudioClip closeClip;
    [SerializeField] [Range(0f,2f)] float volume = 1f;

    [Tooltip("Boşsa sahnedeki ilk ValveSequence otomatik bulunur.")]
    [SerializeField] ValveSequence sequence;

    public bool   IsOpen    { get; private set; }
    public string ValveName => valveName;

    SfxPlayer  sfx;
    Quaternion closedRot;
    Coroutine  turning;

    void Start()
    {
        sfx = SfxPlayer.Create(gameObject, spatialBlend: 1f);
        if (wheel != null) closedRot = wheel.localRotation;
        if (sequence == null) sequence = FindFirstObjectByType<ValveSequence>();
    }

    protected override string PromptText => IsOpen ? null : $"[E] {valveName} Çevir";

    protected override void OnInteract()
    {
        if (IsOpen) return;
        SetOpen(true);
        if (sequence != null) sequence.OnValveOpened(this);
        else Debug.LogWarning("[ValveInteractable] ValveSequence bulunamadı.", this);
    }

    // ValveSequence çağırır: süre dolunca valf kendiliğinden kapanır.
    public void ForceClose()
    {
        if (!IsOpen) return;
        SetOpen(false);
    }

    void SetOpen(bool open)
    {
        IsOpen = open;
        ClearPrompt();
        sfx?.Play(open ? turnClip : closeClip, volume);

        if (wheel == null) return;
        if (turning != null) StopCoroutine(turning);
        turning = StartCoroutine(TurnWheel(open ? closedRot * Quaternion.Euler(wheelOpenEuler)
                                               : closedRot));
    }

    IEnumerator TurnWheel(Quaternion target)
    {
        Quaternion from = wheel.localRotation;
        float t = 0f;
        while (t < turnDuration)
        {
            t += Time.deltaTime;
            wheel.localRotation = Quaternion.Slerp(from, target,
                                                    Mathf.SmoothStep(0f, 1f, t / turnDuration));
            yield return null;
        }
        wheel.localRotation = target;
        turning = null;
    }
}
}

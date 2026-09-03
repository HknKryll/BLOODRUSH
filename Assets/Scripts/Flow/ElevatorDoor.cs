using System.Collections;
using UnityEngine;
using Bloodrush.Player;
using Bloodrush.Shared.Audio;

// Asansör kapısı: oyuncu kapının önüne (bu objenin trigger'ına) yaklaşınca
// otomatik açılır. Kapanma dışarıdan tetiklenir — Elevator.cs, oyuncu asansör
// platformuna binip iniş başlarken CloseAndWait() çağırır.
//
// Kullanım: kapı eşiğine (asansör platformunun biraz önüne) boş bir GameObject
// koy, bu script'i ekle — üzerine BoxCollider (isTrigger) gelir. leftDoor/
// rightDoor alanlarına kapı kanadı mesh'lerini ata (tek kanatlı kapı için
// sadece leftDoor'u ata). Açık/kapalı offsetleri kanadın LOCAL pozisyonuna
// göre — kapı ilk kapalı haldeyken sahnede nereye konduysa o, "kapalı" pozisyon
// olarak kaydedilir.
namespace Bloodrush.Flow
{
[RequireComponent(typeof(BoxCollider))]
public class ElevatorDoor : MonoBehaviour
{
    [Header("Davranış")]
    [Tooltip("Oyuncu trigger'a girince otomatik açılsın mı? ÇIKIŞ kapısında KAPAT — o sadece asansör varınca (Elevator.exitDoor) açılır.")]
    [SerializeField] bool autoOpenOnApproach = true;

    [Header("Kapı Kanatları")]
    [SerializeField] Transform leftDoor;
    [SerializeField] Transform rightDoor;
    [SerializeField] Vector3   leftOpenOffset  = new Vector3(-1.2f, 0f, 0f);
    [SerializeField] Vector3   rightOpenOffset = new Vector3( 1.2f, 0f, 0f);
    [SerializeField] float     moveDuration    = 0.8f;

    [Header("Ses")]
    [SerializeField] AudioClip openClip;
    [SerializeField] [Range(0f,1f)] float openVolume  = 0.8f;
    [SerializeField] AudioClip closeClip;
    [SerializeField] [Range(0f,1f)] float closeVolume = 0.8f;

    Vector3   leftClosedPos, rightClosedPos;
    bool      isOpen;
    Coroutine running;
    SfxPlayer sfx;

    void Start()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        sfx = SfxPlayer.Create(gameObject, spatialBlend: 0f);
        if (leftDoor)  leftClosedPos  = leftDoor.localPosition;
        if (rightDoor) rightClosedPos = rightDoor.localPosition;

        if (leftDoor == null && rightDoor == null)
            Debug.LogWarning("ElevatorDoor: leftDoor/rightDoor atanmadı — kapı hiç hareket etmeyecek.", this);
    }

    // Builder'ların (ör. EntranceFacadeBuilder) programatik kurulumu için — Inspector'dan
    // elle sürüklemek yerine kod ile kanat/offset atar. Mevcut Inspector kullanımını
    // etkilemez, sadece private alanlara ek bir giriş yolu açar.
    public void Configure(Transform left, Transform right, Vector3 leftOpen, Vector3 rightOpen, bool autoOpen)
    {
        leftDoor = left;
        rightDoor = right;
        leftOpenOffset = leftOpen;
        rightOpenOffset = rightOpen;
        autoOpenOnApproach = autoOpen;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!autoOpenOnApproach) return;   // çıkış kapısı: sadece Elevator varışta açar
        if (other.GetComponentInParent<PlayerMovement>() != null) Open();
    }

    public void Open()
    {
        if (isOpen) return;
        isOpen = true;
        sfx.Play(openClip, openVolume);
        Restart(MoveDoors(leftClosedPos + leftOpenOffset, rightClosedPos + rightOpenOffset));
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        sfx.Play(closeClip, closeVolume);
        Restart(MoveDoors(leftClosedPos, rightClosedPos));
    }

    // Elevator.cs bunu çağırır: kapıyı kapatır ve kanatlar yerine oturana kadar bekler.
    public IEnumerator CloseAndWait()
    {
        Close();
        yield return new WaitForSeconds(moveDuration);
    }

    void Restart(IEnumerator routine)
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(routine);
    }

    IEnumerator MoveDoors(Vector3 leftTarget, Vector3 rightTarget)
    {
        Vector3 leftStart  = leftDoor  ? leftDoor.localPosition  : Vector3.zero;
        Vector3 rightStart = rightDoor ? rightDoor.localPosition : Vector3.zero;
        float t = 0f;
        while (t < moveDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / moveDuration));
            if (leftDoor)  leftDoor.localPosition  = Vector3.Lerp(leftStart,  leftTarget,  p);
            if (rightDoor) rightDoor.localPosition = Vector3.Lerp(rightStart, rightTarget, p);
            yield return null;
        }
        if (leftDoor)  leftDoor.localPosition  = leftTarget;
        if (rightDoor) rightDoor.localPosition = rightTarget;
    }
}
}

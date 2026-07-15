using UnityEngine;

// Tutorial ipucu bölgesi: oyuncu girince ekranda ipucu yazısı belirir.
// Kullanım: boş GO + BoxCollider (isTrigger) + bu script.
// Örn. koridora "[Sol Tık] Ateş et", kanca şaftına "[E] Kancala ve yukarı çık",
// wall-jump şaftına "Havada [Space] ile duvardan duvara zıpla" yaz.
using Bloodrush.Player;

namespace Bloodrush.Flow
{
[RequireComponent(typeof(BoxCollider))]
public class TutorialHint : MonoBehaviour
{
    [TextArea]
    [SerializeField] string hintText = "";
    [SerializeField] bool   oneShot = false;          // true: bir kez gösterildikten sonra kapanır
    [SerializeField] float  autoHideSeconds = 0f;     // 0 = bölgeden çıkınca gizlenir; >0 = bu süre sonra otomatik

    bool  used;
    bool  showing;
    float hideAt = -1f;

    void Start()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (used) return;
        if (other.GetComponentInParent<PlayerMovement>() == null) return;

        TutorialHintUI.Show(hintText);
        showing = true;
        hideAt  = autoHideSeconds > 0f ? Time.time + autoHideSeconds : -1f;
    }

    void OnTriggerExit(Collider other)
    {
        if (!showing) return;
        if (other.GetComponentInParent<PlayerMovement>() == null) return;
        Dismiss();
    }

    void Update()
    {
        if (showing && hideAt > 0f && Time.time >= hideAt)
            Dismiss();
    }

    void Dismiss()
    {
        TutorialHintUI.Hide();
        showing = false;
        hideAt  = -1f;
        if (oneShot) used = true;
    }
}
}

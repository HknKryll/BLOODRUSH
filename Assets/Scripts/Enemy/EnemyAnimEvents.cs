using UnityEngine;

namespace Bloodrush.Enemy
{
// Animation Event koprusu.
//
// Unity bir Animation Event'i YALNIZCA Animator'in bulundugu GameObject'teki bilesenlere
// gonderir. Bizim dusmanlarda AI script KOKTE, Animator ise model child'inda duruyor — event
// dogrudan AI'ya ulasamaz. Bu bilesen MODEL objesine (Animator'in yanina) eklenir ve olayi
// yukaridaki AI'ya iletir.
//
// Klibe eklenecek event fonksiyonlari: AnimFire, AnimDodgeEnd, AnimAttackHit.
[DisallowMultipleComponent]
public class EnemyAnimEvents : MonoBehaviour
{
    public interface IAnimEventReceiver
    {
        void AnimFire();
        void AnimDodgeEnd();
    }

    IAnimEventReceiver receiver;
    bool searched;

    IAnimEventReceiver Receiver
    {
        get
        {
            if (!searched)
            {
                searched = true;
                receiver = GetComponentInParent<IAnimEventReceiver>();
                if (receiver == null)
                    Debug.LogWarning("[EnemyAnimEvents] Ust objelerde AI bulunamadi — " +
                                     "animasyon olaylari bos gidiyor.", this);
            }
            return receiver;
        }
    }

    // Klipteki ates karesine eklenir: mermi/atis TAM o karede cikar.
    public void AnimFire() => Receiver?.AnimFire();

    // Dodge klibinin sonuna eklenebilir (opsiyonel; kod zaten sureyle de bitiriyor).
    public void AnimDodgeEnd() => Receiver?.AnimDodgeEnd();
}
}

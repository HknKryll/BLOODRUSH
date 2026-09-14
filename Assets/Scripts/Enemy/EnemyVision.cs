using UnityEngine;
using Bloodrush.Player;

namespace Bloodrush.Enemy
{
// Dusmanlarin "oyuncuyu gorebiliyor muyum" sorusunun tek merci.
//
// Hem menzilli (EnemyRangedAttack) hem yakin dovus (EnemyMeleeAttack) ayni kontrole
// ihtiyac duyuyor. Projedeki "her builder kendi yardimci kopyasini tasir" kurali YAPI
// KURUCULAR icindir; bu bir fizik sorgusu ve tek yerde durmasi dogru — ayni raycast
// mantiginin iki kopyasi zamanla birbirinden ayrilirdi.
//
// Engel tanimi: duvar/zemin/platform. Dusmanin KENDI govdesi, oyuncunun kendisi ve
// diger dusmanlar engel SAYILMAZ (yoksa kalabalikta kimse ates edemezdi).
public static class EnemyVision
{
    // NonAlloc + paylasilan buffer: bu metot kare basina dusman basina birkac kez
    // calisiyor, RaycastAll her cagrida coplu bir dizi ayiriyordu.
    static readonly RaycastHit[] buffer = new RaycastHit[32];

    // Oyuncunun AYAGINDAN itibaren orneklenen noktalar: bel, gogus, bas.
    // Oyuncu kapsulu 2 m, yani 1.8 tam kafa hizasi.
    //
    // NEDEN UC NOKTA: tek nokta (bel) orneklemek kismi siperi anlamsiz kiliyordu.
    // Platform kenarinda dururken belin acikta kalip basin engelli olabiliyor; ucunun
    // de acik olmasini sart kosunca dosemenin altina sokulmak gercekten siper sagliyor.
    public static readonly float[] TorsoHeights = { 0.4f, 1.0f, 1.7f };

    static CharacterController playerCC;

    // DIKKAT — BU METOT OLMADAN HER YUKSEKLIK HESABI YANLIS CIKAR.
    // Oyuncunun pivotu ayaklarinda DEGIL: CharacterController center.y = -0.17,
    // height = 2 → pivot, ayaktan 1.17 m YUKARIDA. Dusmanin pivotu ise
    // (NavMeshAgent baseOffset 0) tam ayagindadir. Iki pivotu dogrudan karsilastirmak
    // dumduz zeminde bile 1.17 m'lik sahte bir kot farki uretiyordu — yakin dovus
    // menzili bu yuzden hic tutmuyordu, dusmanlar sadece itip duruyordu.
    public static Vector3 PlayerFeet(Transform player)
    {
        if (player == null) return Vector3.zero;
        if (playerCC == null || playerCC.transform != player)
            playerCC = player.GetComponent<CharacterController>();
        if (playerCC == null) return player.position;
        return player.position + new Vector3(0f, playerCC.center.y - playerCC.height * 0.5f, 0f);
    }

    // Tek isin. Arada engel yoksa true.
    public static bool Clear(Vector3 origin, Vector3 target, Transform self)
    {
        Vector3 dir  = target - origin;
        float   dist = dir.magnitude;
        if (dist < 0.01f) return true;

        int n = Physics.RaycastNonAlloc(origin, dir / dist, buffer, dist, ~0,
                                        QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var c = buffer[i].collider;
            if (self != null && c.transform.IsChildOf(self)) continue;   // kendi govden
            if (c.GetComponentInParent<PlayerMovement>() != null) continue;   // oyuncu engel degil
            if (c.GetComponentInParent<EnemyAI>() != null) continue;          // diger dusmanlar engel degil
            return false;   // duvar / zemin
        }
        return true;
    }

    // Oyuncunun uzerinde birden cok nokta — HEPSI acik olmali.
    // Yukseklikler AYAKTAN olculur (bkz. PlayerFeet).
    public static bool ClearToPlayer(Vector3 origin, Transform player, Transform self, float[] heights)
    {
        if (player == null) return false;
        Vector3 feet = PlayerFeet(player);
        foreach (float h in heights)
            if (!Clear(origin, feet + Vector3.up * h, self)) return false;
        return true;
    }

    // Yakin dovus icin: govdeden govdeye tek isin.
    //
    // YAKIN ALAN MUAFIYETI: iki govde birbirine degiyorsa aralarina duvar/zemin
    // giremez. Kalabalik bir cevrelemede isin atmak (gorseldeki gibi 5 dusman ust uste)
    // sahte "engel" uretip butun yakin dovusu kilitleyebiliyor. Bu yuzden yakin ve ayni
    // kottaki hedefte hic sorgu yapilmaz — kontrol sadece gercekten uzak ya da kot farki
    // olan durumlar icin var (zeminin icinden vurma).
    public static bool ClearForMelee(Transform enemy, Transform player)
    {
        if (enemy == null || player == null) return false;

        Vector3 feet = PlayerFeet(player);
        Vector3 d    = feet - enemy.position;
        float   dy   = Mathf.Abs(d.y);
        d.y = 0f;

        if (d.magnitude < 1.5f && dy < 1.0f) return true;   // dip dibe — sorgu yok

        return Clear(enemy.position + Vector3.up * 1.0f, feet + Vector3.up * 1.0f, enemy);
    }
}
}

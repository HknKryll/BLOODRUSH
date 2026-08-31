using Bloodrush.Player;

namespace Bloodrush.Flow
{
// Oyun-turu (run) ilerlemesi — sahneler arası KALICI (statik alanlar sahne yeniden
// yüklenince ve checkpoint respawn'da yaşar). Yeni oyun başında ResetRun() ile sıfırlanır
// (CH1 GameFlow, resetProgressOnStart=true). Revolver her zaman açık; shotgun/LMG CH2
// boss'u öldürülüp yerdeki silahlar alınınca açılır.
public static class GameProgress
{
    public static bool ShotgunUnlocked;
    public static bool LmgUnlocked;

    public static void ResetRun()
    {
        ShotgunUnlocked = false;
        LmgUnlocked     = false;
    }

    public static void Unlock(PlayerShoot.Firearm w)
    {
        if      (w == PlayerShoot.Firearm.Shotgun) ShotgunUnlocked = true;
        else if (w == PlayerShoot.Firearm.Lmg)     LmgUnlocked     = true;
    }

    public static bool IsUnlocked(PlayerShoot.Firearm w)
    {
        return w switch
        {
            PlayerShoot.Firearm.Shotgun => ShotgunUnlocked,
            PlayerShoot.Firearm.Lmg     => LmgUnlocked,
            _                           => true,   // Revolver hep açık
        };
    }
}
}

// Parry edilebilen düşmanlar (EnemyAI, BossAI) için ortak arayüz.
// PlayerParry bunu arayarak hem normal düşmanı hem boss'u parry'leyebilir.

namespace Bloodrush.Enemy
{
public interface IParryable
{
    bool IsParryable { get; }
    void Parry(float stunDuration);
}
}

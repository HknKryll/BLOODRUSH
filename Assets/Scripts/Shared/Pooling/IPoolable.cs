namespace Bloodrush.Shared.Pooling
{
// Havuzdan alinip birakilan bilesenler bunu uygular (bkz. PoolManager,
// BLOODRUSH_MIMARI_KURALLARI.md §3).
//
// OnSpawned(): eskiden Start()'ta olan "her seferinde sifirdan kurulum"
// mantigi buraya tasinir — Unity Start()'i havuzlanan bir nesnede SADECE
// ILK ureyiminde bir kez cagirir, sonraki SetActive(true)'larda cagirmaz.
// OnDespawned(): havuza donerken (deaktif olmadan HEMEN once) temizlik —
// bekleyen Invoke/coroutine iptali, gecici child obje temizligi vb.
public interface IPoolable
{
    void OnSpawned();
    void OnDespawned();
}
}

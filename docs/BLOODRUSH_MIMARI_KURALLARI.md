# BLOODRUSH — Mimari Kurallar ve Konvansiyonlar

> Bu doküman, yeniden yapılandırma sürecinde (pooling, veri katmanı, Enemy AI
> refactor, dalga sistemi birleştirme vb.) tüm sistemlerin tutarlı kararlarla
> yeniden yazılmasını sağlamak için var. Yeni bir sistem eklerken veya mevcut
> birini refactor ederken önce buraya bakın.
>
> Eşlik eden dosya: `BLOODRUSH_YENIDEN_YAPILANDIRMA_PLANI.md` (uygulama
> sırası ve fazlar için).

---

## 1. Katmanlama İlkesi: Veri / Mantık / Sunum

Her domain klasörü (`Player`, `Enemy`, `Weapons`, `Arena`, `Flow`, `UI`,
`Shared`) korunuyor — **tüm projeyi `Core/Systems/Presentation` gibi üst
seviye klasörlere bölmüyoruz**, çünkü mevcut domain-bazlı yapı zaten okunur
ve namespace'lerle (`Bloodrush.Player`, `Bloodrush.Enemy` vb.) uyumlu. Bunun
yerine her domain'in **içinde** üç sorumluluk ayrı tutulur:

| Katman | Sorumluluk | Kural |
|---|---|---|
| **Veri** | Denge/istatistik değerleri, konfigürasyon | `ScriptableObject`, mantık içermez, Unity runtime API'sine (Physics, Input vb.) dokunmaz |
| **Mantık** | Davranış, state machine, hesaplama | `MonoBehaviour` / plain C# sınıf, veri katmanını **tüketir**, hardcoded denge değeri içermez |
| **Sunum** | UI, VFX, ses tetikleme | Mantık katmanının event'lerini dinler, oyun kuralına karar vermez |

Örnek: `Weapons/` klasöründe `WeaponData.cs` (veri) + `PlayerFirearm.cs`
(mantık) + `WeaponAnimator.cs`/hit-effect scriptleri (sunum) bir arada
durabilir — ayrı üst klasörlere taşımaya gerek yok, ama bir dosyanın hangi
katmana ait olduğu isminden ve içeriğinden belli olmalı.

**Kural**: Bir `MonoBehaviour` içinde tekrar eden/dengelemeye açık bir sayı
görüyorsanız (hasar, menzil, hız, süre vb.) ve bu değer birden fazla
varyant için farklılaşacaksa (silah çeşidi, düşman tipi, dalga zorluğu),
bu bir **Veri** katmanı adayıdır.

> **Not (2026-09-15 karar)**: Bu "üst seviye klasör yapısı değişmiyor"
> kararı `Assets/Scripts/` altındaki domain klasörlerini kapsar. 3D içerik
> klasörleri (`Materials`, `Textures`, `Prefabs`, karakter varlıkları) bu
> kararın kapsamı dışındadır — onlar için ayrı bir plan
> (`TEMIZLIK_VE_YAPI_PLANI.md`) var ve orada yeni bir `Assets/Characters/`
> üst-seviye klasörü onaylandı. İki karar birbiriyle çelişmiyor, farklı
> katmanlara (kod vs. varlık) hitap ediyorlar.

---

## 2. Singleton Politikası

Mevcut envanter: `PauseMenuController`, `DialogueUI`, `BookUI`,
`StimulantSystem`, sahne-başı `MusicDirector`, static factory `SfxPlayer`.
Bunların hepsi **korunuyor** — zaten net, tek sorumlulu, gerekçesi belli.

**Yeni bir singleton eklemeden önce üç soru:**
1. Bu nesnenin sahne/oyun boyunca **gerçekten tek** bir mantıksal kopyası mı
   olmalı, yoksa birden fazla context'te farklı örnekleri mi olabilir?
2. Doğal bir "sahip" (owner) var mı — örneğin bir `GameFlow`/`PlayerLoadout`
   referansı üzerinden erişilebilir mi? Varsa singleton yerine referans
   geçirin.
3. `DontDestroyOnLoad` gerekiyor mu, yoksa sahne-başı (mevcut
   `MusicDirector` gibi) yeterli mi?

Cevap "hayır" ise singleton değil, **inject edilen bir referans** (Inspector
alanı ya da basit bir servis bulucu) kullanılır. Her yeni singleton, kod
yorumunda tek cümlelik bir gerekçe içermeli (mevcut kod tabanında zaten bu
alışkanlık var — `SettingsPanel`'in "iki kopya yerine tek panel" yorumu gibi,
ve `PauseMenuController`'ın eski `PauseMenu`'yü neden kapattığını açıklayan
yorumu gibi — bunu standart hale getiriyoruz).

---

## 3. Nesne Havuzu (Pooling) Sözleşmesi

Sık `Instantiate`/`Destroy` edilen her şey (mermi/grenade, hit-effect,
düşman spawn'ları) merkezi bir `PoolManager` üzerinden yönetilir.

- Havuzlanabilir nesneler `IPoolable` arayüzünü uygular: `OnSpawned()` /
  `OnDespawned()`.
- Alma/bırakma tek API üzerinden: `PoolManager.Get<T>(key)` /
  `PoolManager.Release(obj)`. Doğrudan `Instantiate`/`Destroy` çağrısı,
  pooling altyapısı devreye girdikten sonra gameplay-sık nesneler için
  **yasak** (tek seferlik/nadir nesneler — örn. `WeaponPickup` — muaf).
- Havuz büyüklükleri sabit kod yerine ilgili veri asset'inde (`WeaponData`,
  `WaveData` vb.) tutulur.
- **Uygulama sırası (2026-09-15 karar)**: pooling altyapısı, dalga
  sistemleri birleştirildikten (`WaveManager`+`WaveDirector` → tek sistem)
  ve Enemy AI ortak tabanı oturduktan **sonra** kurulacak — böylece aynı
  entegrasyon işi iki kez yapılmaz. Detay: `BLOODRUSH_YENIDEN_YAPILANDIRMA_PLANI.md`.

---

## 4. Adlandırma ve Namespace Kuralları

**Sınıf düzeyi — İngilizce:** namespace'ler, sınıf/arayüz/enum adları, dosya
adları İngilizce kalır. Bunlar Unity/C# ekosistemiyle doğrudan etkileşen
"yapısal" kimlikler ve arama/onboarding kolaylığı için evrensel kalmalı.

- Namespace: `Bloodrush.<Domain>` (mevcut kalıp aynen sürdürülür).
- Sınıf/dosya adı: PascalCase, bir dosyada bir ana `MonoBehaviour`/sınıf.
- Arayüzler `I` öneki alır (mevcut `IParryable` kalıbı — yeni `IPoolable`,
  `IDamageable` (varsa) aynı kurala uyar).
- Veri asset'leri: `<Domain>Data_<İsim>` — örn. `WeaponData_Revolver`,
  `WaveData_Ch2Arena1`.

**Metod ve değişken düzeyi — Türkçe serbest:** metod isimleri ile
private/public alan ve değişken isimleri Türkçe yazılabilir, **Türkçe
karakter kullanılmadan** (ASCII karşılıkları ile):

| Türkçe harf | ASCII karşılığı |
|---|---|
| ı | i |
| ğ | g |
| ü | u |
| ş | s |
| ö | o |
| ç | c |

Örnek: `düşman sayısı` → `dusmanSayisi`, `ateş et` → `AtesEt()`, `can
miktarı` → `canMiktari`.

**İstisnalar (İngilizce kalır):**
- Unity yaşam döngüsü metodları (`Awake`, `Start`, `Update`,
  `OnCollisionEnter` vb.) — override edildikleri için isim değiştirilemez.
- Bir interface'i implement eden imza metodları (ör. `IPoolable.OnSpawned()`)
  — interface İngilizce olduğu için implementasyonu da İngilizce kalır;
  interface'i implement eden sınıfın *diğer* yardımcı/private metodları
  Türkçe olabilir.
- Üçüncü parti/Unity API çağrıları (`NavMeshAgent.SetDestination(...)` gibi)
  doğal olarak İngilizce kalır — bunlar zaten sizin isimlendirmeniz değil.

**Kapsam:** Bu kural **bundan sonra yazılan yeni kod** için geçerlidir.
Mevcut dosyalardaki İngilizce metod/değişken isimleri, sadece dil değişikliği
amacıyla toplu olarak Türkçeleştirilmez — hiçbir mimari fayda getirmeyen saf
bir rename işi olur. Ama refactor kapsamında zaten yeniden yazılan bir sınıfın
(örn. Enemy AI temel sınıf) içindeki metodlar o esnada Türkçeleştirilebilir.

**Varlık (asset) dosya/klasör isimleri**: bu kural kod içindir; 3D içerik
klasörlerinin isimlendirmesi için ayrı bir karar `TEMIZLIK_VE_YAPI_PLANI.md`
§6.5'te var (özetle: Türkçe özel karakterler kaldırılıyor, ama zaten ASCII
olan Türkçe kelimeler — `Veri`, `Kitap1` gibi — korunuyor).

---

## 5. Event / Bağımlılık Ayrımı (Decoupling)

- Sistemler arası iletişim `UnityEvent` (Inspector'dan bağlanabilmesi
  gereken, tasarımcı-erişimli durumlar — mevcut `Health.onDeath` gibi) ya da
  saf C# `event` (yüksek frekanslı, sadece kod-içi durumlar) ile yapılır.
- Bir sistem başka bir domain'in **iç durumuna** doğrudan erişmemeli (örnek:
  `WaveManager` UI'ı doğrudan güncellemek yerine event yayınlamalı — şu an
  `UpdateUI()` ile doğrudan push ediyor, bu refactor sırasında event'e
  çevrilecek — bkz. dalga sistemleri birleştirme fazı).
- Cross-domain referans gerekiyorsa interface üzerinden (ör. `IParryable`)
  ya da event üzerinden — asla somut sınıf tipini bilerek değil.

---

## 6. Girdi (Input) Kuralı

Yeni yazılan hiçbir kod legacy `Input` sınıfına dokunmaz. Tüm yeni girdi,
Input System `InputAction`/action map üzerinden okunur. Mevcut
`KeyBindings` katmanı, girdi birleştirme fazı tamamlanana kadar geçiş
dönemi için durur, ama üzerine yeni kod eklenmez.

---

## 7. Yeni Sistem Eklerken Kontrol Listesi

Bir refactor'a veya yeni sisteme başlamadan önce:

- [ ] Denge/konfigürasyon değerleri veri katmanına (SO) mı çıkarıldı?
- [ ] Sık instantiate edilen nesneler için pooling kullanılıyor mu?
- [ ] Yeni bir singleton varsa, 3 soru testi geçti mi ve gerekçesi yorum
      olarak yazıldı mı?
- [ ] Diğer domain'lerle iletişim event/interface üzerinden mi, doğrudan
      referans üzerinden mi?
- [ ] Namespace ve dosya adlandırması kurala uyuyor mu?
- [ ] Girdi okuma yeni Input System üzerinden mi?

---

## Açık Kalan Kararlar

- `Health`/`IDamageable`: şu an tek somut `Health` sınıfı yeterli
  görünüyor; birden fazla farklı hasar-tepki davranışı gerekene kadar
  interface'e geçilmeyecek. Gerekçe olmadan `IDamageable` eklenmeyecek.
- State Pattern'e (ayrı state sınıfları) geçiş kararı, Enemy AI refactor
  adımında `BossAI`/`ExperimentBossAI` gibi karmaşık state setleri için
  ayrıca değerlendirilecek; basit enum+switch (`EnemyAI`'daki gibi) genel
  kural olarak kalıyor.

  > ✅ **Karar (2026-09-15, Faz 4 sırasında)**: enum+switch olarak KALDI,
  > ayrı state sınıflarına bölünmedi. Gerekçe: `EnemyAI`/`BossAI`/
  > `ExperimentBossAI`'nin State enum'ları ve Update() akışları birbirinden
  > yeterince farklı (kısa dövüş/menzilli hibrit vs. faz-tabanlı boss
  > saldırı setleri) — zorla ortak bir FSM/state-object iskeletine
  > sıkıştırmak, kullanıcının "davranış korunsun" önceliğine karşı
  > gereksiz risk taşırdı. Bunun yerine `EnemyAIBase`/`BossAIBase` adında
  > iki taban sınıf eklendi (`Assets/Scripts/Enemy/EnemyAIBase.cs`,
  > `BossAIBase.cs`) — SADECE gerçekten birebir aynı olan kurulum/yardımcı
  > kodu (agent/health/oyuncu referansları, `FacePlayer()`, renderer
  > önbellekleme, ölüm bildirim zinciri, boss faz/can-barı mantığı)
  > topluyor; her sınıfın kendi State enum'u ve switch'i olduğu gibi kaldı.
  > Detay için `BLOODRUSH_YENIDEN_YAPILANDIRMA_PLANI.md` Faz 4 notuna bakın.
- **Kalıcılık/Save sistemi** (§ `BLOODRUSH_YENIDEN_YAPILANDIRMA_PLANI.md`
  eski Faz 7) bu refactor'ın kapsamı **dışına** alındı — sonradan eklenecek
  ayrı bir mekanik olarak not edildi (2026-09-15 kararı).

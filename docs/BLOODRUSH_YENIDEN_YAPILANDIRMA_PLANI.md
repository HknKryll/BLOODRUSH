# BLOODRUSH — Yeniden Yapılandırma Planı

> Bu doküman, Claude Code'a (veya projede çalışan herhangi bir geliştiriciye)
> verilmek üzere hazırlanmıştır. Projenin mevcut durumu `TECH_OVERVIEW.md`
> dosyasında detaylı incelenmiştir; bu plan o incelemeye dayanarak alınan
> kararları ve uygulama sırasını tanımlar.
>
> Eşlik eden dosya: `BLOODRUSH_MIMARI_KURALLARI.md` (tüm yeni kod bu
> kurallara uymalı — aşağıda özetlenmiştir, detaylar için o dosyaya bakın).
>
> **2026-09-15 güncellemesi**: Bu plan bir kod incelemesinden geçti (bkz.
> §5 "İnceleme Notları") ve kullanıcıyla birlikte netleştirilen kararlar
> aşağıda işlendi. Faz sırası, aşağıda açıklanan bir bağımlılık çakışması
> nedeniyle orijinal halinden **değiştirildi**.
>
> **İsimlendirme notu (2026-09-15)**: `TEMIZLIK_VE_YAPI_PLANI.md` da kendi
> bağımsız "Faz 1...Faz 7" sırasını kullanıyor — iki plan aynı faz
> isimlerini paylaşıyor ama tamamen farklı işler. Karışıklığı önlemek
> için bu belgedeki fazlardan bahsederken her zaman **"Refactor planının
> Faz N'i"** denir, diğerindekiler için **"Temizlik planının Faz N'i"**.

---

## 0. Bağlam ve Hedef

- **Motor**: Unity 2022.3.62f3 LTS, HDRP 14.0.12, Mono scripting backend.
- **Oyun**: Tek oyunculu, bölüm bazlı (Ch1–Ch4) FPS/aksiyon — prosedürel
  silah animasyonu, parry/punch, grapple hook, dalga bazlı arena
  dövüşleri, düşman/boss AI, diyalog/kitap sistemi.
- **Proje aşaması**: Erken/orta geliştirme — büyük refactor'lara açık.
- **Hedef**: Performans, mekanik genişletme kolaylığı ve kod tabanı
  sürdürülebilirliğinin **hepsi** — kapsamlı, temelden başlayan bir
  yeniden yapılandırma.
- **Yaklaşım**: Sıfırdan yeniden yazım değil — çalışan sistemler
  (`SettingsStore`, ses sistemi `MusicDirector`/`RoomMusic`) korunuyor,
  zayıf katmanlar köklü şekilde yeniden kuruluyor.

## 1. Alınan Kararlar

| Konu | Karar |
|---|---|
| Netcode for GameObjects | Multiplayer planı yok → **paket tamamen kaldırılacak** |
| Öncelik | Hepsi (performans + mekanik genişletme + sürdürülebilirlik) — kapsamlı plan |
| Refactor agresifliği | Erken/orta aşama → büyük refactor'lara açık |
| Dil/isimlendirme | Sınıf/namespace/arayüz **İngilizce**; metod/değişken isimleri **Türkçe-ASCII** serbest (bkz. §4, mimari kurallar dosyası) |
| Üst seviye klasör yapısı | **Değişmiyor** — domain bazlı (`Player`, `Enemy`, `Weapons`...) korunuyor, katmanlama domain içinde sağlanıyor. *(Bu, `Assets/Scripts/` içindir — 3D içerik klasörleri ayrı bir planda ele alınıyor, bkz. `TEMIZLIK_VE_YAPI_PLANI.md`.)* |
| **Enemy AI ortak taban** (2026-09-15) | **Tam refactor** — `EnemyAI`/`BossAI`/`ExperimentBossAI` düzgün, paylaşılan bir tabana oturtulacak (bkz. Faz 4) |
| **Dalga sistemleri** (2026-09-15) | **Birleştirilecek** — `WaveManager` ve `WaveDirector` tek bir sisteme dönüşecek (bkz. Faz 5). Bu bilinçli bir tasarım değişikliği olarak kabul edildi — "davranışı koru" ilkesinin bu faz için istisnası. |
| **Girdi birleştirme riski** (2026-09-15) | Yüksek riskli kabul edildi, **alt-adımlara bölünecek** (bkz. Faz 2) |
| **Kalıcılık/Save sistemi** (2026-09-15) | Bu refactor'ın **kapsamı dışında** — sonradan eklenecek ayrı bir mekanik. Eski Faz 7 kaldırıldı, sadece not olarak §6'da duruyor. |
| **CasualHit paketi** (2026-09-15 düzeltme) | Kod incelemesinde yanlışlıkla "kullanılmıyor" denip silinmesi önerilmişti — **GUID taramasıyla `Enemy2.prefab`/`ArmoredHazard.prefab` tarafından kullanıldığı doğrulandı. SİLİNMEYECEK.** |

---

## 2. Mimari Kurallar — Özet

(Tam metin: `BLOODRUSH_MIMARI_KURALLARI.md`)

1. **Katmanlama**: her domain içinde Veri (ScriptableObject) / Mantık
   (MonoBehaviour) / Sunum (UI/VFX/ses) ayrımı — üst seviye klasör
   reorganizasyonu yok.
2. **Singleton**: yeni singleton eklemeden önce 3 soru testi (gerçekten
   tek kopya mı / doğal sahibi var mı / DontDestroyOnLoad gerekli mi).
   Mevcut singleton'lar (`PauseMenuController`, `DialogueUI`, `BookUI`,
   `StimulantSystem`, sahne-başı `MusicDirector`, `SfxPlayer`) korunuyor.
3. **Pooling**: `IPoolable` arayüzü + merkezi `PoolManager`. Sık
   instantiate edilen nesneler (mermi, hit-effect, düşman spawn) pooling
   devreye girdikten sonra doğrudan `Instantiate`/`Destroy` kullanmaz.
   **Bu fazın uygulama zamanı değişti** — bkz. §3, Faz 6.
4. **Adlandırma**: namespace `Bloodrush.<Domain>`; sınıf/arayüz/dosya
   İngilizce; metod/değişken Türkçe-ASCII serbest (istisnalar: Unity
   yaşam döngüsü metodları, interface imza metodları, 3. parti API
   çağrıları — bunlar İngilizce kalır). Kural **yeni kod** için geçerli,
   mevcut isimler toplu rename edilmiyor.
5. **Decoupling**: cross-domain iletişim `UnityEvent`/C# `event` veya
   interface üzerinden; doğrudan iç durum erişimi yok.
6. **Girdi**: yeni kod sadece Input System `InputAction` kullanır, legacy
   `Input` sınıfına yeni kod eklenmez.

---

## 3. Uygulama Fazları

Fazlar sırayla uygulanmalı — her biri bir öncekinin üzerine oturuyor.
**Kullanıcı kararı (2026-09-15): fazlar teker teker onaylanarak
ilerlenecek** — bir faz bitip doğrulanmadan bir sonrakine geçilmeyecek.
Test altyapısı henüz olmadığından her fazdan sonra ilgili sahneler
(`CH1`–`CH4`, `MainMenu`, `OutdoorsScene`) Editor'de manuel olarak
çalıştırılıp doğrulanmalı. Her faz ayrı bir commit/PR olarak ele
alınmalı — bir şey ters giderse tek fazı geri almak mümkün olsun.

> **Faz sırası neden değişti?** Orijinal planda pooling (eski Faz 3) veri
> katmanından (eski Faz 4) önce geliyordu ve `WaveManager`'ın spawn
> çağrılarını pooling'e bağlıyordu. Ama dalga sistemlerinin birleştirilmesi
> kararı (Faz 5) kesinleşince şu sorun ortaya çıktı: pooling önce
> `WaveManager`'a bağlanırsa, dalga sistemleri birleştirildiğinde
> (`WaveManager` muhtemelen `WaveDirector`'ın modeline göre yeniden
> yazılacağı için) bu entegrasyon işi **çöpe gidip tekrar yapılması**
> gerekirdi. Çözüm: veri katmanını öne al, Enemy AI tabanını ve dalga
> sistemi birleşmesini bitir, pooling'i **en son, olgunlaşmış tek bir
> sisteme** bağla — iş tekrarı olmaz.

### Faz 1 — Ölü Bağımlılıkları Temizle
**Risk**: Sıfır. **Bağımlılık**: Yok.

> ✅ **UYGULANDI (2026-09-15)**: `com.unity.netcode.gameobjects` ve
> `com.unity.postprocessing` `Packages/manifest.json`'dan kaldırıldı
> (`com.unity.transport` zaten manifest'te doğrudan bağımlılık değildi
> — transitive'di, netcode kalkınca `packages-lock.json`'dan Unity
> açılışta otomatik düşecek, elle dokunulmadı). `Assets/DefaultNetworkPrefabs.asset`
> silindi. `FPSHandsWeaponAnimation/` zaten Temizlik planının Faz 1'inde
> silinmişti (iki plan arasındaki bilinen çakışma, bkz. not). `CasualHit/`
> dokunulmadı, doğrulandı. Proje genelinde `using Unity.Netcode` veya
> `using UnityEngine.Rendering.PostProcessing` içeren hiçbir script
> bulunmadığı grep ile teyit edildi — derleme hatası riski yok. Unity
> Editor'de doğrulama (proje hatasız açılıyor mu) **kullanıcı tarafından
> yapılacak**.

- `Packages/manifest.json`: `com.unity.netcode.gameobjects` ve
  `com.unity.transport` paketlerini kaldır.
- `Assets/DefaultNetworkPrefabs.asset` dosyasını sil.
- `com.unity.postprocessing` paketini manifest'ten kaldır (proje genelinde
  hiçbir referans yok, doğrulanmış).
- `Assets/ThirdParty/FPSHandsWeaponAnimation/` (script içermeyen ham
  varlıklar + kullanılmayan bir Coppercube `.exe`/`.ccb` — proje genelinde
  sıfır referans, GUID ile doğrulandı): sil. **Not**: bu madde
  `TEMIZLIK_VE_YAPI_PLANI.md`'nin kendi Faz 1'inde de bağımsız olarak
  listeleniyor — iki plan burada çakışıyor. Hangisi önce uygulanırsa bu
  klasörü siler, diğeri bu adıma geldiğinde dosya zaten yok olur;
  zararsız ama tek seferlik bir iş, iki kez planlanmış.
- **`Assets/ThirdParty/CasualHit/` SİLİNMEYECEK** — ilk incelemede
  "kullanılmıyor" denmişti ama GUID taramasıyla `Enemy2.prefab` ve
  `ArmoredHazard.prefab` tarafından nested prefab olarak (`Hit_4_Red` vb.)
  kullanıldığı doğrulandı. Bu madde orijinal plandan **çıkarıldı**.
- **Kabul kriteri**: proje hatasız derleniyor, tüm sahneler açılıp
  Play Mode'a giriyor.

### Faz 2 — Girdi Mimarisini Birleştir
**Risk**: **Yüksek** (tüm temel oynanışı — hareket, ateş, parry, kanca,
diyalog, kitap etkileşimi — aynı anda etkiliyor; otomatik test yok).
**Bağımlılık**: Faz 1. **Kullanıcı kararı: alt-adımlara bölünecek.**

**Faz 2a — Çekirdek hareket ve kavga (önce, izole test edilebilir):**
- `Assets/Scripts/Player/KeyBindings.cs`'i Input System
  `InputActionAsset` + action map'lere taşı (Gameplay action map).
- Legacy `Input.GetKey/GetKeyDown/GetAxisRaw` çağrılarını değiştir:
  `PlayerMovement.cs`, `PlayerParry.cs`, `PlayerShoot.cs`,
  `GrapplingHook.cs`.
- **Kabul kriteri**: hareket/wallrun/slide, ateş etme, parry/punch, kanca
  — hepsi yeni Input System üzerinden çalışıyor; bir CH sahnesinde uçtan
  uca test edildi (düşman öldürme, hareket, kanca kullanımı).

> ✅ **UYGULANDI (2026-09-15), test bekliyor**: Plan `InputActionAsset`
> önermişti, ama proje genelinde her şeyin kod-içinde inşa edildiği
> (prefab'sız UI, procedural builder'lar) tutarlı tarzına uyması için
> **tamamen kod-içi** bir çözüm seçildi — ayrı bir `.inputactions`
> asset dosyası yerine `KeyBindings.cs`'e `Keyboard.current`/
> `Mouse.current` (yeni Input System) kullanan bir sorgu katmanı
> (`Down`/`Held`/`Up`/`MouseDelta`) eklendi.
>
> **Kritik tasarım kararı**: `KeyBindings`'in depolama katmanı
> (`KeyCode[]` cache, `Set`/`ResetDefaults`/`EnsureCache`) **bilerek
> hiç değiştirilmedi** — çünkü `SettingsPanel.Tabs.cs`'teki asıl tuş
> yakalama kodu (`CaptureRebind()`) hâlâ legacy `Input` ile tüm
> `KeyCode` değerlerini tarayıp bu cache'e yazıyor ve Faz 2a'nın
> kapsamında değil. Yeni sorgu katmanı aynı cache'i okuyor, böylece
> iki katman senkron kalıyor ve henüz dokunulmamış dosyalar
> (`NpcDialogue`, `InteractionInput`, `BookSession`, `SettingsPanel.Tabs.cs`)
> bozulmadan derlenmeye devam ediyor. Proje ayarı `activeInputHandler: 2`
> ("Both") olduğu için eski ve yeni sistem aynı anda çalışabiliyor —
> kontrol edildi.
>
> **Ayrıca fark edilip düzeltilen bir ince nokta**: `PlayerMovement.Look()`
> eskiden `Input.GetAxisRaw("Mouse X") * sensitivity` kullanıyordu.
> Yeni Input System'in `Mouse.current.delta`'sı **ham piksel** değeri
> döndürüyor, eski Input Manager'ın "Mouse X" ekseni ise
> `ProjectSettings/InputManager.asset`'te `sensitivity: 0.1` ile
> ölçekleniyordu (kontrol edildi) — bu çarpan `KeyBindings.MouseDelta`
> içine bilerek gömüldü ki fare hassasiyeti hissi bozulmasın.
> `PlayerShoot.cs`'deki `Input.GetButtonDown("Fire2")` (launcher ateşi,
> KeyBindings'e hiç bağlı değildi) de taşındı — Input Manager'daki
> varsayılanı (`Sol Alt` VEYA `Mouse1`, kontrol edildi) birebir
> `Mouse.current.rightButton`/`Keyboard.current.leftAltKey` ile
> korundu.
>
> **Test listesi (senin yapman gerekiyor)**: WASD hareket, fare bakış
> hassasiyeti (eskisiyle aynı hissetmeli), zıplama (Space), wall-jump,
> wall-run, slide (Ctrl), parry/yumruk (F), ateş etme (sol tık), silah
> değiştirme (1/2/3), reload (R), launcher modu (Q), launcher ateşi
> (sağ tık veya Sol Alt), kanca (E) — atma/çekme/bırakma/duvarda asılı
> kalma/oradan zıplama. Bir CH sahnesinde uçtan uca.

**Faz 2b — Diyalog/etkileşim/UI girdisi (sonra, daha izole risk):**
- `NpcDialogue.cs`, `InteractionInput.cs`, `BookSession.cs`'i Input
  System'e taşı (UI/Interact action map).
- `SettingsPanel.cs`'deki mevcut "herhangi bir tuşa bas" algılaması zaten
  Input System kullanıyor — rebind akışını yeni action map'lere bağla.
- Tuş yeniden atama verisini `SettingsStore`'da JSON olarak saklamaya
  devam et, ama `KeyCode[]` yerine Input System binding path'leri sakla.
- **Kabul kriteri**: NPC diyaloğu, kitap okuma, okuma köşesi etkileşimi
  yeni Input System üzerinden çalışıyor; `KeyBindings.cs` tamamen
  kaldırılmış, legacy `Input` sınıfına hiçbir yeni referans yok.

### Faz 3 — Veri Katmanı: ScriptableObject
**Risk**: Orta-Yüksek (denge değerlerinin taşınması, dikkatli test
gerekir). **Bağımlılık**: Faz 1.

- Yeni: `Assets/Scripts/Weapons/Data/WeaponData.cs` — hasar, menzil, ateş
  hızı, pellet sayısı, spread, şarjör, yedek mühimmat, reload süresi,
  recoil ölçeği, VFX/SFX referansları, **+ pool havuz büyüklüğü alanı**
  (pooling altyapısı Faz 6'da bu alanı okuyacak).
- `Assets/Scripts/Player/PlayerShoot.cs`'daki monolitik alanları üç
  `WeaponData_Revolver/Shotgun/LMG` asset'ine taşı; `PlayerFirearm.cs`
  bu veriyi tüketecek şekilde güncellenir.
- Yeni: `Assets/Scripts/Arena/Data/WaveData.cs` — **hem `WaveManager`'ın
  basit sabit-dalga modelini hem `WaveDirector`'ın heat-bazlı
  eskalasyonunu destekleyecek şekilde tasarlanmalı** (Faz 5'in birleşik
  sistemi bu veriyi tüketecek): dalga sayısı/heat parametreleri, düşman
  tipi ağırlıkları, spawn noktası kategorileri (yer-seviyesi/yükseltilmiş),
  + pool havuz büyüklüğü alanları.
- **Kabul kriteri**: silah denge değerleri kod değiştirmeden asset
  üzerinden düzenlenebiliyor; mevcut silah davranışı (fonksiyonel olarak)
  değişmiyor. `WaveData` henüz hiçbir sisteme bağlanmadı (sadece veri
  şeması hazır) — bağlama işi Faz 5'te.

### Faz 4 — Enemy AI Ortak Taban
**Risk**: Yüksek (üç bağımsız AI sınıfı birleştiriliyor). **Bağımlılık**:
Faz 3 (veri katmanı deseni referans alınır — faz-eşiği/denge değerleri
gibi sayılar burada da SO'ya çıkarılabilir).

**Kullanıcı kararı (2026-09-15): tam kapsamlı refactor** — "projede
aldığımız kararlar gibi güzelce refactor edip düzgün bir tabana oturt."
Kod incelemesinde önerilen "sadece `EnemyVision` duplikasyonunu düzelt"
şeklindeki daraltılmış/minimal seçenek **reddedildi** — tam bir ortak
taban/state-machine iskeleti hedefleniyor.

- `Assets/Scripts/Enemy/EnemyAI.cs`, `BossAI.cs`, `ExperimentBossAI.cs`
  arasında paylaşılan bir temel sınıf/ortak state machine iskeleti çıkar.
- `ExperimentBossAI`'nin kendi kopyaladığı `HasLineOfSight()` metodunu
  kaldır, paylaşılan `EnemyVision` servisini kullan.
- Karmaşık state setleri (`BossAI`: Blackout/Dash, `ExperimentBossAI`:
  PhaseBurst/Volley) için ayrı state sınıflarına bölünüp
  bölünmeyeceğine bu fazda karar ver (mimari kurallar dosyasının "Açık
  Kalan Kararlar" bölümüne bakın).
- **Kabul kriteri**: üç AI sınıfı da ortak tabanı kullanıyor, kod
  tekrarı (özellikle görüş kontrolü) kalmıyor, mevcut boss davranışları
  (blackout, phase burst, volley, dash vb.) fonksiyonel olarak korunuyor
  — her boss encounter'ı tek tek Editor'de test edilmeli.

### Faz 5 — Dalga Sistemlerini Birleştir
**Risk**: Orta-Yüksek. **Bağımlılık**: Faz 3 (WaveData), Faz 4 (Enemy AI
tabanı — spawn edilen düşmanların tipini/kategorisini artık ortak
taban üzerinden tutarlı şekilde bilmesi gerekiyor).

**Kullanıcı kararı (2026-09-15): birleştirilsin.** `Assets/Scripts/Arena/WaveManager.cs`
(basit sabit-dalga) ile `Assets/Scripts/Flow/WaveDirector.cs` (ısı-tabanlı,
gelişmiş) tek bir sisteme dönüşecek — `WaveDirector`'ın veri-odaklı,
düşman-çeşitliliği destekleyen modeli temel alınacak, `WaveData` (Faz 3)
asset'leri üzerinden yapılandırılacak. Bu, "davranışı fonksiyonel olarak
koru" ilkesinin bilinçli bir istisnası — Arena'nın "dalgaları temizle"
kazanma koşulu, birleşik sistemde de bir mod/parametre olarak (örn.
`WaveData.hasFixedWaveCount` gibi) temsil edilerek korunmalı, tamamen
kaybolmamalı.

- `WaveManager`'ın UI'ı doğrudan `UpdateUI()` ile güncellemesini event
  tabanlı hale getir (mimari kurallar §5, decoupling).
- Birleşik sistem, hem basit sabit-dalga (mevcut Arena kullanımları)
  hem heat-bazlı sürekli eskalasyon (Ch3 Server Core) modlarını tek bir
  bileşenden, `WaveData` parametreleriyle sürebilmeli.
- **Kabul kriteri**: tek bir dalga/spawn sistemi var, tüm arena
  sahnelerinde **ve** Ch3 "Server Core" bölümünde doğru şekilde
  çalışıyor; her iki kazanma/bitiş koşulu da (dalga tükenmesi / terminal
  tamamlanması) test edildi.

### Faz 6 — Object Pooling Altyapısı
**Risk**: Orta. **Bağımlılık**: Faz 3 (veri katmanındaki pool boyutu
alanları), Faz 4 (Enemy AI tabanı), Faz 5 (birleşik dalga sistemi —
**pooling artık tek, olgunlaşmış bir spawn sistemine bağlanıyor,
iki kez entegrasyon işi yapılmıyor**).

- Yeni: `Assets/Scripts/Shared/Pooling/IPoolable.cs`,
  `Assets/Scripts/Shared/Pooling/PoolManager.cs`.
- Tüketiciler: `Assets/Scripts/Weapons/LauncherProjectile.cs` (grenade/
  flashbang), `Assets/Scripts/FX/HitEffect.cs`/`BloodEffect.cs`, Faz 5'te
  birleştirilmiş dalga/spawn sisteminin düşman spawn çağrıları,
  `EnemyProjectile` (`ExperimentBossAI`'nin yaylım ateşi).
- **Kabul kriteri**: sayılan sistemlerde doğrudan `Instantiate`/`Destroy`
  çağrısı kalmıyor (tek seferlik nesneler — `WeaponPickup`,
  `AmmoPickup` — muaf).

### Faz 7 — Hafif CI
**Risk**: Düşük. **Bağımlılık**: Yok, herhangi bir zamanda eklenebilir.

- Basit bir GitHub Actions workflow: her push'ta Unity build doğrulaması
  (örn. `game-ci/unity-builder` action'ı).
- Ağır bir unit test paketi şu aşamada gerekmiyor; kritik state
  machine'ler (`PlayerMovement`, birleşmiş Enemy AI tabanı, dalga
  sistemi) olgunlaştıkça ayrıca değerlendirilir.

> ✅ **UYGULANDI (2026-09-15)**: `.github/workflows/unity-build-check.yml`
> eklendi — `main`'e her push/PR'da `game-ci/unity-builder@v4` ile
> `StandaloneWindows64` hedefine build alıp sadece derleme hatası olup
> olmadığını kontrol ediyor (gerçek bir test paketi değil), `Library/`
> klasörü önbelleğe alınıyor (hız için), LFS dosyaları checkout'ta
> dahil ediliyor.
>
> ⚠️ **Senin yapman gereken tek adım**: bu workflow GitHub repo'sunda
> `UNITY_LICENSE` (ya da `UNITY_EMAIL`+`UNITY_PASSWORD`) secret'ları
> tanımlanmadan **çalışmaz** (kimlik doğrulama hatasıyla başarısız
> olur, kod tabanına zarar vermez, sadece kırmızı X görürsün). Bunlar
> senin Unity hesap bilgilerin olduğu için ben ekleyemem —
> `game-ci/unity-builder`'ın "Activation" dokümantasyonundaki adımları
> izleyip GitHub repo → Settings → Secrets and variables → Actions'a
> eklemen gerekiyor. İstemezsen workflow dosyasını `.github/workflows/`'tan
> silmen ya da devre dışı bırakman yeterli, projeye başka hiçbir etkisi
> yok.

---

## 4. Kapsam Dışı: Kalıcılık (Save/Progression) Sistemi

**Kullanıcı kararı (2026-09-15)**: "Bu sonradan eklenecek bir mekanik.
Refactor'a ait değil." Bu, orijinal planın Faz 7'siydi — tamamen
kaldırıldı, sadece burada not olarak duruyor:

- Şu an sadece `SettingsStore` (ayarlar) ve `GameSettings.lastChapter`
  kalıcı. Bölüm içi checkpoint/ilerleme (ölüm sonrası nereden devam,
  toplanan item'lar) **refactor kapsamına dahil değil**.
- İleride netleşirse: mevcut `SettingsStore`'un JSON tabanlı yaklaşımı
  genişletilerek ayrı bir `SaveStore`/`ProgressData` katmanı eklenebilir
  — ama bu ayrı bir görev/plan olarak ele alınacak.

---

## 5. İnceleme Notları (2026-09-15 kod incelemesinden)

Bu plan yazıldıktan sonra bir kod incelemesinden geçti. Bulunan ve
düzeltilen maddeler yukarıda ilgili fazlara işlendi; özet:

- ✅ **Düzeltildi**: Faz 1'in `CasualHit` silme talimatı hatalıydı (GUID
  taramasıyla kullanıldığı doğrulandı) — kaldırıldı.
- ✅ **Karara bağlandı**: Faz 5'in (eski) "davranışı koru" ilkesiyle
  çelişkisi — kullanıcı bilinçli olarak birleştirmeyi (davranış
  değişikliğini) onayladı, plana not düşüldü.
- ✅ **Karara bağlandı**: Enemy AI ortak taban kapsamı — kullanıcı tam
  refactor'ı tercih etti (minimal/daraltılmış seçenek yerine).
- ✅ **Düzeltildi**: Girdi birleştirme riski "Orta"dan "Yüksek"e
  çıkarıldı ve iki alt-faza bölündü.
- ✅ **Düzeltildi**: Pooling/veri-katmanı/dalga-birleştirme arasındaki
  sıralama çakışması — fazlar yeniden sıralandı (bkz. §3 giriş notu).

---

## 6. Claude Code İçin Genel Notlar

- Her fazı ayrı bir commit/PR serisi olarak ele al, **her fazdan sonra
  kullanıcı onayı bekle** — bir sonraki faza otomatik geçme.
- Yeni yazılan her kod parçası §2'deki (ve `BLOODRUSH_MIMARI_KURALLARI.md`
  dosyasındaki) kurallara uymalı — özellikle namespace/sınıf İngilizce,
  metod/değişken Türkçe-ASCII kuralı.
- Otomatik test altyapısı yok (Faz 7'ye kadar) — her değişiklikten sonra
  ilgili sahneleri Editor'de açıp temel akışı (hareket, ateş, bir düşman
  öldürme, bir menü açma) manuel doğrula. **Not**: Unity Editor'de görsel
  doğrulama gerektiren adımlar kullanıcı tarafından mesai sonrası
  yapılacak — kod tarafı hazırlanıp "Editor'de şunu doğrula" şeklinde
  net bir liste bırakılmalı.
- Bir faz içinde mevcut davranışı **fonksiyonel olarak** korumak esas
  (Faz 5 — dalga birleştirme — hariç, orası bilinçli bir istisna) — bu
  bir "yeniden yaz ve iyileştir" değil, "yeniden yapılandır, davranışı
  koru" geçişidir; yeni özellik eklemek bu fazların kapsamı dışında.
- Belirsiz bir mimari karar çıkarsa, varsayım yapıp ilerlemek yerine
  seçenekleri kısaca özetleyip sor.

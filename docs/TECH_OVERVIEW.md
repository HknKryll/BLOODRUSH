# BLOODRUSH — Teknik Genel Bakış

> Bu belge, projenin kullandığı teknolojileri, sistemleri ve yapıyı kod
> seviyesinde incelemenin bir sonucudur. Amaç; genel farkındalık, gelecekteki
> mimari kararlar için hazırlık, projeye yeni katılacak biri için tanıtım ve
> olası sorun tespiti gibi birden fazla ihtiyaca hizmet etmektir. Envanterin
> yanında fark edilen boşluklar/riskler de not edilmiştir (bkz. son bölüm).
>
> Son güncelleme: 2026-09-15

---

## 1. Genel Bakış

| Bileşen | Değer |
|---|---|
| Motor | **Unity 2022.3.62f3** (LTS) |
| Render Pipeline | **HDRP 14.0.12** (High Definition Render Pipeline) |
| Multiplayer altyapısı | **Netcode for GameObjects 1.12.2** — kurulu ama **kullanılmıyor** (bkz. §4) |
| Scripting backend | Mono (IL2CPP override yok), API Compatibility: .NET Standard 2.1 |
| Input | Yeni Input System (1.14.0) paket olarak kurulu, ama oyun içi kontroller **legacy `Input` sınıfı** üzerinden özel bir `KeyBindings` katmanıyla yürütülüyor (bkz. §5) |
| UI | Unity UGUI (Canvas/EventSystem), çoğunlukla **kod ile runtime'da inşa ediliyor** (prefab tabanlı değil) |
| Persistans | Tek yapılandırılmış kayıt sistemi: `SettingsStore` (JSON dosyası). Ayrı bir "oyun kaydı/save" sistemi yok |
| Sürüm kontrolü | Git + Git LFS (`*.wav, *.mp4, *.png, *.psd, *.fbx`) |
| Test altyapısı | `com.unity.test-framework` paketi kurulu ama proje içinde hiç test yok |
| CI/CD | Yok |

Proje, tek oyunculu, bölüm bazlı (Ch1–Ch4) bir FPS/aksiyon oyunu izlenimi
veriyor: prosedürel silah animasyonu, parry/punch yakın dövüş mekaniği,
grapple hook, dalga bazlı arena dövüşleri, düşman/boss AI, diyalog/kitap
okuma sistemleri ve sahne bazlı senaryo (elevator kazası, karanlık sekans,
sunum video akışı vb.) içeriyor.

---

## 2. Proje Yapısı

`Assets/` altındaki üst düzey klasörler:

- **Scripts/** — tüm oyun kodu (aşağıda detaylandırılıyor): `Arena`, `Editor`, `Enemy`, `FX`, `Flow`, `Player`, `Settings`, `Shared` (Audio dahil), `UI`, `Weapons`
- **Scenes/** — `CH1.unity`, `CH2.unity`, `CH3.unity`, `CH4.unity`, `MainMenu.unity`, `OutdoorsScene.unity`
- **Settings/** — HDRP Render Pipeline Asset'leri, Volume Profile'lar, HDRP varsayılan kaynakları
- **Veri/** — Türkçe "Data": kitap/diyalog verisi (`Kitap1.asset`, `Kitap2.asset`)
- **Resources/** — runtime'da `Resources.Load` ile çekilen varlıklar: `GameMixer.mixer`, `MenuTheme.asset`, `UITheme.asset`, `Kitaplar/` (ek kitap verisi seti)
- **Prefabs/**, **Materials/**, **Textures/**, **Shaders/**, **Animation/**, **anim/**, **Audio/**, **Music/**, **Video/** — standart içerik klasörleri
- **"TextMesh Pro"** — TMP paket varlıkları
- **"Ch1 Palette"** — Bölüm 1 renk paleti varlıkları
- **ThirdParty/** — satın alınmış/indirilmiş varlık paketleri (bkz. §21)
- Kök seviyede: `DefaultNetworkPrefabs.asset` (Netcode'un boş varsayılan listesi), `door.prefab`

Scene serileştirmesi **force-text (YAML)** modunda
(`ProjectSettings/EditorSettings.asset: m_SerializationMode: 2`) — yani
sahneler diff'lenebilir ve grep edilebilir, binary değil.

---

## 3. Render Pipeline — HDRP

**Paket**: `com.unity.render-pipelines.high-definition@14.0.12`.

`ProjectSettings/HDRPProjectSettings.asset` sadece editör tarafı bir
yapılandırma (`HDWizardSettings`) — HDRP kaynak klasör yolunu ve HDRP
onboarding sihirbazının bayraklarını tutuyor. Asıl render pipeline
yapılandırması `Assets/Settings/` altındaki asset dosyalarında:

- **Üç kalite seviyesi** için ayrı `HDRenderPipelineAsset`: `HDRP High
  Fidelity.asset`, `HDRP Balanced.asset`, `HDRP Performant.asset` —
  `ProjectSettings/QualitySettings.asset` içinde aynı isimli kalite
  seviyelerine bağlanmış (High Fidelity varsayılan/aktif, index 0).
  Farklar tipik bir kalite kademesi: High Fidelity `colorBufferFormat: 74`
  (R11G11B10 HDR) + 4096 shadow atlas; Performant `colorBufferFormat: 48`
  + 2048 shadow atlas ile daha hafif.
- Üç profil de aynı özellik setini paylaşıyor
  (`HDRPDefaultResources/HDRenderPipelineAsset.asset`): SSR/SSGI/ray
  tracing/probe volumes/decal layers/light layers/water **kapalı**;
  SSAO, subsurface scattering, volumetrics, decals, motion vectors,
  distortion, transparent pre/post-pass **açık**; hem forward hem
  deferred destekleniyor (`supportedLitShaderMode: 2`); MSAA kapalı
  (sample count 1); render-graph yolu açık.
- `HDRenderPipelineGlobalSettings.asset` proje genelindeki varsayılan
  frame settings'i ve varsayılan Volume Profile'ı (`DefaultSettingsVolumeProfile.asset`)
  tutuyor — Exposure, Tonemapping, Bloom, AmbientOcclusion,
  ContactShadows, MotionBlur, HDShadowSettings, VisualEnvironment,
  HDRISky ve bir DiffusionProfileList override'ları içeriyor.
- `Assets/Settings/SkyandFogSettingsProfile.asset` daha hafif, sahne
  bazlı bir gökyüzü/sis profil override'ı.

**Legacy post-processing (`com.unity.postprocessing` 3.4.0) kullanılmıyor**:
`PostProcessLayer`/`PostProcessVolume`/`PostProcessProfile` için proje
genelinde hiçbir referans yok. Tüm post-processing, HDRP'nin native
Volume çerçevesi üzerinden yürütülüyor (`UnityEngine.Rendering.Volume` +
`VolumeProfile` + `Bloom`, `Vignette`, `ChromaticAberration`,
`FilmGrain`, `ColorAdjustments`, `Exposure`, `Fog` gibi bileşenler).

**Özel shader**: `Assets/Shaders/PixelatePass.shader` — HDRP Custom Pass
pipeline'ı için yazılmış, `CustomPassCommon.hlsl` kullanan, pikselleştirme
+ posterizasyon + 4×4 Bayer-matrisi dither uygulayan bir "PS1 retro"
filtresi. Runtime'da `Assets/Scripts/FX/PixelatePass.cs` (bir
`CustomPass` alt sınıfı) tarafından `CustomPassInjectionPoint.AfterPostProcess`
noktasına enjekte ediliyor.

**Runtime'da HDRP'ye dokunan kod**, `Assets/Scripts/FX/` ve
`Assets/Scripts/Flow/` altında yoğunlaşıyor:
- `SceneVolumeSetup.cs` — `Awake()`'de programatik olarak global bir
  `Volume` + `VolumeProfile` oluşturuyor (Exposure, GradientSky, Bloom,
  ColorAdjustments, FilmGrain, Fog, artı `PixelatePass` çalıştıran bir
  `CustomPassVolume`) — normal sahnelerdeki "PS1 görünümü"nün ana
  düzeneği; glitch/collapse durumuna göre parametreleri her frame lerp'liyor.
- `DarkSceneExposure.cs` — karanlık sekans/el feneri için daha yüksek
  öncelikli (200 vs 100) bir Volume override: sabit EV exposure,
  karartılmış ambient gökyüzü, sıkılaştırılmış sis, ayrı
  Bloom/Grain/PixelatePass ayarları, `HDAdditionalLightData.SetIntensity`
  ile mevcut sahne ışıklarını doğrudan ölçekliyor.
- `DamageVignette.cs` — hasar geri bildirimi için düşük öncelikli bir
  Volume (`Vignette` + `ChromaticAberration`); ayrıca `StimulantSystem`'in
  "collapse level"ını görsel olarak yansıtmak için de kullanılıyor.

---

## 4. Multiplayer / Networking — Netcode for GameObjects

**Paket**: `com.unity.netcode.gameobjects@1.12.2` (`Packages/manifest.json`
satır 12), transport bağımlılığı olarak `com.unity.transport@1.4.0`.
Relay/Lobby gibi bulut servisleri (`com.unity.services.*`) kurulu değil.

**Sonuç: Netcode kurulu ama proje içinde %100 kullanılmıyor.**

- `Assets/Scripts/` altındaki 160 `.cs` dosyasının tamamında
  `NetworkBehaviour`, `NetworkVariable`, `NetworkObject`, `ServerRpc`,
  `ClientRpc`, `IsServer`, `IsClient`, `IsOwner`, `NetworkManager`
  aramalarının **hiçbiri eşleşmedi**.
- `Assets/DefaultNetworkPrefabs.asset` (NGO'nun varsayılan
  `NetworkPrefabsList` asset'i) YAML olarak okunduğunda `List: []` —
  yani kayıtlı hiçbir network prefab'ı yok. Bu, paket kurulumunda
  otomatik oluşan varsayılan dosya; hiç dokunulmamış.
- Tüm sahneler (`CH1`–`CH4`, `MainMenu`, `OutdoorsScene`) force-text
  YAML olarak okunabildi ve hiçbirinde `NetworkManager` GameObject'i
  yok.
- Hiçbir Assembly Definition dosyası `Unity.Netcode` derleme birimine
  referans vermiyor.

**Değerlendirme**: Paket muhtemelen proje kurulumu sırasında (bir
şablondan veya ileride co-op/multiplayer eklemeyi öngörerek) eklenmiş
ama hiçbir sisteme bağlanmamış. BLOODRUSH, mevcut haliyle baştan sona
**tek oyunculu** bir mimariye sahip. (Not: NGO 1.12.2, Unity 2022.3 LTS
ile uyumlu bir kombinasyon — versiyon açısından bir sorun yok, sadece
hiç devreye alınmamış.)

---

## 5. Oyuncu Sistemleri

Konum: `Assets/Scripts/Player/` (namespace `Bloodrush.Player`).

### Hareket sistemi
`PlayerMovement.cs` (`[RequireComponent(typeof(CharacterController))]`)
merkez sınıf; `Awake()`'de iki plain C# (MonoBehaviour olmayan) yardımcı
sınıfı kompoze ediyor: `PlayerWallRun` ve `PlayerSlide`. Biçimsel bir
enum-tabanlı state machine yok — durum, bool/property'ler üzerinden
(`wallRun.Running`, `slide.IsSliding`, `FlipEnabled`) ve birbirini
dışlayan erken-dönüşlerle yönetiliyor. `Update()` → `Look()` (fare
bakışı + slide/wallrun/flip'e göre roll eğimi) ve `Move()`'u çağırıyor;
`LateUpdate()` kamera tabanını `CameraShake`'e aktarıyor (execution-order
çakışmasını önlemek için). Hareket, `CharacterController.Move()` ile
doğrudan sürülüyor — `FixedUpdate` yok, yani kinematik, Rigidbody fiziği
değil.

- **Yerçekimi ters çevirme (flip) odaları**: `FlipEnabled` açıkken
  ayrı bir `MoveFlip()` yolu çalışıyor; zıplama, zıplamak yerine
  yerçekimi yönünü tersine çeviriyor.
- **Wall-run**: `PlayerWallRun.CheckWall()` dört yönde raycast atıyor;
  `TryStart()` havadayken ve ileri hareket ederken yan raycast'lerle
  (`wallRunMask` katmanına karşı) başlıyor. Çalışırken her frame
  `cc.Move()` çağrılıyor (ileri hız + duvara hafif itiş + `wallRunGravity`).
- **Slide**: `PlayerSlide.Tick()` hızı sürtünmeyle azaltıyor,
  `CharacterController` yüksekliğini/merkezini çömelme için küçültüyor,
  zıplama/tuş bırakma/çok yavaşlama/timeout ile bitiyor ve çıkış
  momentumunu (`launchOut`) `PlayerMovement`'a geri veriyor.
- Dış sistemler (yerçekimi bölgeleri, asansörler) `SpeedMultiplier`,
  `JumpMultiplier`, `GravityScale`, `DisableGravity`, `SlideEnabled`,
  `JumpEnabled` gibi public property'lerle hareketi modüle ediyor.

### GrapplingHook.cs
Yay/halat fizik simülasyonu **yok** — saf raycast/kinematik bir kanca.
`LineRenderer` sadece görsel bir çizgi. Durum akışı: `firing →
isHooked (Pull) → hanging (opsiyonel) → idle` (bool'larla takip
ediliyor). `TryGrapple()` bir `Physics.Raycast` (ince kenarlar için
`SphereCast` fallback) atıyor; vurunca hemen çekmiyor, `fireDuration`
süresince görsel olarak kancayı hedefe taşıyıp sonra `Attach()`
tetikliyor. `Pull()` üç özel durumu ele alıyor: `AmmoPickup` çekme,
küçük bir `EnemyAI`'ı oyuncuya doğru çekme (`StartBeingPulled()`), ve
düz zemin/duvar kancası (yerçekimini `DisableGravity` ile kapatıyor).
İlerleme yeterince hızlı değilse (~0.4s eşik) "sıkışma" tespit edilip
`EnterHang()`'e geçiliyor — oyuncu duvarda asılı kalıyor, wall-jump ile
çıkabiliyor.

### PlayerParry.cs
`KeyBindings.ParryPunch` tuşuna basılınca `Physics.OverlapSphere` ile
`IParryable` (bkz. §6) uygulayan ve `IsParryable == true` olan en yakın
hedefi arıyor; bulursa `Parry(parryStun)` çağırıyor + kamera sarsıntısı
+ SFX. Parry edilecek hedef yoksa `TryPunch()`'a düşüyor: sadece küçük
(`!enemy.IsLarge`, yani boss'lar hariç) `EnemyAI`'lara karşı doğrudan
hasar + knockback uyguluyor. Belirgin bir "parry penceresi" tamponu yok —
tek karelik bir tuş-basımı kontrolü.

### StimulantSystem.cs
Singleton bir "uyarıcı/ilaç" buff sistemi. `UseStimulant()` oyuncuyu
iyileştiriyor, ardından `SpeedBuff`/`DamageBuff` coroutine'leriyle
`movement.SpeedMultiplier`/`shoot.DamageMultiplier`'ı geçici olarak
artırıyor. Her kullanım `collapseLevel`'i düşürüyor (bağımlılık/aşırı
doz mekaniği — 5 kullanım = 0); 0'a ulaşınca oyuncu ölüyor
(`TakeDamage(9999f)`). `CancelBuffs()`, `PlayerLoadout` tarafından
bileşen devre dışı bırakıldığında buff'ların kalıcı takılı kalmasını
önlemek için dışarıdan çağrılıyor.

### Silah/loadout
`PlayerFirearm` (plain C# sınıf, `EnemyRangedAttack` kompozisyon
desenine benzer) tek bir hitscan silahın mühimmat/ateş hızı/reload
durumunu kapsüllüyor. `PlayerShoot` (MonoBehaviour) üç `PlayerFirearm`
örneğini (Revolver/Shotgun/LMG) + grenade/flash fırlatıcı mantığını
tutuyor; `Update()`'de silah değiştirme, ateş etme, fırlatıcı ve reload
girdilerini okuyor. `PlayerLoadout`, static ve sahne-ötesi kalıcı bir
"ekipman kapı bekçisi" (`[Flags] Gear` enum) — anlatı/akış scriptleri
(`GameFlow`, asansör kazası sekansı) oyuncunun ekipmanını
kısıtlamak/geri vermek için bunu kullanıyor; `Apply()` neredeyse tüm
oyuncu alt sistemlerine (silah, hareket, kanca, parry, stimulant)
dokunuyor — yani sistemler arası **merkezi bağlayıcı** bu sınıf.

### Input System entegrasyonu
Yeni Input System paketi (1.14.0) kurulu olmasına rağmen, **hiçbir
oyun içi oyuncu scripti `InputAction`/action map/`PlayerInput`
kullanmıyor**. Tüm hareket/kavga/yetenek girdisi **legacy Input Manager**
(`UnityEngine.Input.GetKey/GetKeyDown/GetAxisRaw`) üzerinden, özel bir
static `KeyBindings.cs` sarmalayıcısıyla yürütülüyor. Bu sınıf yeniden
atanabilir tuşları (`KeyCode[]`) `SettingsStore`'da JSON olarak
saklıyor. Yeni Input System'in tek gerçek kullanım yeri
`SettingsPanel.cs` — tuş yeniden atama akışında "herhangi bir tuşa bas"
algılaması için `Keyboard.current`/`Gamepad.current` kullanılıyor,
oyun içi girdi dağıtımı için değil.

---

## 6. Düşman / AI Sistemleri

Konum: `Assets/Scripts/Enemy/` (namespace `Bloodrush.Enemy`). AI Navigation
paketi (`com.unity.ai.navigation@1.1.7`) kullanılıyor; hem `EnemyAI` hem
`BossAI`/`ExperimentBossAI` `NavMeshAgent` gerektiriyor.

**Mimari**: Ortak bir temel sınıf **yok** — `EnemyAI`, `BossAI`,
`ExperimentBossAI` birbirinden bağımsız `MonoBehaviour`'lar; sadece
`IParryable` arayüzü ve aynı küçük "davranış" yardımcı sınıf setini
(`EnemyMeleeAttack`, `EnemyRangedAttack`, `EnemyPatrolBehavior`,
`EnemyLeapBehavior`, `EnemyKnockbackHandler`, `BossMeleeAttack`,
`BossShotgunAttack`, `BossBlackoutSequence`) kompoze etmeleriyle
birleşiyorlar. `EnemyVision`, neredeyse her şey tarafından paylaşılan
tek static line-of-sight servisi.

### EnemyAI.cs (sıradan düşman)
`enum State { Patrol, Chase, Attack, Telegraphing, Stunned, RangedFire }`
ile durum makinesi `Update()`'den yürütülüyor. `enum Behavior { Melee,
Ranged }` alanı yakın dövüş/menzilli davranışı seçiyor. Patrol,
`EnemyPatrolBehavior.Tick()` ile waypoint yürüyüşü + görüş taraması
yapıyor; oyuncu görülünce `Chase`'e geçiyor. Melee düşmanlar
`ChaseTarget()` (NavMesh üzerinde örneklenmiş, 0.2s'de bir yeniden
hesaplanan bir nokta) ile takip ediyor ve hem `EnemyMeleeAttack.InRange()`
hem `EnemyVision.ClearForMelee()` doğrulanınca `Attack`'e geçiyor.
Menzilli düşmanlar `DoRangedChase`/`EnemyRangedAttack.GetChaseMove()`
ile ilerleme/bekleme/geri çekilme kararı veriyor. Ayrıca kompoze edilen:
`EnemyKnockbackHandler` (yumruk/kanca geri tepmesi), opsiyonel
`EnemyLeapBehavior` (`isJumper` tipi), NavMesh'e kendi kendine dönüş
(`TryRecoverNavMesh`), fizik tabanlı düşüş (`PhysicsFall`, kanca
çekiminden sonra), stun/parry (`IsParryable => state==Telegraphing`).

### BossAI.cs ve ExperimentBossAI.cs
**İki bağımsız boss implementasyonu** (biri diğerinden türemiyor —
`STORY_DESIGN.md`'ye göre `BossAI` Bölüm 2'nin insan "denetçi"si,
`ExperimentBossAI` bölüm-sonu "yaratık" evrimi).

- **`BossAI`**: `Chase, ShotgunAim, MeleeTelegraph, Dash, Blackout,
  Stunned` durumları. Üç can-yüzdesi fazı (100–66%, 66–33%, <33%)
  `RunBlackout()`'u tetikliyor (ışıklar sönüyor, oyuncunun arkasına
  ışınlanıyor — `BossBlackoutSequence`). Menzilli saldırı hitscan
  shotgun konisi (`BossShotgunAttack`), yakın dövüş parry edilebilir
  telegraph'lı vuruş (`BossMeleeAttack`). Uzun süre mesafeli kalınırsa
  anti-kiting `Dash` durumu tetikleniyor. Ölünce silah drop'u yapıyor.
- **`ExperimentBossAI`**: `Chase, Volley, Leaping, MeleeTelegraph,
  Stunned, PhaseBurst` durumları. Aynı faz-eşiği tetikleyicisiyle
  `PhaseBurstRoutine()` çalıştırıyor — AoE patlama + halka şeklinde
  `HazardZone` asit havuzları (`SpawnHazard()`). `BossMeleeAttack`'i
  pençe saldırısı için, `EnemyLeapBehavior`'ı slam-leap için yeniden
  kullanıyor (`OnLeapLanded()` alan hasarı + faz 2+'de `HazardZone`
  bırakıyor). Menzilli davranış hitscan değil, projectile yaylım ateşi
  (`VolleyRoutine`, `EnemyProjectile` örnekleri). Kendi
  `HasLineOfSight()` metodunu kopyalıyor (`EnemyVision`'ı yeniden
  kullanmıyor). `UnityEvent onDefeated` ile bitiyor (cutscene kancası).

Her ikisi de `IsLarge => true` (bu yüzden `PlayerParry.TryPunch` onları
asla yumruklamıyor) ve parry edilebilirliği sadece telegraph durumuna
kısıtlıyor.

### Diğer önemli parçalar
- **`EnemyPatrolBehavior.cs`** — plain C# sınıf; sıralı waypoint dizisi
  üzerinde round-robin yürüyüş (rastgele/prosedürel hareket yok),
  `sightInterval=0.15s`'de bir throttled görüş kontrolü.
- **`EnemyVision.cs`** — static utility; `PlayerFeet()` CharacterController
  pivot farkını düzeltiyor; `Clear()` tek raycast (shared 32-slot
  buffer); `ClearToPlayer()` üç gövde yüksekliğini kontrol ediyor
  (kısmi örtünme gerçekçiliği için, menzilli saldırılarda kullanılıyor);
  `ClearForMelee()` sık-mesafe temas için raycast'i tamamen atlıyor
  (düşman yığılmalarında yanlış "görüş yok" sonucunu önlemek için).
- **`EnemyLeapBehavior.cs`** — plain C# sınıf; `NavMeshAgent`'ı
  devre dışı bırakıp Rigidbody'yi fırlatıyor, iniş sonrası yeniden
  NavMesh'e snap'liyor ve `onLanded` callback'i tetikliyor.
- **`HazardZone.cs`** — dinamik olarak oluşturulan (prefab'a
  eklenmemiş), zamanlı alan-hasarı tetikleyici hacim. `Init(radius,
  duration, tickDamage, tickInterval, damageMask, ignore)` ile
  yapılandırılıyor; `Health` bileşeni olan her şeye (oyuncu dahil)
  hasar veriyor.
- **`IParryable.cs`** — minimal iki üyeli arayüz (`IsParryable`,
  `Parry(stunDuration)`). `EnemyAI`, `BossAI`, `ExperimentBossAI`
  tarafından uygulanıyor; tek tüketicisi `PlayerParry.cs` — somut
  düşman tiplerinden tamamen ayrıştırılmış.

Prefab kompozisyonu: `NavMeshAgent` + `Health` + `Rigidbody` + AI
`MonoBehaviour` (kendi davranış yardımcılarını `Awake()`'de `new`'liyor)
+ opsiyonel `Animator` + telegraph/stun partikülleri + drop/pickup
referansları.

---

## 7. Silah Sistemleri

Konum: `Assets/Scripts/Weapons/` ve `Assets/Scripts/Player/`.

### LauncherProjectile.cs
Grenade ve flashbang için **tek sınıf**, `isFlash` bool'u ile ayrışıyor
(ayrı alt sınıflar yok). Hareket saf Rigidbody fiziği — `Launch(velocity)`
spawn'da bir kere hız veriyor, sürekli kuvvet/homing yok. Patlama
zamanlayıcı tabanlı (`Invoke(nameof(Detonate), fuseTime)`) veya dışarıdan
tetiklenebiliyor: `PlayerFirearm.FirePellet` bir hitscan atışı fırlatılmış
bir grenade'a değerse `proj.Detonate()`'i doğrudan çağırıyor (revolver
ile havadaki grenade'ı "vurup düşürme"). `Detonate()`
`Physics.OverlapSphere` ile ya `ApplyBlast` (mesafeye göre lineer
interpolasyonlu hasar) ya `ApplyFlash` (`EnemyAI.Stun()`) uyguluyor.
Object pooling yok — `Instantiate`/`Destroy`.

### Prosedürel silah hareketi
`ProceduralWeaponMotion.cs` silah transform hareketinin **tek motoru** —
tamamen kod tabanlı, Animator/Mecanim değil. Her `LateUpdate()`'de: sinüs
dalgalı koşu sallanması, iki aşamalı spring recoil (snap-sonra-decay),
fare sway'i, slide-lean, kanca uzatma, duvar yakınlığı geri çekilmesi
(raycast), ve çekme/kılıfa koyma state machine'i (`Holster()`/`PlayDraw()`)
birleştiriliyor. Ayrı bir `WeaponAnimator.cs`, ateş etme/reload/parry
pozları için gerçek Animator tetikleyicilerini (`TriggerFire()`,
`TriggerParry()` vb.) sarmalıyor — yani sway/bob/recoil prosedürel,
ayrık ateş/parry pozları Mecanim üzerinden, aynı silah transformu
üzerinde katmanlanıyor.

### Silah verisi — ScriptableObject sistemi yok
Silahlarla ilgili hiçbir ScriptableObject/`CreateAssetMenu` bulunamadı.
Bunun yerine `PlayerShoot.cs` tek başına üç silahın (Revolver/Shotgun/LMG)
tüm stat bloklarını (hasar, menzil, ateş hızı, pellet sayısı, spread,
şarjör, yedek mühimmat, reload süresi, recoil ölçeği, VFX/SFX referansları)
serialized alanlar olarak tutan monolitik bir `MonoBehaviour`. `Start()`'ta
üç `PlayerFirearm` örneği (plain C# sınıf, `EnemyRangedAttack` desenine
benzer) oluşturuyor.

### FPSHandsWeaponAnimation — kullanılmayan ham varlık
`Assets/ThirdParty/FPSHandsWeaponAnimation/` içinde **hiç .cs script**
yok — sadece ham kaynak varlıklar (`.fbx`, glock `.obj`/`.mtl` modeli,
CopperCube demo projesi). Hiçbir custom kodla sarmalanmamış/bağlanmamış
— muhtemelen sadece referans/import materyali.

### Ateş etme, reload, mühimmat
`PlayerFirearm.TryFire()` cooldown/reload durumunu kontrol edip
mühimmatı düşürüyor, muzzle-flash + SFX çalıyor, kamera sarsıntısı
tetikliyor, `pelletCount` kadar `FirePellet()` döngüsü çalıştırıyor
(shotgun spread). `FirePellet` raycast atıp ya `LauncherProjectile`'ı
(değince patlatıyor) ya `Health`'i (`GetComponentInParent`) çözüyor,
mesafe-lerp'li hasar uyguluyor, hit-marker + procedural spark/kan efekti
(`HitEffect`, `BloodEffect` — kendi özel, prosedürel efektleri)
tetikliyor. Silah/mühimmat kilidi açma `PlayerLoadout` ile dışarıdan
kontrol ediliyor, pickup'lar `WeaponPickup.cs`/`AmmoPickup.cs` ile
alınıyor.

### Hasar akışı ve cross-reference'lar
Hasar her zaman `Assets/Scripts/Shared/Health.cs` üzerinden akıyor
(`TakeDamage`/`Heal`/`onDeath` UnityEvent). `IParryable` sadece
`PlayerParry.cs` (yakın dövüş) tarafından tüketiliyor — hiçbir silah/
projectile scripti tarafından referans edilmiyor;
`LauncherProjectile.ApplyFlash` bunun yerine `EnemyAI.Stun()`'ı
doğrudan çağırıyor.

### CasualHit — içe aktarılmış ama silahlara bağlanmamış
`Assets/ThirdParty/CasualHit/Scripts/Casual_Hit.cs` sadece generic bir
kamera sarsıntısı utility'si içeriyor; asıl hit-effect prefab'ları
(`Hit_1`–`Hit_4`) hiçbir script veya prefab tarafından referans
edilmiyor. BLOODRUSH'ın kendi hit VFX'i (`HitEffect`, `BloodEffect`)
tamamen özel, prosedürel olarak inşa edilmiş efektler.

---

## 8. Ses / Müzik Sistemi

Konum: `Assets/Scripts/Shared/Audio/` (namespace `Bloodrush.Shared.Audio`),
altı dosya: `MusicDirector.cs`, `MusicFadeZone.cs`, `MusicTrigger.cs`,
`RoomMusic.cs`, `SfxPlayer.cs`, `AudioRouting.cs`. Gerçek bir Unity mixer
asset'i mevcut: `Assets/Resources/GameMixer.mixer`.

- **`RoomMusic`** — temel çalma birimi: bir `AudioClip` + bir
  `AudioSource`. `FadeIn()`/`FadeOut()` ve süre parametreli overload'ları
  var; `PlayScheduledAt(dspTime, ...)` örnek-hassasiyetli zamanlanmış
  başlangıç için. Fade'ler tek bir coroutine slotu üzerinden yürüyor —
  tekrar tetiklenme fade'leri yığmıyor, mevcut ses seviyesinden devam
  ediyor. Mixer grubu atanmamışsa `AudioRouting.Music`'ten otomatik
  çözülüyor.
- **`MusicDirector`** — orkestratör, **sahne başına singleton**
  (`DontDestroyOnLoad` **değil** — sahne kapanınca ölüyor, müzik
  bölümler arası sızmıyor). `Play(id, crossfade)` zaten çalan parçayı
  tekrar başlatmıyor (no-op); `alignToBar` açıkken bir sonraki bar
  sınırına kadar bekleyip `PlayScheduledAt` ile senkronize, faz-kilitli
  bir crossfade yapıyor. Boss'un `Health.onDeath`'ini dinleyip müziği
  kilitliyor (`lockAfterBossDeath`); oyuncunun ölümünde checkpoint
  respawn sahne yeniden yüklemediği için fallback parçaya dönüyor.
- **`MusicTrigger`** — `BoxCollider` tetikleyicisi; oyuncu girince
  (`GetComponentInParent<PlayerMovement>()`, tag değil)
  `MusicDirector.Instance.Play(...)`'ı çağırıyor. `onlyOnce` bayrağı
  var (boss odalarında kapalı, tekrar girişte yeniden tetiklensin diye).
- **`MusicFadeZone`** — ayrı, daha basit bir tetikleyici hacim; belirli
  bir `RoomMusic`'e doğrudan referans veriyor (director'a hiç
  uğramadan) — örn. asansör boşluğunda müziği yerel olarak susturmak
  için kullanılıyor.
- **`AudioRouting`** — static, tembel-çözümlenen bir kaynak bulucu:
  `Resources.Load<AudioMixer>("GameMixer")`'dan `Music`/`Sfx`/`Ui`
  gruplarını buluyor. Mixer yoksa her şey `null`'a düşüp doğrudan
  dinleyiciye sesle devam ediyor (graceful degradation). Snapshot
  geçişleri **kullanılmıyor** (mixer'da tek "Snapshot" var ama kod
  `TransitionToSnapshots` çağırmıyor).
- **`SfxPlayer`** — singleton değil, static factory metodlu (`Create`,
  `CreateOrGet`, `PlayAtPoint`, `PlayDetached`) bir instantiable
  sarmalayıcı. `AudioSource.PlayClipAtPoint` **bilinçli olarak
  kullanılmıyor** (mixer routing'i bypass ettiği için) — bunun yerine
  kendi kendini yok eden throwaway GameObject'ler kullanılıyor. Proje
  genelinde ~28 çağrı noktasıyla (Player, Flow, Enemy, Arena) projenin
  tek SFX giriş noktası; müzik sisteminden tamamen ayrık.

**Veri akışı özeti**: oyuncu `MusicTrigger` collider'ına girer →
`MusicDirector.Instance.Play(id, crossfade)` → director `Track`'i
çözer, bar-hizalı zamanlama hesaplar → gelen parçada
`RoomMusic.PlayScheduledAt`, her ikisinde `FadeIn/FadeOut` → crossfade
tamamlanır. `MusicFadeZone` bundan bağımsız olarak belirli bir
`RoomMusic`'i doğrudan fade'liyor.

---

## 9. UI / Menü Sistemi

Konum: `Assets/Scripts/UI/Menu/`. Tamamen **UGUI** üzerine kurulu
(`Canvas`, `CanvasScaler`, `GraphicRaycaster`, `EventSystem`,
`UnityEngine.UI.*`, TextMeshPro) — **kod ile runtime'da inşa ediliyor,
prefab/sahne asset'i değil**. UI Toolkit (`UIDocument`/`VisualElement`)
hiç kullanılmıyor.

- **`MainMenuController`** — ana menü UGUI hiyerarşisini `Build()`'de
  kuruyor (başlık, OYNA/DEVAM ET/AYARLAR/KREDİLER/ÇIKIŞ); eski
  `MainMenuManager` bileşenini sahnede bulursa devre dışı bırakıyor.
- **`PauseMenuController`** — kendi kendini `DontDestroyOnLoad` ile
  bootstrap eden singleton (`[RuntimeInitializeOnLoadMethod]`);
  `Update()`'de Escape/gamepad Start'ı polling ile izliyor; `MainMenu`
  sahnesinde ve `SettingsPanel.IsOpen` iken devre dışı.
- **`SettingsPanel`** — iki partial class dosyasına bölünmüş
  (`SettingsPanel.cs`: kabuk; `SettingsPanel.Tabs.cs`: sekme içerikleri).
  Hem ana menü hem duraklatma menüsü **aynı tek panel örneğini**
  paylaşıyor (`SettingsPanel.Open(onClosed)` static metodu — kod
  yorumunda bu, önceki "her ikisinin kendi kopyası vardı" tasarımının
  yerine geçtiği açıkça belirtiliyor). Sekme değişimi
  (`SettingsSection`: Display/Audio/Controls/Gameplay) `content`'in
  çocuklarını yok edip yeniden inşa ediyor.
- **`CreditsPanel`**, **`ConfirmDialog`** — `ConfirmDialog` hem yıkıcı
  onaylar hem riskli görüntü değişiklikleri için 10 saniyelik
  otomatik-geri-alma sayacı (`AskWithRevert`) olarak yeniden kullanılıyor.
- **`MenuUI.cs`** — static, prosedürel UGUI inşa araç seti
  (`CreateCanvas`, layout yardımcıları, `Text`, `Button`,
  `SliderControl`). `SliderControl`, gamepad odak nedeniyle
  `TMP_Dropdown` yerine bilinçli olarak "< Değer >" stepper olarak da
  kullanılabiliyor.
- **`MenuTheme.cs`** — `ScriptableObject` (`Resources/MenuTheme.asset`);
  tüm renk/font/boşluk/animasyon süresi buradan geliyor, kodda
  hardcoded görsel sabit yok.
- **`MenuNavigation.cs`** — yeni Input System'in `InputSystemUIInputModule`'ünü
  kullanarak `EventSystem`'i programatik kuruyor ve `Selectable`'lar
  arası navigasyonu bağlıyor (bu, projede yeni Input System'in
  kullanıldığı nadir yerlerden biri).
- **`PauseBackdrop.cs`** — duraklatma arka planı için bir
  `RenderTexture`'ı downsample ederek blur benzeri bir görünüm
  yakalıyor (HDRP özel blur pass'i yerine).

---

## 10. Ayarlar Sistemi

Konum: `Assets/Scripts/Settings/`.

- **`GameSettings.cs`** — plain `[Serializable]` C# sınıf (ScriptableObject
  değil), tüm alanlar açıkça public ("`JsonUtility` serileştirebilsin
  diye" — kod yorumu). Display (çözünürlük, yenileme hızı, ekran modu,
  monitör, vsync, fps limiti, kalite seviyesi, FOV, parlaklık), Audio
  (master/music/sfx/ui ses seviyeleri), Controls (hassasiyet, ADS
  hassasiyeti, invertY, titreşim, tuş atamaları), Gameplay (nişangah
  tipi/rengi, kamera sarsıntısı, kan efektleri, motion blur, altyazı,
  dil) alanlarını kapsıyor, artı "Devam Et" için `lastChapter`.
- **`SettingsStore.cs`** — persistans katmanı, **bilinçli olarak
  PlayerPrefs değil**: `GameSettings`'i `JsonUtility` ile
  `Application.persistentDataPath/bloodrush_settings.json` dosyasına
  JSON olarak seri hale getiriyor. Bozuk/eksik dosyalarda
  `try/catch` ile varsayılana dönüyor. `NotifyChanged(save=true)`,
  her UI değişikliğinde tetiklenen "anında uygula, Uygula butonu yok"
  kancası. **Bu, projedeki tek yapılandırılmış kayıt sistemi** — başka
  bir yerde bulunan tek persistans, `PlayerPrefs` "Sensitivity" anahtarı
  (geriye dönük uyumluluk için `PlayerMovement.cs`/`KeyBindings.cs`'te
  hâlâ kullanılan legacy bir kalıntı).
- **`SettingsApplier.cs`** — veriden canlı motor/oyun durumuna köprü,
  `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` ile boot oluyor.
  `ApplyDisplay()` → `QualitySettings`/`Screen`/FOV; `ApplyAudio()` →
  `AudioRouting.Mixer` üzerinden `SetFloat` (lineer 0–1'den dB'ye
  dönüşümle); `ApplyControls()` → canlı `PlayerMovement` + legacy
  PlayerPrefs; `ApplyGameplay()` → `CameraShake.ShakeScale` +
  `CrosshairHUD`. **HDRP Volume/post-processing'e dokunmuyor** —
  parlaklık, motion blur, kan efekti, altyazı, dil, ADS hassasiyeti,
  titreşim ayarları saklanıyor ama arayüzde "HENÜZ ETKİN DEĞİL" olarak
  işaretli (`MarkPending()`).

**Tam veri akışı**: sekme UI'ı `SettingsStore.Current`'ı okuyor →
her kontrolün `onChange`'i alanı doğrudan mutasyona uğratıyor → ilgili
`SettingsApplier.Apply*()` çağrılıyor → `SettingsStore.NotifyChanged()`
(diske kaydediyor + `Changed` event'ini tetikliyor). Ayrı bir
"commit/Uygula" adımı yok — çözünürlük/ekran modu hariç (bunlar
`ConfirmDialog.AskWithRevert` ile 10 saniyelik güvenlik ağından geçiyor).

---

## 11. Diyalog / Kitap Sistemi

Konum: `Assets/Scripts/UI/DialogueUI.cs`, `Assets/Scripts/Flow/NpcDialogue.cs`,
`Assets/Scripts/UI/BookUI.cs`, `Assets/Scripts/Flow/BookData.cs`,
`BookSession.cs`, `InteractableBook.cs`, `SeatInteractable.cs`,
`InteractionInput.cs`. Veri: `Assets/Veri/Kitap1.asset`, `Kitap2.asset`
ve daha geniş bir ikinci set `Assets/Resources/Kitaplar/` altında
(`Kitap_OrionTarihi.asset`, `Kitap_DevletIsbirligi.asset` vb.).

- **`DialogueUI.cs`** — kendi kendini oluşturan singleton (`Ensure()`
  runtime'da kendi Canvas'ını yaratıyor, sahnede hiçbir şey yerleşik
  değil), static metodlarla sürülüyor (`ShowPrompt`/`HidePrompt`,
  `ShowLine`/`HideLine`). **Daktilo/harf-harf efekt yok, dallanan
  seçim UI'ı yok** — satırlar bütün gösteriliyor, ilerleme tamamen
  `NpcDialogue`'un manuel string dizisi ilerletmesiyle sağlanıyor.
  Birden fazla etkileşimli obje (NPC, koltuk, kitap) aynı karede
  prompt gösterebileceği için `promptOwner` alanıyla çakışma önleniyor.
- **`NpcDialogue.cs`** — diyaloğu doğrudan bileşen üzerinde inline
  `string[] lines` dizisi olarak yazıyor (ScriptableObject/dış dosya
  yok — kod yorumu "Metinleri SEN yazarsın" diyor). Tetikleme
  mesafe + bakış açısı (dot product) ile her `Update()`'de polling
  ediliyor; **legacy `Input` sınıfı** (`Input.GetKeyDown`, varsayılan
  `E`). Tekrar-konuşma modeli var: ilk tam oynatmadan sonra sadece son
  satır tekrarlanıyor; `repeatThreshold` sonrasında özel bir
  "sinirlenme" satırı bir kez tetikleniyor.
- **`BookData.cs`** — `ScriptableObject`
  (`[CreateAssetMenu(menuName = "Bloodrush/Kitap Verisi")]`), `title`,
  `subtitle`, `string[] pages` tutuyor.
- **`BookUI.cs`** — aynı kendi-kendini-inşa-eden singleton deseni,
  ama daha zengin: TextMeshPro, `Resources/UITheme.asset`'ten tema
  okuyor, iki görünüm sunuyor — okuma paneli (`ShowBook`/`HideBook`,
  sayfa noktası göstergesiyle) ve seçim menüsü (`ShowMenu`/`HideMenu`,
  9'a kadar numaralı satır).
- **`BookSession.cs`** — "bir kitap açık" durumunun tek runtime
  denetleyicisi: `PlayerMovement`'i kilitliyor, imleci serbest
  bırakıyor, mevcut sayfayı takip ediyor, `InteractionInput.TryConsume()`
  ile korunan tuşla sayfa ilerletiyor.
- **`InteractableBook.cs`** — tek bir okunabilir obje (masadaki tek
  kitap gibi), `BookSession.Open(data, ...)` çağırıyor doğrudan.
- **`SeatInteractable.cs`** — "Okuma köşesi" parçası: oturma kamerayı
  yumuşak geçişle `seatPoint`'e taşıyor (`PlayerMovement` geçiş
  boyunca devre dışı), ardından `BookData[]` dizisinden
  `BookUI.ShowMenu` listesi açılıyor; kitap kapatma seçim menüsüne
  geri dönüyor.
- **`InteractionInput.cs`** — bir "hakem" static sınıf (`TryConsume()`)
  — aynı E tuşunun aynı karede birden fazla etkileşimli tarafından
  tüketilmesini (örn. kitabı kapatırken aynı anda koltuğu yeniden
  tetiklemeyi) önlüyor.

**Diğer ilgili bulgular**: `Assets/Scripts/Player/Flashlight.cs` — "el
feneri" (F tuşu, kameraya bağlı HDRP spot ışığı, silahsızken bile
kullanılabiliyor). `Assets/Scripts/Flow/PresentationScreen.cs` —
duvardaki sunum ekranı, `string[] slides` dizisini fade geçişleriyle
döngüleyen bir world-space canvas (slayt-metni simülasyonu, video değil).

> **Düzeltme (2026-09-15)**: Bu bölüm ilk yazıldığında "gerçek video
> oynatımı bulunamadı" notu düşülmüştü — bu artık **güncel değil**.
> Proje sonradan `Assets/Scripts/Flow/PresentationSequence.cs` adında,
> `PresentationScreen.cs`'den **tamamen ayrı**, gerçek bir
> `VideoPlayer`/`VideoClip` (`Assets/Video/VERITAS_Sunum.mp4`) oynatan
> ikinci bir sunum sistemi kazanmış — detaylar için §16 "Anlatı / Akış
> Altyapısı" bölümüne bakın. İki sistem birbirinden habersiz, paralel
> olarak bir arada duruyor; hangisinin hangi sahnede kullanıldığı ayrı
> ele alınmalı.

---

## 12. Arena / Dalga (Wave) Sistemi

Konum: `Assets/Scripts/Arena/WaveManager.cs` (namespace `Bloodrush.Arena`)
— klasördeki tek dosya.

Plain `MonoBehaviour`; dalgalar tamamen **hardcoded/Inspector alanları**
üzerinden (`maxWaves=10`, `baseEnemyCount=3`, `enemyCountPerWave=+1`,
`timeBetweenWaves=5s`, düz `spawnPoints[]`/`enemyPrefabs[]` dizileri) —
ScriptableObject tabanlı bir veri sistemi yok.

- **Akış**: `Start()` → `playerHealth.onDeath`'i bağlıyor →
  `StartNextWave()`. Zorluk **lineer** ölçekleniyor
  (`baseEnemyCount + (currentWave-1) * enemyCountPerWave`) — sadece
  miktar artıyor, kompozisyon çeşitliliği yok.
- **Spawn**: `SpawnEnemy()` `spawnPoints[]`'ten **düzgün rastgele**
  bir nokta ve `enemyPrefabs[]`'ten düzgün rastgele bir prefab seçip
  doğrudan `Instantiate` ediyor — pooling/factory soyutlaması yok.
- **Tamamlanma tespiti**: canlı bir sayaç (`aliveEnemies`), her
  `Health.onDeath` ile azalıyor; 0 olunca `playerShoot.RefillWave()`
  çağrılıyor, sonra son dalgaysa `EndGame(true)`, değilse geri sayımlı
  bir sonraki dalga.
- **EnemyAI/BossAI ile ilişki**: `WaveManager`'ın `EnemyAI`/`BossAI`
  hakkında hiçbir farkındalığı yok — sadece `enemyPrefabs[]`'e ne
  konmuşsa onu instantiate edip generic `Health` bileşenini dinliyor.
- **Events**: dışarıya çok az şey açıyor — sadece static
  `IsGameOver` bayrağı ve `ReturnToMenu()`. UI güncellemeleri
  (`waveText`, `enemyCountText` vb.) dışarıdan abone olunabilir
  event'ler yerine doğrudan içeriden `UpdateUI()` ile push ediliyor.
  Ses de yerel olarak (`SfxPlayer`) çalınıyor.
- **`MusicTrigger` ile bağlantı yok**: `MusicTrigger.cs`'nin kendi
  kod yorumu, bilinçli olarak `Arena.cs`'e (üç arenanın paylaştığı
  ayrı bir sınıf, `Assets/Scripts/Flow/Arena.cs`) bağlanmadığını
  belirtiyor — çünkü o sınıfın dışa event yayınlama mekanizması yok
  ve eklemek üç arenayı paylaşan bir scripte dokunmak anlamına
  gelirdi. Bu yüzden `MusicTrigger` tamamen ayrık, kendi kendine
  yeten bir tetikleyici hacim olarak tasarlanmış.

**Kardeş sistem**: `Assets/Scripts/Flow/WaveDirector.cs` (namespace
`Bloodrush.Flow`, singleton) — "Ch3 Server Core" bölümünde kullanılan,
çok daha gelişmiş, ısı-tabanlı (heat-based) eskalasyon yapan bir
spawner: sürekli zamanlayıcı tabanlı, düşman tipi çeşitliliği (küçük/
büyük/zırhlı/hızlı/zıplayan/patlayan), terminal ilerlemesine bağlı
zorluk artışı. `Arena/WaveManager.cs`'in basit sabit-dalga tasarımından
**mimari olarak tamamen ayrı** — `EnemyAI.AlertNow()`'ın kod yorumu
takviye-spawn kullanım örneği olarak açıkça `WaveDirector`'ı (`WaveManager`
değil) referans veriyor.

**Multiplayer**: Netcode paketi kurulu olsa da, `WaveManager`'ın
spawn mantığı tamamen yerel/tek-oyunculu — sunucu-yetkili bir kavram
yok (bkz. §4).

---

## 13. Puzzle ve Seviye Hedefi (Objective) Sistemleri

Konum: `Assets/Scripts/Flow/*.cs`. Bu grup, `WaveDirector`/`Arena` gibi genel dalga-yönetimi altyapısının üstüne kurulan, sahneye özgü "ne yapılırsa bölüm ilerler" mantığını taşıyan scriptlerden oluşuyor: bir sıralı-şifre bulmacası (`SequenceLock` + `PuzzleConsole`), iki farklı terminal/upload objektifi (`DataTerminal`, `UploadTerminal`), bir tutorial ipucu sistemi (`TutorialHint`/`TutorialHintUI`), iki yerçekimi tetik hacmi (`GravityZone`, `GravityFlipZone`) ve basit tek-seferlik bir spawner (`RoomEnemySpawner`).

### SequenceLock + PuzzleConsole

`Bloodrush.Flow.SequenceLock`, oda hacmini kaplayan bir trigger üstünde duran denetleyici; `PuzzleConsole`'lar (`RequireComponent(BoxCollider)`) `Start()`'ta `FindFirstObjectByType<SequenceLock>()` ile sahnedeki kilidi bulup `Register(this)` ile kendini listeye ekliyor — elle referans bağlama gerekmiyor. Her konsolun sabit bir `Index`'i, kendi rengi (`Color`) ve kendi düşman dalgası (`EnemyPrefab`/`EnemyCount`/`SpawnPoints`) var.

`SequenceLock.LateUpdate()`'te, tüm konsolların `Register` çağrıları bitmiş olacağı ilk karede (`inited` bayrağı) hedef sıra kuruluyor: Inspector'dan `targetOrder` boş bırakılmışsa Fisher-Yates ile rastgele bir permütasyon üretiliyor. Sıra, `screenSlots` renk göstergeleriyle sürekli ekranda (`RefreshScreen()`) gösteriliyor; tamamlanan adımlar `*0.2f` ile sönükleşiyor. Giriş bariyeri (`entryBarrier`) `WaveDirector.Instance.IsFinished` olana kadar kapalı kalıyor — yani bulmaca odasına önce dört veri terminalinin (aşağıda) bitmesi gerekiyor. Oyuncu içeri girince `OnTriggerEnter` bir kereliğine `entryBarrier`'ı arkadan tekrar kapatıyor (`sealedBehind`) ve opsiyonel `combatHallToDisable` ile dışarıdaki dövüş salonunu kapatıyor — hem kaçışı engelliyor hem performans için sahneyi hafifletiyor.

Oyuncu `PuzzleConsole.Update()`'te menzildeyken Etkileşim tuşuna basınca `lockCtrl.OnConsoleActivated(this)` çağrılıyor. `SequenceLock.OnConsoleActivated`: konsolun `Index`'i `targetOrder[step]`'e eşitse doğru — konsol yeşile döner (`SetActivated(true)`), kendi dalgası `SpawnWave` ile doğar (prefab `NavMeshAgent.Warp` ile mesh'e yapıştırılıp `EnemyAI.AlertNow()` ile hemen alarma geçirilir — `WaveDirector.Spawn` ile aynı desen), `step` artar ve ekran yenilenir; son adımdan sonra `Complete()` çağrılır. Yanlışsa: `step` sıfırlanır, tüm konsollar `SetActivated(false)` ile idle rengine döner ve `penaltyEnemyPrefab`'tan `penaltyCount` adet ceza düşmanı `penaltySpawnPoints`'te doğar — yani yanlış sıra hem ilerlemeyi siler hem savaş yükü ekler. `Complete()`'te `bossDoorBarrier` açılır (`SetActive(false)`) ve `onComplete` UnityEvent'i tetiklenir; giriş bariyeri kasıtlı olarak kapalı kalır çünkü zaten arkadan mühürlenmiştir.

### DataTerminal ile UploadTerminal — aynı isim ailesinden ama farklı sistemler

İkisi de `BoxCollider` trigger'lı, ama amaçları ve akışları temelden farklı.

**`UploadTerminal`** genel-amaçlı, kampanya döngüsünün çekirdek objektifi: oyuncu trigger'a girince (`OnTriggerEnter`) `uploading=true` olur ve otomatik başlar — içeride durma şartı yok, bir kez tetiklenip kendi başına ilerler. `Update()`'te `progress`, `uploadDuration` (vars. 90 sn) üzerinden dolar ve her karede `GameHUD.AddUploadProgress(uploadShare * delta)` ile HUD'a yazılır. Kendi `spawnTimer`'ı üzerinden periyodik olarak (`spawnInterval`) `spawnPoints[]`/`enemyPrefabs[]`'ten düz `Instantiate` ile düşman doğurur — `WaveManager`'a benzer basit rastgele spawn, `NavMesh.Warp`/`AlertNow` gibi inceliği yok. Tamamlanınca `exitBarrier` açılır ve `onComplete` UnityEvent'i ateşlenir (`EndingSequence.Begin` buna bağlanıyor, bkz. §16).

**`DataTerminal`** ise Ch3 (Server Core) savunma objektifine özel: oyuncu terminalin içinde **durduğu sürece** (`playerInside`) `progress`, `activateTime` (vars. 6 sn) üzerinden dolar; dışarı çıkarsa ilerleme donar ama silinmez. Doluncaya kadar `Active` static alanı HUD'un "VERİ x/y — %.." satırı için hangi terminalin aktif olduğunu taşır. `Complete()`'te üç şey olur: `GameHUD.AddUploadProgress(uploadShare)` ile upload barına tek seferlik katkı, `WaveDirector.Instance.OnTerminalDone()` ile **escalation basamağı bildirimi** (`WaveDirector.terminalsDone++`; dördü de bitince `Finish()` çıkışı açar — `SequenceLock`'un giriş bariyerinin beklediği koşul tam olarak bu), ve `FireEMP()` — `Physics.OverlapSphere(empRadius)` ile yakındaki tüm `EnemyAI`'ları bulup `Stun(empStun)` + `Knockback(away, empKnockback)` uygulayan, ayrıca `CameraShake` ve zeminde genişleyen bir disk (`EmpVisual` coroutine, dinamik `Cylinder` primitive) ile görselleştirilen bir şok dalgası. `UploadTerminal`'in kendi düşman spawn etmesine karşılık, `DataTerminal` düşman üretmiyor — sadece düşman zaten `WaveDirector`'ın heat-tabanlı sürekli spawn'ıyla geliyor ve terminal onu ilerleme sinyaliyle besliyor. Özetle: `UploadTerminal` = objektif + kendi spawn'ı olan genel terminal; `DataTerminal` = savunma-tipi objektif + dış spawn yöneticisine (`WaveDirector`) rapor veren + EMP nefes payı veren Ch3'e özel terminal.

### TutorialHint / TutorialHintUI

`TutorialHint`, trigger'a girilince `TutorialHintUI.Show(hintText)`'i çağıran pasif bir bölge; `oneShot` true ise bir daha tetiklenmez, `autoHideSeconds>0` ise süre sonunda otomatik kapanır. Çıkışta dikkat edilen bir detay var: `StillInsideHorizontally()` sadece dikey (Y) hareketle (ör. zıplama) kutunun üstünden çıkışları yok sayıyor — oyuncu hâlâ X/Z sınırları içindeyse ipucu kapanmıyor. `TutorialHintUI` tekil, kendi kendini `Ensure()` ile inşa eden bir singleton: `ScreenSpaceOverlay` canvas'ta alt-ortada yarı saydam bir panel, `CanvasGroup.alpha`'yı `MoveTowards` ile yumuşak fade'liyor. `SetPaused()` ile duraklatma menüsü açıkken tamamen gizleniyor.

### GravityZone / GravityFlipZone

`GravityZone`, `PlayerMovement.GravityScale`'i değiştiren trigger hacmi: `affectsGravity=true` oda-geneli düşük-g (floaty) için, `false` ise sadece `AddUpdraft()` ile sürekli yukarı itiş uygulayan iç içe geçmiş bir "updraft kolonu" için kullanılıyor — böylece kolondan çıkmak oda-geneli düşük-g'yi bozmuyor. `GravityFlipZone` ise zıplayınca yerçekimini tersine çeviren (`SetFlipMode(true)`, zemin↔tavan) bir kule/tırmanış mekaniği; içerideyken `GrapplingHook.HookEnabled=false` yapılarak kanca kilitleniyor (flip tırmanışının atlanmaması için). Kod yorumunda açıkça belirtilen bir sağlamlaştırma var: ışınlama (checkpoint respawn, düşme-reset) `OnTriggerExit`'i tetiklemediği için flip modu takılı kalabiliyordu; çözüm olarak `OnTriggerStay` her fizik turunda `seen=true` işaretliyor, `FixedUpdate()` bir tur hiç `Stay` gelmezse `Release()` çağırıp modu serbest bırakıyor. Her iki zone da `GravityRoomBuilder.cs` ve `GravityTowerBuilder.cs` tarafından kurulum-zamanında üretilip yapılandırılıyor (bkz. §17.5) — burada önemli olan nokta: iki zone tipi de builder'lardan bağımsız, kendi başına çalışabilen genel-amaçlı trigger bileşenleri.

### RoomEnemySpawner — WaveDirector'a karşıt basitlik

`RoomEnemySpawner`, kod yorumunun da dediği gibi "WaveDirector gibi karmaşık değil" bir tek-seferlik spawner: trigger'a ilk girişte (`spawned` bayrağı) `enemyPrefab`'tan `count` adet düşmanı `spawnPoints[]` üzerinde döngüsel (`i % spawnPoints.Length`) doğurup `NavMeshAgent.Warp` + `EnemyAI.AlertNow()` ile hemen aktive ediyor, sonra bir daha hiçbir şey yapmıyor — ne heat/eskalasyon, ne çeşitlilik, ne tamamlanma bildirimi. `SequenceLock.SpawnWave` ile neredeyse birebir aynı spawn deseni tekrarlanmış (paylaşılan bir yardımcıya çıkarılmamış), ama `RoomEnemySpawner`'ın olay/geri bildirim yüzeyi hiç yok; tipik kullanım yeri, `WaveDirector`'ın "heat" mantığının anlamsız olacağı yerçekimi/kule odaları gibi tek seferlik, izole karşılaşmalar.

---

## 14. Karanlık Sekans (Dark Sequence) Modülü

Konum: `Assets/Scripts/Flow/DarkSequence/` (namespace `Bloodrush.Flow`). Bu klasör,
projenin geri kalanından kod olarak bağımsız, kendi kapanışını kendi tetikleyen
kapalı bir sahne-senaryosu modülü: oyuncunun bir kaza sonrası el fenerle
karanlık bir endüstriyel/laboratuvar alanında uyanıp sigorta/valf bulmacalarıyla
gücü geri getirdiği, iki korku anı ve bir kaçış patlamasıyla süslenen, ~12
adımlık doğrusal bir sekans. Dosyaların çoğu kod içi yorumlarda `ADIM 1`…
`ADIM 12` etiketleriyle işaretli; bu da modülün tek bir tasarım dokümanından
adım adım uygulandığını gösteriyor.

**Akış**: `WakeUpEffect.cs` (ADIM 1) sahne açılışında kendiliğinden çalışır —
`DamageVignette` desenindeki gibi kod içinde bir `Volume` kurup `Vignette` +
`DepthOfField`'ı 1→0 lerp'leyerek "yerden kalkma" bulanıklığını dağıtır;
`SceneFadeIn`'in karanlığı söndükten hemen sonra devreye girecek şekilde küçük
bir `startDelay` bırakır. Aynı anda `EmergencyLight.cs` (ADIM 1-2) düzensiz
aralıklarla yanıp sönen kırmızı acil durum lambasını, `FlickerLightGroup.cs`
ise `DarkSequenceBuilder`'ın kurduğu tavan lambası gruplarını hem `Light`
hem de emissive panel mesh'i (Unlit materyal, `_UnlitColor`/`_BaseColor`
`MaterialPropertyBlock` ile) birlikte söndürüp yakarak "kontak var" hissi
verir. ADIM 3-4'te oyuncu dağılmış `FuseItem.cs` örneklerini toplar (her biri
`ProximityInteractable`'dan türeyip `panel.AddFuse()` çağırır, panel boşsa
sahnedeki ilk `FusePanel` otomatik bulunur); `FusePanel.cs` gereken sigorta
sayısına ulaşınca `PowerOn()` çalışır ve `onPowered` UnityEvent'i dışarıya
(örn. `ElevatorDoor.Open`, `DoorStatusLight.SetOpen`) bağlanır. `DoorStatusLight.cs`
kapıyı dinlemez — sadece aynı `onPowered`/`onBothOpen`/`onPushed` event'lerine
bağlanan, kilitliyken kırmızı nabız gibi atan, açılınca yumuşak geçişle yeşile
dönen bağımsız bir durum lambasıdır. ADIM 5 ve 8'de `SilhouetteScare.cs`
devreye girer: aynı component hem "belirip kaybolan silüet" korkusunu hem de
(`vanishOnLook` açıkken) oyuncu doğrudan baktığında hızla solup kaybolan
halüsinasyon anını karşılar — `stare` sayacı bakış kesildiğinde sıfırlanır,
yani göz ucuyla görmek figürü öldürmez. Gösterilecek model olarak
`HallucinationFigure.cs` kullanılabilir: oyuncunun karısını temsil eden,
primitive'lerden (küre/silindir) prosedürel olarak kurulan, bilerek simsiyah
değil soluk gri-mavi ve Unlit-transparent yapılmış bir kadın silüeti (saç
kütlesi + dar bel + etek konisi ile "hat" okutuluyor; yüz detayı yok).
ADIM 7'de `ValveInteractable.cs` + `ValveSequence.cs` ikilisi bir zaman
penceresi bulmacası kurar: valf açan `ValveInteractable.OnInteract()`
durumunu `ValveSequence.OnValveOpened()`'a bildirir, `ValveSequence` her açılan
valfi `holdSeconds` süresiyle sayar ve süre dolmadan ikisi de açıksa
`onBothOpen`'ı tetikler, dolarsa `valve.ForceClose()` ile valfi kendiliğinden
kapatır — kod içi yorum, spec'teki "B açılınca A kapansın" + "ikisi aynı anda
açık olsun" çelişkisinin bilerek bu zaman-penceresi modeliyle çözüldüğünü
açıklıyor. ADIM 9'da `ScriptedChaseTrigger.cs` saf atmosferik bir kaçış anı
üretir — buhar/kıvılcım `ParticleSystem`'leri, patlama sesi, `CameraShake.Shake()`
ile iki dalgalı sarsıntı ve opsiyonel ışık söndürme; yorumda özellikle
belirtildiği gibi gerçek bir tehlike/hasar YOK, tamamen tempo yükseltme amaçlı.
ADIM 10'da `PowerRestoreLights.cs` koridor ışıklarını birkaç kez titretip
yarı şiddette sabitler (tek seferlik, `EmergencyLight`'ın aksine döngüsüz);
ADIM 11'de `PushObstacle.cs` (yine `ProximityInteractable` alt sınıfı) "[E] İt"
ile devrilen bir rafı `Vector3`/`Quaternion` lerp'iyle kaydırıp yol açan
engel-objesidir. Son olarak ADIM 12'de `DarkSequenceExit.cs` oyuncu eşikten
geçince ekranı karartır, kapının arkasındaki soğuk beyaz ışığı fade-in eder
(`RevealColdLight()`/`FadeLight()`) ve `DarkSequence.Complete()`'i çağırır.
Genel amaçlı `PlayerTriggerZone.cs` ("oyuncu girince UnityEvent tetikle")
ve dekoratif `LabPropScatter.cs` (devrilmiş bir laboratuvar rafının etrafına
seed'li rastgelelikle deney tüpü/şişe/kavanoz/kasa/dosya saçan greybox prop
üreticisi) sekansın çeşitli noktalarında bu akışı destekleyen yardımcı
parçalar.

**ProximityInteractable — bilinçli bir kural sapması**: `ProximityInteractable.cs`,
dört farklı etkileşimin (`FuseItem`, `FusePanel`, `ValveInteractable`,
`PushObstacle`) ortak atası olan soyut bir `MonoBehaviour`; yaklaşma mesafesi
(`range`), bakış açısı (`lookDot`, kamera ileri vektörü ile hedefe olan
vektörün dot çarpımı), prompt gösterimi (`DialogueUI.ShowPrompt/HidePrompt`)
ve tuş tüketimini (`InteractionInput.TryConsume()`, aynı frame'de birden
fazla etkileşimin tetiklenmesini önleyen static bir "hakem") tek yerde
topluyor; alt sınıflar sadece `PromptText` ve `OnInteract()`'i dolduruyor.
Kod içi yorum bunun projenin genel geleneğinden **bilinçli bir sapma**
olduğunu açıkça belirtiyor: projenin geri kalanında her etkileşim script'i
(`NpcDialogue`, `InteractableBook`, `SeatInteractable`) yakınlık mantığının
kendi kopyasını taşır (tıpkı builder'ların "her biri kendi yardımcı metod
kopyasını taşısın" prensibiyle DRY'den bilerek kaçınması gibi — bkz. §17).
Burada gerekçe tersine dönüyor: tek bir sekansta dört ayrı etkileşim
olduğu için aynı ~30 satırı dört kez kopyalamak yerine ortak taban seçilmiş.
`AimPoint()` ayrıca hedefin obje pivotu yerine (builder'la kurulan objelerin
pivotu genelde tabanda kaldığından) renderer bounds merkezini hedeflemesini
sağlıyor — bu önbellek Play modunda bir kez alınıp editörde her karede
yeniden hesaplanıyor ki raf/prop yeniden kurulunca gizmo bayat veri
göstermesin.

**Atmosfer/render tarafı**: `DarkSceneExposure.cs`, sahneye eklenen tek bir
bileşenle karanlık sekansın tüm render ayarını kurar — `SceneVolumeSetup`'ın
CH3 için tasarladığı GradientSky dolgu ışığı ve otomatik pozlamanın ikisi de
el fenerli bir bölüm için "düşman" olduğundan (dolgu ışığı karanlığı yok
eder, otomatik pozlama fenerin kontrastını geri alır), 200 önceliğiyle
(`SceneVolumeSetup`'ın 100'ünün üstünde) sabit düşük EV pozlama, simsiyah
gökyüzü, yoğun sis ve ayrı Bloom/FilmGrain/`PixelatePass` ayarları içeren bir
`Volume` inşa eder; `Awake()`'de ayrıca sahnedeki tüm mevcut `Light`'ları
`HDAdditionalLightData.SetIntensity` ile `lightIntensityScale`'e göre bir kez
ölçekler. Bu static `LightScale` değeri, modüldeki neredeyse tüm ışık
kontrolcüleri (`EmergencyLight`, `FlickerLightGroup`, `DoorStatusLight`,
`PowerRestoreLights`) tarafından okunup kendi lümen hesaplarına çarpan
olarak uygulanıyor — sahnenin ışık şiddeti tek noktadan (`DarkSceneExposure.exposureEV`)
ayarlanabiliyor. `DarkSequenceBuilder.cs` (tüm greybox oda/koridor zincirini
ve marker objelerini tek tıkla kuran `[ContextMenu]` script'i) ve
`LabShelfBuilder.cs` (metal laboratuvar rafı greybox'ı) bu modülün geometri
kurulum tarafını oluşturuyor; ayrıntıları §17.7'de.

**Kapanış ve bir sonraki bölümden ayrıştırma**: `DarkSequence.cs`, modülün tek
durum sahibi olan minimal bir static sınıf — `IsComplete` bool'u ve bir kez
tetiklenen `OnComplete` event'i dışında hiçbir şey tutmuyor.
`DarkSequenceExit.cs` oyuncu son eşikten geçtiğinde `DarkSequence.Complete()`'i
çağırır; kod içi yorum bunun bilinçli bir mimari sınır olduğunu vurguluyor:
"Karanlık Sekans" bir sonraki bölüme (gözlem odası, kasaba) hiçbir referans
taşımıyor — sadece "bittim" diye haber veriyor. Bir sonraki bölümü kuracak kişi
`DarkSequence.OnComplete`'e abone olur ya da `IsComplete`'e bakar; bu modüle
dokunmasına gerek kalmıyor. `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`
ile işaretli `Boot()` metodu, domain reload kapalıyken static alanların
oturumlar arası sızmamasını sağlıyor — `PlayerLoadout`'ta görülen aynı önlem
(bkz. §5). Bu tasarım, `PlayerLoadout`'un oyuncu ekipmanı için merkezi bir
"bağlayıcı" rolü üstlenmesinin tam tersi bir örnek: burada modül kasıtlı
olarak *hiçbir şeye* bağlanmıyor, sadece kendi tamamlanma sinyalini yayınlıyor.

---

## 15. Aşırı Yük (Overload) Odası

Konum: `Assets/Scripts/Flow/Overload/` (namespace `Bloodrush.Flow`),
5 dosya: `ChargeZone.cs`, `OverloadGenerator.cs`, `OverloadRoom.cs`,
`OverloadRoomBuilder.cs`, `ZoneAvoidance.cs`. Projede zaten üç bulmaca
sistemi var — kod yorumlarına göre `SequenceLock` (sıra), `FusePanel`
(toplama), `ValveSequence` (süreli iki anahtar) — ama hiçbiri
**çatışmayı** araç yapmıyordu. Aşırı Yük odasının fikri savaşın
kendisini bulmacanın çözümü hâline getirmek: jeneratör yalnızca belirli
bir alanda öldürülen düşmanlardan şarj olur, düşmanlar da o alandan
kaçmaya çalışır — oyuncu onları oraya zorla çekmek zorunda kalır.

**Çekirdek kural — `ChargeZone.cs`**: Bir `BoxCollider` (`isTrigger`)
şarj bölgesini tanımlıyor. Kritik tasarım kararı, "İÇERİDE mi öldü"
sorusunun trigger giriş/çıkış defteri yerine **ölüm ANINDA yapılan bir
sınır testiyle** (`box.bounds.Contains`) cevaplanması — kod yorumu bunu
açıkça gerekçelendiriyor: trigger defteri tutmak, düşman ölürken
collider kapanırsa/sıra değişirse sessizce yanlış cevap verebilir; tek
satırlık sınır testi her zaman doğru. `Register(EnemyAI, Health)`,
`OverloadRoom`'un doğurduğu her düşman için `health.onDeath`'e bir
listener ekliyor (global bir ölüm event'i değil — o oyuncunun ölümünü
de tetiklerdi); `OnEnemyDied` düşmanın **gövde merkezini** (ayak
hizası değil — havada çekilirken ölen düşmanın ayağı bölge dışında
kalabiliyor) `Contains` ile test edip başarılıysa `chargePerKill`
ekliyor, değilse "BÖLGE DIŞINDA — ŞARJ YOK" bildirimi basıp hiçbir şey
vermiyor. `chargeNeeded`'e ulaşınca `fullFired=true` olup `onCharged`
UnityEvent'i bir kez ateşliyor; isteğe bağlı `decayPerSecond` (varsayılan
0) oda çok kolay bulunursa şarjın zamanla sızmasını sağlıyor.

**Kaçış davranışı — `ZoneAvoidance.cs`**: `OverloadRoom` her doğan
düşmana çalışma zamanında `AddComponent` ile ekliyor; `EnemyAI`'ın
içine hiç karışmıyor, sadece dışarıdan `NavMeshAgent.SetDestination`
çağırıyor. İki önemli incelik: (1) **`LateUpdate` içinde çalışıyor**,
çünkü `EnemyAI`'ın kendi chase mantığı hedefi `Update`'te veriyor —
`ZoneAvoidance` de `Update`'te olsaydı ikisi aynı karede sırayla
birbirini ezip düşmanı titretirdi; `LateUpdate` her zaman sonra yazıp
kazanıyor. (2) Düşman kancayla çekilirken (`EnemyAI.IsBeingPulled`)
tamamen susuyor — oyuncunun çekişine araya girmiyor — ve bırakıldıktan
sonra da `graceAfterPull` (1.4s) boyunca susmaya devam ediyor; bu
olmasaydı düşman bırakıldığı an bölgeden fırlar, bulmaca neredeyse
imkânsız olurdu. Bu tam olarak `GrapplingHook.cs`'te zaten belgelenen
`pullingEnemy`/`hookedEnemy.StartBeingPulled()` mekanizmasının (bkz.
§5) kancanın *ana kullanım alanı* hâline geldiği yer — kod yorumu bu
özelliğin "oyunda zaten var ama neredeyse hiç kullanılmıyordu" olduğunu
not ediyor. Bölge içindeyken kaçış hedefi `TryPickFleeTarget` ile
merkezden dışa doğru (köşeye sıkışmayı önlemek için 55°'lik beş
alternatif açı deneyerek) `NavMesh.SamplePosition` üzerinden
hesaplanıyor, `repathInterval` (0.5s) ile sınırlanıyor; `zone.IsFull`
olduğunda düşmanlar tamamen normal davranışa dönüyor.

**Geri bildirim — `OverloadGenerator.cs`**: Hiçbir kural bilmiyor,
sadece `ChargeZone.onChargeChanged` (0-1 normalize) UnityEvent'ine
bağlanıp `SetCharge(float)` ile **üç katmanlı** göstergeyi güncelliyor:
gövde emissive parlaklığı + ışık lümeni (`minLumen`–`maxLumen`,
`HDAdditionalLightData.SetIntensity`), soldan sağa dolan segment
çubuğu (kaç kutunun yanacağı `Mathf.RoundToInt(shown * segMats.Length)`
ile hesaplanıyor), ve sürekli çalan `hum` `AudioSource`'unun pitch/volume
değerleri (`minPitch`–`maxPitch`) tizleşerek yükselmesi. `sparkThreshold`
üstünde ayrıca bir `ParticleSystem` kıvılcım efekti devreye giriyor.
Redundancy kasıtlı: karanlık/kalabalık bir çatışmada tek bir ipucu
gözden kaçabilir.

**Orkestrasyon — `OverloadRoom.cs`**: `BoxCollider` trigger'ıyla
oyuncunun girişini yakalayıp `Begin()`'de `entryBarrier`'ı açarak
(mühürleyerek) odayı kilitliyor, `exitBarrier`'ı şarj dolana kadar
kapalı tutuyor. `RunWaves()` coroutine'i dalgaları art arda doğuruyor;
doğurma sırası (`Instantiate` → `NavMesh.SamplePosition`+`Warp` →
`AlertNow()`) bilinçli olarak `RoomEnemySpawner`'dan kopyalanmış —
o bileşen tek seferlik/trigger tabanlı olduğu için tekrarlı dalga
ihtiyacına uymuyordu. Her dalgada doğan her düşmana hem `ChargeZone.Register`
hem de yeni bir `ZoneAvoidance` bileşeni bağlanıyor; `PickSpawnPoint`
bölge içinde kalan spawn noktalarını atlayarak düşmanın bedava şarj
vermesini engelliyor. Sonraki dalga, mevcut dalganın `nextWaveKillRatio`
(varsayılan %50) kadarı ölünce ya da `maxWaveDelay` (25s) dolunca
tetikleniyor; `endlessUntilCharged` açıkken dalgalar bitse bile şarj
dolmadıysa oda kilitlenmesin diye yeniden dalga gönderiliyor.
`zone.onCharged` → `Solve()` bağlantısı çıkışı açıp girişi geri
açıyor. `OverloadRoomBuilder.cs`, bu odayı mevcut "Puzzle Room" kabuğunun
içine greybox olarak kuran, projenin diğer builder'larıyla (`PuzzleRoomBuilder`,
`ReceptionBuilder`, `LabShelfBuilder`) aynı self-contained/`[ContextMenu]`
desenini izleyen ayrı bir editor-time araç (bkz. §17.6).

---

## 16. Anlatı / Akış Altyapısı (GameFlow ve Sahne Geçişleri)

Konum: `Assets/Scripts/Flow/GameFlow.cs`, `GameProgress.cs`, `Checkpoint.cs`,
`KillVolume.cs`, `Elevator.cs`, `ElevatorDoor.cs`,
`ElevatorAccidentSequence.cs`, `IElevatorSequenceHook.cs`,
`EndingSequence.cs`, `IntroSalonController.cs`, `PresentationSequence.cs`,
`SceneFadeIn.cs`, `LevelExit.cs`, `PlayerStartPoint.cs`. Bu grup, kampanyanın
"iskeleti"ni oluşturuyor: sahne içi ölüm/respawn otoritesi, sahneler arası
kalıcı durum ve sinematik geçişler. Bunların dışında kalan `Arena.cs`,
`UploadTerminal.cs`, `DataTerminal.cs`, `WaveDirector.cs`, `TutorialHint.cs`
zaten §12/§13'te işlendiği için burada tekrarlanmıyor.

- **`GameFlow.cs`** — her kampanya sahnesine tek bir instance olarak konan
  **sahne-başı koordinatör**. `Awake()`'te kendini statik `instance`'a
  yazıyor, `Time.timeScale`'i 1'e zorluyor (hit-pause/slow-mo ortasında
  ölüm ihtimaline karşı) ve `hasCheckpoint`'i sıfırlıyor — checkpoint
  **sahneler arası taşınmaz**, her sahne temiz başlar. `Start()`'ta
  `Health.onDeath`'e `OnPlayerDied` bağlanıyor, ardından **bölüm
  loadout'u** uygulanıyor: `PlayerShoot.LauncherEnabled`,
  `SetUnlocked(Firearm.Shotgun/Lmg, GameProgress.xUnlocked ||
  allWeaponsThisScene)`, `GameHUD.Instance.SetVisible(showHUDBars)`.
  Bunun hemen ardından `PlayerLoadout.Apply(player)` çağrılıyor — kod
  yorumu sırayı açıkça gerekçelendiriyor: `Apply()` sadece **kısar**,
  asla vermez, dolayısıyla bölüm ayarlarının *üstüne* güvenle
  bindirilebiliyor. `startDisarmed=true` olan sahnelerde (asansör
  kazasından sonrası) `PlayerLoadout.EnterPostCrashState(startJumpScale)`
  çağrılır — bu bayrak tur boyunca **sadece bir kez** etki eder
  (`postCrashApplied` statiği), yani sahneyi doğrudan Play'lemek de aynı
  duruma düşer ama sonradan bulunan silah ölüp checkpoint'te
  canlanınca kaybolmaz. Ölüm/respawn: `OnPlayerDied()` →
  `RespawnRoutine(hasCheckpoint)` — `PlayerMovement`/`PlayerShoot`
  kapatılır, `CreateOverlay(Color.black)` ile siyah bir Canvas/Image
  runtime'da inşa edilir (checkpoint varsa 0.4s hızlı fade, yoksa
  `deathFadeDuration`); checkpoint varsa `Health.Revive()` +
  `PlayerMovement.Teleport(cpPos, cpRot)` ile sahne **yeniden
  yüklenmeden** canlandırılır, yoksa `GameHUD.UploadProgress` sahne
  başına sarılıp `SceneManager.LoadScene(aktif sahne)` çağrılır.
  `LoadNext()` ve `CreateOverlay()` statik yardımcıları `LevelExit`,
  `Elevator`, `IntroSalonController`, `EndingSequence` gibi diğer Flow
  scriptleri tarafından ortak fade/sahne-geçiş API'si olarak kullanılıyor
  — karartma mantığı tek yerde.
- **`GameProgress.cs`** — sahneler arası **kalıcı** (statik) ilerleme:
  `ShotgunUnlocked`, `LmgUnlocked`. `Unlock(Firearm)` / `IsUnlocked(Firearm)`
  ile okunuyor (revolver her zaman `true` döner). `ResetRun()` yeni oyun
  başında CH1'in `GameFlow.resetProgressOnStart=true` bayrağıyla çağrılıyor
  ve ayrıca `PlayerLoadout.ResetRun()`'ı da tetikliyor — kod yorumu bunu
  bilinçli olarak gerekçelendiriyor: yeni bir oyun asla "asansör kazası"
  kısıtıyla başlamamalı. `GameProgress` "neyi kazandın" (kalıcı), Player
  klasöründeki `PlayerLoadout` (statik, `Gear` flag enum'u: Revolver/
  Shotgun/Lmg/Launcher/Hook/Melee/Stimulant) ise "şu an elinde ne var"
  (anlık kısıt) sorusunu cevaplıyor; ikisi çakışınca `PlayerLoadout.Apply()`
  her zaman daha kısıtlayıcı olanı uyguluyor.
- **`Checkpoint.cs`** — `BoxCollider(isTrigger)`; `OnTriggerEnter`'da
  `PlayerMovement` doğrulaması yapıp (`used` bayrağıyla tek seferlik)
  `GameFlow.SetCheckpoint(transform)` çağırıyor; respawn yönü objenin
  Y ekseni (`eulerAngles.y`) olarak kaydediliyor.
- **`KillVolume.cs`** — aynı trigger deseni; sahneyi baştan yüklemek
  yerine doğrudan `GameFlow.RespawnAtCheckpoint()` çağırıyor — bu da
  `GameFlow`'un statik `RespawnRoutine`'ini `respawning` guard'ıyla
  tetikliyor (aynı anda hem ölüm hem düşme respawn'ı çakışmasın diye).
- **`LevelExit.cs`** — bölüm sonu tetikleyicisi: opsiyonel
  `requireNoEnemies` ile sahnede canlı `EnemyAI` varken geçişi
  engelleyebiliyor; `ExitRoutine()` `GameFlow.CreateOverlay` ile kararıp
  `nextSceneName` doluysa isimle, boşsa `GameFlow.LoadNext()` (build
  index + 1, liste biterse ana menüye) yükleniyor.
- **`SceneFadeIn.cs`** — **karartmayı karşı sahneye taşıyan** yardımcı.
  Sorun şu: `GameFlow.CreateOverlay` sahne objesi ürettiği için
  `SceneManager.LoadScene` onu yok ediyor ve yeni sahne bir kare tam
  parlaklıkta patlayıp *ancak sonra* kararabiliyordu. `CarryOverlay(img,
  hold, fadeDuration)` overlay'in kök Canvas'ını `DontDestroyOnLoad`
  yapıp kendine bir `SceneFadeIn` component'i ekliyor,
  `SceneManager.sceneLoaded`'a abone oluyor; yeni sahne yüklenince
  `Run()` coroutine'i `PlayerMovement`'i kısa süre kilitleyip (oyuncu
  karanlıkta kör yürümesin), `hold` kadar sessiz bekleyip fade-in
  yapıyor. `Elevator` ve `PresentationSequence`, sahneler-arası geçişte
  bu deseni birebir kullanıyor.
- **`PlayerStartPoint.cs`** — bkz. §17.12; `ArenaBuilder`'ın ürettiği
  başlangıç noktasıyla aynı sınıf, tek görevi `Start()`'ta oyuncuyu
  `PlayerMovement.Teleport` ile kendi konum/yönüne ışınlamak.

**Asansör alt sistemi — üç parçalı, hook tabanlı ayrım.** `Elevator.cs`
kabini fiziksel olarak **hiç hareket ettirmez**; `OnTriggerEnter`'da
`PlayerMovement`'i yakalayıp `insideTimer >= doorCloseDelay` olunca
`Activate()`→`Run()` coroutine'ini başlatır: `entryDoor.Close()`, ekranı
karart (`useFade`/`fadeDuration`), `blackHold` kadar `rideLoopClip` çalarken
karanlıkta bekle, ardından ya `destinationScene` doluysa
`SceneFadeIn.CarryOverlay` + `SceneManager.LoadScene` ile başka sahneye geç,
ya da aynı sahnede `playerMovement.Teleport(destination...)` ile ışınla,
`exitDoor.Open()`, fade-in, `onArrived` event'i. `ElevatorDoor.cs` bu iki
kapıyı (`entryDoor`/`exitDoor`) yönetiyor — kanatları `leftOpenOffset`/
`rightOpenOffset` kadar `SmoothStep` ile kaydırıyor, `autoOpenOnApproach`
giriş kapısında açık, çıkış kapısında kapalı tutuluyor (sadece
`Elevator.exitDoor.Open()` çağrısıyla açılır).

Asıl mimari karar **`IElevatorSequenceHook.cs`**'de: dört metotlu bir
arayüz (`OnCabinCinematic(Elevator)` — `IEnumerator`, kabin sinemasını
*bekletebilmek* için bilerek `UnityEvent` değil; `OnBlackoutBegin
(chaosWindow)`; `OnFullyBlack()`; `OnRideEnd()`). `Elevator.Start()`
bunu **aynı GameObject üzerinde** `GetComponent<IElevatorSequenceHook>()`
ile arıyor — Inspector bağlantısı yok, dolayısıyla yanlış asansöre
bağlanma riski de yok; hook bulunamazsa (`hook == null`) `Run()` eski
davranışı birebir koruyor (`entryDoor.CloseAndWait()`). Bu, `Elevator.cs`'in
kaza anlatısından **tamamen habersiz** kalmasını sağlıyor: CH1'deki sade
asansör hiçbir değişiklik görmeden çalışmaya devam ediyor.
**`ElevatorAccidentSequence.cs`** bu arayüzü implemente eden
`[RequireComponent(typeof(Elevator))]` bileşeni — `OnCabinCinematic`
kamerayı `focusPoint`e (tavandaki halat) `Quaternion.LookRotation` ile
çevirir; `OnBlackoutBegin` ışık kırpışması (`Chaos()`) ve kafa savurma
(`PanicLook()` — ayrık hedeflere `easeOutBack` overshoot ile Slerp, açıya
göre ölçeklenen süre) coroutine'lerini paralel başlatır; `OnFullyBlack`
ışıkları kalıcı söndürür, sesi `FadeOutAudio`'ya bırakır,
`rider.SetFlipMode/DisableGravity/SpeedMultiplier` sıfırlanır (yerçekimi
odasından kalıntı temizliği) ve — sekansın gerçek narrative etkisi —
`PlayerLoadout.DisarmAll()` çağrılır; `OnRideEnd` kamera rotasyonunu geri
yükleyip `rider.SetLookPitch(wakePitch)` ile "yeni uyanmış" bakış açısı
verir. `OnDisable()` yarıda kesilme (ölüm/sahne değişimi) durumunda
coroutine'leri ve kamerayı temizliyor — `BossBlackoutSequence` ile aynı
konvansiyon.

**`EndingSequence.cs`** son `UploadTerminal`'in `onComplete`'ine bağlanan
`Begin()` ile başlıyor: tüm `EnemyAI`'ları durdurup devre dışı bırakıyor,
`PlayerShoot`/`GrapplingHook`'u kapatıyor, `Time.timeScale`'i 1→0.2 lerp'liyor,
beyaza fade edip runtime'da `Text` nesneleriyle harf-harf daktilo kapanış
metni + "BLOODRUSH" damgası yazdırıyor, `Input.anyKeyDown` ile ana menüye
dönüyor. **`IntroSalonController.cs`** CH1 açılışını yönetiyor: silahı/
`PlayerShoot`/`GrapplingHook`/`StimulantSystem`'i kapatıp
`SpeedMultiplier`'ı düşürüyor, `IntroTextSequence.OnFinished` event'ini
bekleyip (itiraf metni bitmeden koltuk etkileşimi açılmıyor) mesafe+dot
kontrolüyle `[E] Otur` prompt'unu gösteriyor, `SitAndExit()` kamerayı
`sitCameraPose`'a `SmoothStep`'le kaydırıp `GameFlow.CreateOverlay` +
sahne geçişiyle kapanıyor.

**`PresentationSequence.cs`**, §11'de belgelenen `PresentationScreen.cs`
(dünya-uzayı canvas'ta döngülenen `string[] slides`) ile aynı "sunum"
temasını paylaşan ama **teknik olarak tamamen ayrı, daha yeni** bir
bileşen: gerçek video oynatıyor. `SeatInteractable`'daki oturma deseni
kopyalanmış (doğrudan kullanılamamış, çünkü o dosya `BookSession`'a
kilitli); `Run()` oyuncuyu `seatPoint`'e oturtup karartıyor,
`PrepareAndPlay()` bir `VideoPlayer`'ı `VideoRenderMode.RenderTexture`
ile (kod yorumu: `CameraNearPlane` modu HDRP'de güvenilir çalışmıyor)
kod-üretimi bir `RenderTexture` + `RawImage`'a bağlıyor, `musicClip`'i
**aynı karede** (`video.Play()` ile `music.Play()` arasında yield yok)
başlatıyor. `RoomMusic.FadeOut()` ile ortam müziği susturuluyor.
Bitiş `loopPointReached` event'iyle `FinishOut()`'a düşüyor;
`allowSkip`/`skipKey` açıksa `SkipOut()` video+müziği birlikte fade-out
ediyor. Her iki yol da `SceneFadeIn.CarryOverlay` ile perdeyi yeni sahneye
taşıyor. Proje şu anda video-tabanlı (`PresentationSequence`) ve
slayt-tabanlı (`PresentationScreen`) iki farklı sunum mekanizmasını
paralel, birbirinden habersiz olarak barındırıyor.

**Kaynak notu**: Bu bölümün önemli kısmı `docs/BLOODRUSH_Gelistirme_Ozeti.pdf`
(15 Temmuz 2026) kaynağından doğrulanarak derinleştirildi; ama `Elevator`,
`ElevatorDoor`, `ElevatorAccidentSequence`, `IElevatorSequenceHook`,
`SceneFadeIn`, `GameProgress`, `PresentationSequence` ve `PlayerLoadout`
PDF'te **hiç geçmiyor** — hepsi o özetten sonraki iki ayda eklenmiş yeni
altyapı.

---

## 17. Harita İnşa Araçları (Greybox Level Building)

Konum: `Assets/Scripts/Flow/` (çoğu dosya doğrudan bu klasörde; `Overload/` ve
`DarkSequence/` alt klasörleri kendi set-piece'lerinin builder + runtime
script'lerini bir arada tutuyor). Bunlar **oyun içi çalışan sistemler değil**,
Editor'da greybox seviye geometrisi üretmek için yazılmış yaklaşık 19 adet
`MonoBehaviour` — hepsi aynı sözleşmeyi paylaşıyor: `[ContextMenu("...")]`
ile Inspector'ın sağ üst ⋮ menüsünden tek tıkla tetiklenir, kendi ürettiği
child GameObject'leri yönetir (`Build()` her çalıştığında önce
`transform.GetChild(i)` döngüsüyle `DestroyImmediate` eder, sonra sıfırdan
yeniden kurar), elle yapılmış kapı/geçiş değişikliklerine dokunmaz ve
`isStatic = true` işaretleyerek NavMesh bake'ine hazır geometri üretir.

**Mimari not**: `EnemyAI`/`BossAI` üçlüsünde olduğu gibi (bkz. §6), bu
builder'lar arasında da **ortak bir temel sınıf yok** — ama burada bu bilinçli
bir tercih olarak kod yorumlarında açıkça belirtiliyor. `LabShelfBuilder.cs`
ve `ReadingCornerBuilder.cs` gibi neredeyse aynı iskeleti (`MakeBox`/`MakeCyl`
yardımcıları, kendi materyal önbellekleri) üreten dosyalar bile bunu kasıtlı
olarak kopyalıyor: *"her builder kendi yardımcılarının kopyasını taşıyor —
bilerek DRY değil, her builder tek başına ayakta dursun diye"*. Amaç, bir
builder'da yapılan değişikliğin (örn. `RoomBuilder`'ın ışık mantığı) başka
bir builder'ı (`GravityRoomBuilder`) kırmasını önlemek — her dosya
self-contained.

### 17.1 RoomBuilder.cs — temel desen

En basit ve projedeki tüm dikdörtgen-oda builder'larının fiilen kopyalandığı
kalıp. `Build()` (`[ContextMenu("Odayı Kur")]`) zemin + tavan (opsiyonel,
`buildCeiling`) + 4 duvar (`Duvar_Kuzey/Guney/Dogu/Bati`) üretir; her parça
`GameObject.CreatePrimitive(PrimitiveType.Cube)` ile oluşturulup `MakeSlab()`
yardımcısından geçer (isim, local pozisyon, ölçek, `isStatic = true`). Ardından
`BuildLights()` tavana `width`/`depth`'e göre `lightSpacing` aralıklı bir grid
düşer — her hücreye `HDAdditionalLightData.SetIntensity(lightLumen,
LightUnit.Lumen)` ile lümen-tabanlı, gölgesiz (`EnableShadows(false)`,
greybox'ta performans için) bir `Point` ışık. Bu "zemin+tavan+4 duvar+MakeSlab
+ HDAdditionalLightData ışık" iskeleti; `IndustrialHallBuilder`,
`PuzzleRoomBuilder`, `GravityRoomBuilder`, `GravityTowerBuilder`,
`DarkSequenceBuilder` gibi neredeyse tüm oda builder'larında satır satır
tekrarlanıyor (kapı boşluklu duvar versiyonu — `BuildGappedWall` — dahil).
Kapı boşluğu desteği yok; kullanıcı kurulduktan sonra istediği duvarı silip
elle açıklık bırakmak zorunda — bu ihtiyaç sonraki builder'larda
`gapWidth`/`doorHeight` parametreleriyle çözülmüş.

### 17.2 StairBuilder.cs

En minimal builder. `Build()` (`[ContextMenu("Merdiven Kur")]`) `stepCount`
kadar art arda yükselen **dolu** (zeminden basamak üstüne kadar tam blok)
küp üretir — her basamağın Y ölçeği `topY` (o basamağın üst yüzeyi), böylece
oyuncu altında boşluk kalmadan güvenle tırmanır. Bu "her basamak zeminden
doludur, hiçbiri havada kalamaz" deseni; `IndustrialHallBuilder.BuildStair()`
ve `ArenaBuilder.BuildStair()` içinde daha karmaşık (kavisli/kardinal)
versiyonlarla yeniden üretiliyor — ikisi de kod yorumunda StairBuilder'ı
referans alıyor.

### 17.3 ArenaBuilder.cs ve IndustrialHallBuilder.cs (yerini alan)

**`ArenaBuilder.cs`** — Ch3 için ilk tasarlanan, tamamen simetrik/dairesel
"server arena" kurucusu. `Build()`, merkeze doğru daralıp yükselen `tierCount`
kadar halka katı (`MakeCylinder`) + her kat arası 4 kardinal merdiven
(`BuildStair`, açı bazlı) + en yüksek merkez çekirdek + 4 kardinal giriş
boşluklu dış duvar (`BuildWallWithGaps`, `NearCardinal` açı toleransıyla) +
emissive floresan tavan panelleri (HDRP/Unlit HDR renk, Bloom eşiği üstü,
her panelin altına gerçek `Point` ışık — `BuildCeilingPanels`) + server rafı
siperleri (`BuildRacks`) + 4 spawn noktası + merkez çekirdeğin tepesinde bir
`PlayerStartPoint` üretir. Dikkat çeken bir implementasyon detayı:
`MakeCylinder()` Unity'nin `Cylinder` primitive'inin varsayılan
`CapsuleCollider`'ını bilerek siliyor ve yerine `MeshCollider` koyuyor —
kod yorumu bunun sebebini açıkça belirtiyor: düz diske ölçeklenmiş bir
cylinder'ın capsule collider'ı devasa bir görünmez kubbeye dönüşüp oyuncuyu
kenardan düşürüyordu (bu, `docs/BLOODRUSH_Gelistirme_Ozeti.pdf`'teki
"ArenaBuilder zemininde oyuncu kenardan kayıp düşüyor" bug kaydıyla birebir
örtüşüyor).

**`IndustrialHallBuilder.cs`** — kod yorumunda açıkça "ArenaBuilder'ın
dairesel arenasının **yerine geçer** — dairesel yapı merkeze daraldığı için
terk edildi" diye belirtilmiş, Ch3'ün güncel greybox kurucusu. `ArenaBuilder`
gibi simetrik/prosedürel değil, **veri-tabanlı asimetrik** bir tasarım:
`Platform[] platforms` dizisi (her eleman `center`, `size`, `buildStair`,
`stairYawDeg`, `spawnMarker`, `terminalMarker` alanlarıyla elle
tanımlanmış — varsayılan 5 platform: `OrtaPlatform_Bati/Guney/Dogu`,
`Kopru_Merkez`, `Kopru_Bati`) + `Rack[] racks` dizisi. `Build()`
(`[ContextMenu("Salonu Kur")]`) dikdörtgen kabuk kurar (batı=giriş,
doğu=boss odası çıkışı, `BuildGappedWall` ile lentolu boşluk), sonra her
`Platform` için döşeme + merdiven-yönüne-yakın-köşeyi-atlayan kolon dörtlüsü
(`BuildPlatform`, kolon çakışması merdiven yönüyle `Vector3.Dot` kontrolüyle
önleniyor) + isteğe bağlı `ElevatedSpawnPoint`/`TerminalSpot` marker'ı üretir.
`buildTerminals` açıkken `MakeTerminalOrMarker()` marker yerine doğrudan
çalışan bir `DataTerminal` (trigger + Glow kaide + FillBar) kurar — yani bu
builder sadece geometri değil, gameplay component'i de otomatik bağlıyor.
`layoutScale` tek bir çarpanla tüm iç yerleşimi (platform konum/boyutları,
raf/marker konumları) orantılı büyütüyor, kabuk `width`/`depth` ile ayrı
büyütülüyor. `ArenaBuilder`'daki emissive panel + `MeshCollider` teknikleri
burada da (dikdörtgen grid versiyonu, `BuildCeilingPanels`) tekrarlanıyor.

### 17.4 PuzzleRoomBuilder.cs

Ch3'teki sıralı güvenlik kilidi odasını kurar; `IndustrialHallBuilder` ile
aynı kabuk deseni (kapı boşluklu batı giriş/doğu boss çıkışı). `Build()`
(`[ContextMenu("Puzzle Odası Kur")]`) `consoleColors[]` dizisinin uzunluğu
kadar `PuzzleConsole` (`BuildConsole` — trigger + gövde + renkli emissive
"Glow" + `spawnsPerConsole` adet spawn marker) + merkezi bir sıra ekranı
(`BuildScreen`, renk sayısı kadar emissive `Slot`) + `entryBarrier`/
`bossBarrier` adlı açılıp-kapanabilen bariyer blokları + `penaltySpawnCount`
kadar ceza spawn noktası + `PlayerStart` üretir. Son adımda hepsini tek bir
`SequenceLock` component'ine `Configure(entryBarrier, bossBarrier,
slots.ToArray(), penaltySpawns.ToArray())` ile bağlıyor ve oda-hacmi kadar
bir `BoxCollider` trigger ekliyor (oyuncu girince arkadan mühürlenme). Yani
builder sadece geometriyi değil, `SequenceLock`/`PuzzleConsole` runtime
bağlantılarını da otomatik kuruyor — kullanıcının elle yapması gereken tek
şey her konsola düşman prefabı/sayısı ve ceza prefabı atamak.

### 17.5 GravityRoomBuilder.cs ve GravityTowerBuilder.cs

**`GravityRoomBuilder.cs`** — yerçekimi odasının set-piece'i; ortada bir
`chasmWidth` genişliğinde uçurum, iki kenarda batı/doğu zemin platformu.
`useFlip` bayrağı iki tamamen farklı geçiş moduna geçiş yapıyor: açıkken oda
genelini kaplayan bir `GravityFlipZone` trigger'ı (zıplayınca yerçekimi ters
döner), kapalıyken `GravityZone.Configure(true, roomGravityScale, 0f)` ile
düşük-g bölgesi + `updraftColumns` kadar sadece-yukarı-iten dikey sütun
(`GravityZone.Configure(false, 1f, updraftSpeed)`). `tutorialMode` bayrağı
ayrıca içeriği kökten değiştiriyor: açıkken asılı platform kalabalığı ve
düşman spawner'ı kurulmuyor, bunun yerine uçurumun altına bir `KillVolume`
+ başlangıç `Checkpoint` konuyor (düşünce anında son checkpoint'te
canlanma) — "asıl zorluk `GravityTowerBuilder`'da, burada sadece flip
öğretiliyor" mantığı. Kapalıyken `RoomEnemySpawner.Configure(points)` ile
oda-hacmi trigger'lı bir düşman spawner'ı da kuruluyor.

**`GravityTowerBuilder.cs`** — "Flip Kulesi", zeminZ↔tavan platformlarının
sırayla YUKARI-flip/AŞAĞI-flip ile tırmanıldığı dikey bir şaft. Kod yorumu
tasarım gerekçesini ayrıntılı açıklıyor: platformlar zikzak/iki-kolon yerine
**"yürüyen" (marching)** düzende, her biri benzersiz bir X'te — çünkü aynı
kolonda üst üste gelen zeminler alttakinin yukarı-flip'ini bloklardı.
`BuildClimbPlatforms()` her zemin arasına bir tavan yerleştiriyor;
`Hup` (yukarı-flip kazancı) her zaman `Hdown`'dan (aşağı-flip kaybı) büyük
tutularak net tırmanış (`Hup - Hdown`) sağlanıyor. Her platforma otomatik
`Checkpoint` (`AddCheckpoint`) ve dipte bir `KillVolume` (`BuildFallCatcher`)
+ **prosedürel mesh** ile üretilmiş bir diken tarlası (`SpikeMesh()` — elle
4 üçgenli piramit mesh'i, normaller `RecalculateNormals` ile hesaplanan,
çift-yönlü üçgenlerle her açıdan görünür kılınmış statik mesh) + parlayan
kırmızı "enerji zemini" içeren görsel bir tehlike göstergesi
(`BuildBottomHazard`) barındırıyor — öldürme işini tamamen `KillVolume`
yapıyor, diken mesh'inin collider'ı yok.

### 17.6 OverloadRoomBuilder.cs

"Aşırı Yük" odasının gameplay sistemi (§15) başka bir bölümde ele alınıyor;
bu builder sadece o sistemin **içini** kurar. Diğerlerinden farkı: kendi
container'ı (`"AsiriYuk"` child) dışında **hiçbir şeye dokunmuyor** — mevcut
Ch3 "Puzzle Room" kabuğunun (zemin/tavan/duvarlar/tavan ışıkları) İÇİNE
additive olarak yerleşiyor, kabuğu asla silmiyor. `Build()`
(`[ContextMenu("Aşırı Yük Odasını Kur")]`) bir `ChargeZone` (kare şarj
bölgesi: emissive kenar çubukları + köşe direkleri), bir `OverloadGenerator`
(gövde + segment bazlı dolum çubuğu + `ParticleSystem` kıvılcım efekti +
uğultu `AudioSource` + point ışık), giriş/çıkış bariyerleri ve
`spawnXZ[]`'den türetilen spawn noktalarını üretip hepsini tek bir
`OverloadRoom.Configure(zone, generator, entry, exit, spawns)` çağrısıyla
birbirine bağlıyor. Kurulum sonunda `Debug.Log` ile kullanıcıya "Enemy
Prefab alanına kancayla çekilebilen küçük düşmanı ata" hatırlatması
bırakıyor.

### 17.7 DarkSequenceBuilder.cs ve LabShelfBuilder.cs

**`DarkSequenceBuilder.cs`** — Karanlık Sekans'ın (gameplay tarafı §14)
**tüm** greybox yerleşimini (10 alanlık oda+koridor zinciri: `A_UyanisOdasi`
→ `B_KesifAlani` + gizli yan oda `B2` → `C_KilitliKapiEsigi` →
`D_MetalikKoridor` → `E`/`F`/`G` valf odaları+koridoru → `H_KacisKoridoru`
→ `I_SonOda`) tek komutla kuruyor. `Shell()` yardımcısı alanları `+X`
ekseninde zincirleme diziyor (`cursorX` ilerleyen imleç), her alan için A-C
arası "eski/beton" (`darkMat`) ve D-I arası "metalik/endüstriyel"
(`metalMat`) olmak üzere **iki ayrı görsel palet** kullanıyor — bu,
hikâyedeki doku/his kopuşunu (ADIM 6) coğrafi olarak da işaretliyor.
Kritik fark: bu builder **component'lerin kendisini kurmuyor** — her alanın
içine `Marker()` ile isimlendirilmiş boş GameObject'ler (`A_PlayerStartYeri`,
`B_Sigorta1Yeri`, `G_ValfSekansiYeri` vb.) bırakıyor, script ekleme ve
event bağlama işini kullanıcıya devrediyor. `CeilingLights()` tavan
ışıklarını **bilinçli olarak kapalı** (`light.enabled = false`) kuruyor —
sekans "karanlık" temalı olduğu için. Kod yorumu açık bir uyarı içeriyor:
"Kur" tekrar çalıştırılırsa TÜM child'lar (marker'lara elle eklenmiş
script'ler dahil) silinir — yerleşim netleştikten sonra bir daha
çalıştırılmaması gerekiyor.

**`LabShelfBuilder.cs`** — `ReadingCornerBuilder.BuildShelfUnit()` ile
**aynı iskeleti** (arka panel + yan çerçeveler + üst + yatay raflar) bilerek
kopyalayan, ahşap kitaplık yerine metal laboratuvar rafı üreten bağımsız bir
builder. Raflara `propsPerRow` kadar prosedürel bilimsel eşya
(`TestTube`/`Flask`/`Jar`/`Crate`/`Folder`, `seed`'li `System.Random` ile
tekrarlanabilir dağılım) serpiyor; `tippedRatio` kadarı devrik duruyor
("dağınıklık hissi"). Devrilmiş raf istenirse builder'ın kendisi dik
kurulup **parent objesi** devrilir — eşyalar child olduğu için birlikte
döner; ayrıca rafla birlikte dönmemesi gereken yerdeki parçalar için ayrı
bir `LabPropScatter` kullanılması öneriliyor (bkz. §14).

### 17.8 ReadingCornerBuilder.cs, ReceptionBuilder.cs, EntranceFacadeBuilder.cs

**`ReadingCornerBuilder.cs`** — kod yorumu, bu builder'ın **bilerek**
`ReceptionBuilder`'dan ayrı tutulduğunu açıklıyor: kitaplara ileride
"okunabilir" (etkileşimli) davranış eklenecek, eğer okuma köşesi
`ReceptionBuilder`'ın kendi container'ının içinde olsaydı, masaya/koltuğa
yapılan her küçük ayar elle eklenmiş etkileşim component'lerini de silerdi.
`Build()` (`"Okuma Köşesi Kur"`) `shelfUnitCount` kadar kitaplık ünitesi
(rastgele renkli/boyutlu "kitap sırtı" bloklarıyla dolu raflar,
`System.Random(7)` sabit seed) + greybox kanepe (ya da `seatingPrefab`
atanmışsa gerçek model) + sehpa + okuma lambası (direk+baza+emissive abajur+
point ışık) kuruyor.

**`ReceptionBuilder.cs`** — additive prop yerleştirici; odanın kendi
duvar/zemin/ışık/mekaniğine dokunmadan sadece `"ResepsiyonProps"`
container'ını kurup yeniliyor. Her bölüm (`buildLogoWall`, `buildDesk`,
`buildTurnstiles`, `buildSeating`, `buildSign`, `buildPlanters`,
`buildCeilingAccent`) ayrı ayrı açılıp kapatılabiliyor; `logoTexture`,
`plantPrefab`, `seatingPrefab` gibi asset slotları boşken geliştirilmiş
greybox placeholder'lar (örn. `GreyboxPlant` — gövde+katmanlı küre yaprak)
devreye giriyor, doluyken gerçek model instantiate ediliyor
(`InstancePrefab`). `GreyboxSofa()` yardımcı metodu `ReadingCornerBuilder`
içinde birebir aynı şekilde tekrarlanıyor (yine bilinçli kopya).

**`EntranceFacadeBuilder.cs`** — en karmaşık ve **iki aşamalı** builder.
1. adım (`"1) Kapı Boşluğu Aç"`, tek seferlik): `targetWall`'un gerçek
dünya `Renderer.bounds`'ını okuyup o duvarı **siler**, yerine lento + iki
kenar duvarından oluşan 3 parça koyar; Editor'da `Undo.SetCurrentGroupName`
+ `Undo.CollapseUndoOperations` ile tüm işlemi **tek bir Ctrl+Z adımına**
gruplayan özel bir dikkat var. Hesaplanan cephe merkezi
(`anchorWorldPos`) ayrı bir serialize alanında saklanıyor — kod yorumu
bunun sebebini açıklıyor: builder objesi sonradan yanlışlıkla sürüklenirse
bile 2. adım hep doğru konumda kurulsun diye, builder'ın **o anki**
transform'una değil bu cache'e güveniliyor. 2. adım (`"2) Giriş Cephesi
Kur"`, tekrar tekrar çalıştırılabilir, additive) kapı kanatlarını **yeniden
kod yazmadan `ElevatorDoor` component'ini** `Configure()` ile kuruyor (proje
içi cross-reuse örneği, bkz. §16), iki yanına collider'lı yarı-saydam cam
panel (`HDRP/Lit` opak üretilip kod yorumunda "Surface Type'ı Inspector'dan
elle Transparent yap" notu bırakılıyor — HDRP transparent modunun kod
içinden güvenilir ayarlanamadığı açıkça belirtiliyor), düşme güvenliği için
görünmez bir "niş" durdurucusu, ve tamamen prosedürel bir **dış manzara**
(rastgele serpilmiş ağaçlar — `treePrefab` boşsa greybox gövde+yaprak küre —
+ otomatik pencere ızgaralı tek büyük bina + devasa gökyüzü backdrop'ı,
`System.Random(seed)` ile tekrarlanabilir) kuruyor.

### 17.9 AcidPoolBuilder.cs

`GravityTowerBuilder.BuildBottomHazard()`'daki "dip tehlikesi" deseninin
bağımsız, tekrar kullanılabilir havuz versiyonu. Parlayan (HDR emissive,
`glowMult` ile Bloom eşiği üstü) collider'sız bir asit yüzeyi (içine
düşülebilir) + üstünde durulabilir collider'lı kenar çerçevesi + `bubbleCount`
kadar yarı-batık küre "kabarcık" (`System.Random(seed)`) + isteğe bağlı
alçak kızıl ışıklar (`castLight`) + yüzeyin `killHeight` kadar üstünü kaplayan
bir `KillVolume` (düşen oyuncu son checkpoint'te canlanır — proje standardı)
üretiyor.

### 17.10 Aydınlatma Araçları — AutoLightRig.cs ve LightGrid.cs

**`AutoLightRig.cs`** — bu envanterdeki en dikkat çekici implementasyon
detayına sahip araç: sahneyi **ışın taramasıyla analiz ederek** ışık
yerleştiriyor, geometri hakkında hiçbir varsayımda bulunmuyor (kat/halka/
sahanlık farketmiyor). `Build()` (`"Işıkları Kur (Analiz)"`) önce
`ComputeBounds()` ile hedef alanın (boşsa `roomRoot`, doluysa tüm sahne)
toplam collider bounds'unu çıkarıyor, sonra `sampleStep` aralıklı bir X-Z
grid üzerinde her noktadan yukarıdan aşağı `Physics.RaycastAll` atıyor.
Çarpılan yüzeyler `OrderByDescending(h => h.point.y)` ile yukarıdan aşağı
sıralanıp taranıyor: ismi `"Tavan"` içeren en üstteki yüzey tavan olarak
işaretleniyor (`ceilY`), `normal.y >= 0.5` olan ilk yüzey zemin kabul
ediliyor (`floorY`) — düşey duvar/eğik yüzeyler bu eşiği geçemediği için
atlanıyor. `headroom = ceilY - floorY` 1.6 m'den azsa (sıkışık boşluk) ışık
konmuyor; ışık `heightAboveFloor` ile `headroom - 0.6f`'nin küçüğü kadar
zeminden yukarı yerleştiriliyor. Aynı zeminde çok yakın ışık varsa
(`< lightSpacing`, XZ mesafesi) atlanıyor, ama **Y farkı 2 m'den fazlaysa**
farklı bir kat sayılıp yine de ışık konuyor — böylece üst üste binen katlar
birbirini "ışık dolu" saymıyor. Son bir `Physics.CheckSphere` ile ışığın
katı geometri içine denk gelmediği doğrulanıyor. Geometriye hiç
dokunmuyor, sadece kendi ışık child'larını yönetiyor.

**`LightGrid.cs`** — `AutoLightRig`'in analiz-tabanlı yaklaşımının aksine,
tamamen **manuel/geometrik** üç yerleşim modu sunan basit araç: `Layout`
enum'u (`KareIzgara`, `DaireselTavan`, `DuvarHalkasi`) ile kare grid, artan
yarıçaplı dairesel halkalar, ya da çevre duvarına monte edilmiş tek halka
(`wallInset` ile duvardan içeri mesafe — alçak `lightHeight` ile duvar
lambası hissi) arasından seçim yapılıyor. `RoomBuilder`'ın gömülü tavan
ışığından farkı, mevcut bir odaya **sonradan** ışık eklemek için
tasarlanmış olması — geometriye hiç dokunmuyor.

### 17.11 SceneDresser.cs ve SurfacePalette.cs

**`SceneDresser.cs`** — kendi yorumunda açıkça **"v2"** olarak
işaretlenmiş bir yeniden yazım: *"v1'deki dünya-uzayı kutu + raycast
kaldırıldı — konumlandırma hatasına çok açıktı"*. v2, builder objesinin
sahnedeki konumundan tamamen bağımsız: sahnedeki **isimli** yüzeylerin
kendi `Renderer.bounds`'ları kullanılıyor. Beş `[ContextMenu]` komutu var:
(1) **"Materyalleri Uygula"** — isim eşleşmesiyle (`duvar/wall`→`wallMat`,
`zemin/floor`→`floorMat`, `tavan/ceiling`→`ceilingMat`,
`basamak/merdiven`→`stairMat`, `kolon/raf/kopru/platform`→`metalMat`)
`SurfacePalette` materyali atıyor; `onlyReplaceDefault` açıkken sadece hâlâ
varsayılan/builtin materyalli yüzeylere dokunuyor (elle özelleştirilmiş
materyaller korunuyor), `skipKeywords[]` (panel/isik/glow/barrier/logo vb.)
ile emissive/ışık/UI parçaları hariç tutuluyor, hepsi `Undo.RecordObject`
ile Ctrl+Z'lenebilir. (2) **"Mevcut Işıkları Renklendir + Atmosfer"** — ışık
OLUŞTURMUYOR, var olan `Light`'ları paletin rengine/lümenine çeviriyor ve
`SerializedObject` üzerinden `SceneVolumeSetup`'ın `ambientColor`/
`ambientIntensity`/`fogMeanFreePath` alanlarına doğrudan yazıyor. (3)
**"Tavan Işığı Diz"** — `AutoLightRig`'e benzer bir amaç ama farklı yöntem:
raycast değil, ismi `"tavan"` içeren her `Renderer`'ın bounds'ını tarayıp
altına grid ışık+emissive panel diziyor (`minCeilingArea` altındaki
kırıntı parçalar atlanıyor). (4) **"Prop Serp"** — ismi `"zemin"` içeren
yüzeylerin üstüne alan başına yoğunluk-ölçekli (`propDensity`,
`maxPropsPerFloor`) rastgele prop (kasa/varil/boru/moloz/kablo) serpiyor.
(5) **"SEÇİLİ Objelere Zorla Uygula"** — güvenlik kısıtlarını (`onlyReplaceDefault`,
skip listesi) tamamen es geçen bir kaçış kapısı, sadece Hierarchy'de seçili
objelere uygulanıyor.

**`SurfacePalette.cs`** — `SceneDresser`'ın okuduğu `[CreateAssetMenu]`
ScriptableObject (`Create → Bloodrush → Yüzey Paleti`). Bir bölümün TÜM
görünümünü tek asset'te topluyor: 5 yüzey materyali (duvar/zemin/tavan/
merdiven/metal), atmosfer ayarları (`ambientColor`, `ambientIntensity`,
`fogMeanFreePath` — küçük değer = daha yoğun sis) ve ışık rengi/lümeni.
Kod yorumu tasarım niyetini açıklıyor: bölüm başına bir palet
(`PaletteCH1` temiz kurumsal → `PaletteCH2` endüstriyel → `PaletteCH3`
kirli/paslı) oluşturularak "temiz cephe → altındaki dehşet" anlatı yayının
görsel karşılığı da kademeli olarak değişsin diye.

### 17.12 PlayerStartPoint.cs

En küçük ama en yaygın kullanılan parça — `ArenaBuilder`, `IndustrialHallBuilder`,
`PuzzleRoomBuilder`, `GravityRoomBuilder`, `GravityTowerBuilder` dahil hemen
her oda builder'ının ürettiği `"PlayerStart"` child'ına ekleniyor. `Start()`'ta
`"Player"` tag'li objeyi bulup, `PlayerMovement` varsa `Teleport(pos, rot)`
(CharacterController'ı güvenli ışınlayan API — bkz. §5) üzerinden, yoksa
doğrudan `transform.SetPositionAndRotation` ile o noktaya taşıyor. Sahnedeki
Player objesinin kendi konumundan bağımsız, taşınabilir bir başlangıç
noktası sağlıyor; Editor'da cyan bir gizmo küre + bakış yönü oku çiziyor.

---

## 18. Ek Düşman Tipleri ve FX Yardımcıları

Konum: `Assets/Scripts/Enemy/` (ek düşman tipleri ve ölüm/teşhis yardımcıları) ve `Assets/Scripts/FX/` (prosedürel görsel/kamera efektleri), artı `Assets/Scripts/Player/PlayerPickup.cs`.

### EnemyExplosive.cs — Şişkin/Patlayıcı tip
`STORY_DESIGN.md` Bölüm 8 Tip 4'ün karşılığı; kodun kendi başındaki yorum da doğrudan bu bölüme referans veriyor. **PDF'te (15 Temmuz) yer almıyor** — özetten sonra eklenmiş. `BossAI`/`ExperimentBossAI` gibi ayrı bir AI değil: mevcut bir `EnemyAI` + `Health` düşman prefabına eklenen bağımsız, `[RequireComponent(typeof(Health))]` bir bileşen; `Behavior` enum'una üçüncü dal olarak değil, kompozisyonla ekleniyor.

- **İki tetikleyici**: oyuncu `proximityRadius` (vars. 3.5 m) içine girince `StartPriming()` ile `fuseTime` (1 sn) geri sayımı başlıyor (`fuseIndicator` + `primeClip` sesi — kaçış penceresi). Alternatif olarak `Health.onDeath`'e bağlı: fitilin dolmasını beklemeden öldürülürse de patlıyor.
- **`Detonate()`**: `explosionVFX` + `blastClip` + `CameraShake.Shake(0.18f, 0.22f)`, ardından `Physics.OverlapSphere(blastRadius)` içindeki her `Health`'e mesafeyle lerp'lenen hasar (merkezde `blastDamage`, kenarda %25'i). `detonated` flag'iyle çift tetiklemeye karşı korunuyor; kendi `Health`'ini `TakeDamage(float.MaxValue)` ile bitiriyor ki `EnemyAI`'ın normal ölüm akışı (drop/VFX) tam bir kez çalışsın.
- **Opsiyonel `HazardZone`**: `leaveHazardZone=true` ise patlama noktasında `HazardZone.Init(...)` ile zamanlı zehirli/asit alanı bırakıyor — bu, `ExperimentBossAI`'nin faz-2 slam saldırısında kullandığı **aynı** `HazardZone.cs`'in yeniden kullanımı (bkz. §6).
- **Durum**: `STORY_DESIGN.md`'ye göre kod tarafı tamam, görsel yok; `Enemy2.prefab` (büyük/tanky mesh) placeholder olarak öneriliyor.

### DirectionalArmor.cs — yönlü zırh (artık atıl)
Tek metotlu minik yardımcı: `Multiplier(shotDir)`, `transform.forward` ile atışın ters yönü arasındaki açıyı (`Vector3.Angle`) `frontAngle` (vars. 70°) ile karşılaştırıp önden gelen atışlara `frontMult` (vars. 0.1 → %90 azaltma) uyguluyor.

`docs/BLOODRUSH_Gelistirme_Ozeti.pdf`'in 3.1/4.3 bölümleri bunun `PlayerShoot` hasarına "otomatik uygulandığını" anlatıyor, ama kod artık değişmiş — hasar hesabı §7'de belgelenen `PlayerFirearm.FirePellet()`'e taşınmış ve orada `DirectionalArmor.Multiplier()` hiç çağrılmıyor. Kodun kendi yorumu bunu itiraf ediyor: *"Yönlü zırh (önden az hasar / arkadan tam) kaldırıldı — zırhlı düşman artık her yönden tam hasar alır. `DirectionalArmor` component'i `ArmoredHazard` prefabında atıl duruyor; istenirse elle silinebilir."* Script hâlâ derleniyor ve prefab üzerinde duruyor ama fiilen hiçbir hasar hesabını etkilemiyor — **ölü kod** (bkz. §23).

### DeathEffect.cs / DeathEffectVisual.cs
`DeathEffect`, `Health.onDeath`'e abone olup `effectPrefab`'ı ölüm noktasında `Instantiate` eden jenerik tek satırlık kanca (`EnemyExplosive` gibi özel akışı olan tipler kendi patlama VFX'ini ayrıca yönetiyor, bu bileşene ihtiyaç duymuyor). `DeathEffectVisual`, aynı prefab'ın parçacık materyalini **çalışma zamanında** düzelten bir yama: `DeadMaterial` texture'suz/opak olduğu için parçacıklar kare görünüyordu; `Awake()`'te `ParticleSystemRenderer.material`'i `SoftDotVFX.CreateMaterial(tint)` ile üretilen yumuşak-kenarlı/saydam bir materyale değiştiriyor.

### EnemyDebugOverlay.cs — F3 canlı teşhis paneli
`[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` ile kendini sahneye/prefaba dokunmadan `DontDestroyOnLoad` bir obje olarak kuruyor. Görünürlük **tamamen `OnGUI`/IMGUI** ile çiziliyor — gerekçe kod yorumunda açık: hangi Input System aktif olursa olsun (`ENABLE_INPUT_SYSTEM`/`ENABLE_LEGACY_INPUT_MANAGER` sembollerinden hangisi tanımlıysa o yol derlenip çalışıyor) F3'e bağlı kalabilsin diye. Açıkken 0.5 sn'de bir `FindObjectsByType<EnemyAI>()` ile sahnedeki tüm düşmanları tarıyor; sol-üst panelde her biri için state/`OnNavMesh`/`NavMeshPathStatus` (renk kodlu: yeşil tam yol, sarı kısmi — ada şüphesi, kırmızı geçersiz)/hedef koordinatı/hız/anim hızı/NavMesh kurtarma sayacını basıyor, ayrıca her düşmanın üstünde ekran-uzayı dünya etiketi çiziyor.

### FX yardımcı script'leri (`Assets/Scripts/FX/`)
Küçük, tek-iş prosedürel yardımcılar — hiçbiri asset/prefab gerektirmiyor:
- **`FPSCounter.cs`** — 0.5 sn'lik pencerede ortalama FPS hesaplayıp TMP etikete yazıyor, eşiğe göre yeşil/sarı/kırmızı renklendiriyor.
- **`HitStop.cs`** — singleton, `Time.timeScale`'i kısa süreliğine düşürüp (`scale`, vars. 0.05) `WaitForSecondsRealtime` ile normale döndüren klasik "hit-stop" coroutine'i sağlıyor. **Kod tabanında hiçbir çağıran yok** — API hazır ama şu an hiçbir vuruş/parry/patlama tarafından tetiklenmiyor; kullanılmayı bekleyen dormant bir yardımcı (bkz. §23).
- **`MuzzleFlash.cs`** — asset gerektirmeden `Spawn(pos, parent)` ile HDR turuncu emissive küre üretip hızla küçültüp yok ediyor; tek paylaşılan `sharedMat` ile materyal sızıntısını önlüyor. `EnemyRangedAttack` tarafından her menzilli düşman atışında çağrılıyor.
- **`SoftDotVFX.cs`** — çalışma zamanında radyal alfa gradyanlı yumuşak daire dokusu üreten static yardımcı; `DeathEffectVisual` bu paylaşılan üretici üzerinden çalışıyor.
- **`FaceCamera.cs`** — `LateUpdate`'te `transform.forward`'ı kameraya kilitleyen minimal billboard bileşeni; kodda hiçbir script tarafından `AddComponent` edilmiyor, yalnızca Editor'de manuel eklenmek üzere hazır genel amaçlı bir yardımcı (bkz. §23).
- **`SpeedEffect.cs`** — oyuncu hızına bağlı FOV artışı + `ChromaticAberration` yoğunluğu (kod ile oluşturulan kendi global `Volume`'u üzerinden); `SpeedEffect.FovOverride` static alanı `SettingsApplier.ApplyDisplay()` tarafından ayarlardaki FOV kaydırıcısıyla besleniyor, hız etkisi bu taban değerin üstüne ekleniyor.

### PlayerPickup.cs — jenerik toplama (orijinal §5'ten eksik kalmıştı)
`Assets/Scripts/Player/`. `Update()`'te `OverlapSphere(1.5f)` ile her karede yakındaki collider'ları tarayan otomatik toplayıcı (tetiklenen bir trigger değil, sürekli poll). Sırasıyla `AmmoPickup` (sadece `PlayerLoadout.AnyFirearm` iken — silahsızken mermi toplamanın anlamı yok), `WeaponPickup` (kısıt fark etmeksizin her zaman serbest — yerden silah almak asla engellenmiyor) ve `HealthPickup`'ı arıyor; her biri `GetComponent`/`GetComponentInParent`/`GetComponentInChildren` üçlemesiyle esnek hiyerarşilerde bulunuyor. `PlayerShoot` referansı root'ta veya herhangi bir child'da aranıyor, bulunamazsa sahne genelinde `FindFirstObjectByType`'a düşüyor.

---

## 19. UI Ek Bileşenleri

Konum: `Assets/Scripts/UI/` ve `Assets/Scripts/UI/Menu/` — §9'da (UI/Menü Sistemi) detaylandırılmamış tamamlayıcı bileşenler.

### GameHUD.cs — PDF'teki BEDEN/YÜKLEME barları DOĞRULANDI: kaldırılmış
Script'in başındaki yorum bunu birebir teyit ediyor: *"BEDEN (StimulantSystem collapse) ve YÜKLEME (upload) barları kaldırıldı — HUD artık sadece CH3'ün 'VERİ x/y' terminal sayacını gösteriyor."* PDF'in bug-fix tablosunda bahsedilen `VerticalLayoutGroup`/`HorizontalLayoutGroup` tabanlı satır düzeni hâlâ duruyor ama içerik tamamen değişmiş:

- **CAN satırı** — oyuncunun `Health`'ine bağlı, anchor-tabanlı dolgu bar (`healthFillRT.anchorMax.x` cana göre değişiyor) + üstte sayı (`Mathf.CeilToInt`). PDF'te hiç bahsi geçmeyen **yeni** bir eklenti.
- **VERİ satırı** — sadece `WaveDirector.Instance` varsa (CH3 gibi dalga tabanlı sahnelerde) görünür oluyor; `"{TerminalsDone}/{TotalTerminals}"` yazıyor, aktif bir `DataTerminal` varsa `"— %ilerleme"` ekleniyor. **Bar yok, sadece metin** — PDF'in "BEDEN barı aşağı, YÜKLEME barı yukarı" ters-saat metaforuyla tarif ettiği görsel artık ekranda yok.
- API tarafı korunmuş: `AddUploadProgress()`, `UploadProgress` static property ve `ResetProgress()` hâlâ var ve `DataTerminal`/`UploadTerminal`/`GameFlow`/`MainMenuManager` tarafından yazılmaya devam ediyor — sadece görsel karşılığı kaldırılmış. Yani PDF'in "iki ters saat" mekanik teması kod düzeyinde hâlâ işliyor, ekranda artık görünmüyor.

### CrosshairHUD.cs
PDF'in "isabet işareti + kanca cooldown çubuğu" tarifiyle birebir örtüşüyor, fark yok. `Awake()`'te kendi `Canvas`'ını kuruyor; klasik + şeklinde dört çizgi + opsiyonel merkez nokta (`ApplyStyle(type, color)` ile `SettingsApplier.ApplyGameplay()`'den 4 nişangah tipi arasında seçim), ayrıca gizli duran kırmızı X `hitLines` (`ShowHitMarker()` → `hitDuration` sonra otomatik kapanan coroutine). `BuildHookBar()`, nişangahın ~16px altına `Image.Type.Filled` bir dolgu çubuğu yerleştiriyor; `GrapplingHook` her karede `SetHookCooldown(normalized)` çağırıyor — 1.0 iken çubuk tamamen gizli, altındayken doluyor.

### BossHealthUI.cs
`Health` + isim ile sürülen statik `ShowBoss()`/`HideBoss()` çifti; sahnede hiçbir şeye ihtiyaç duymadan kendi `Canvas`'ını (üst-orta, geniş kırmızı dolgu bar + üstte isim) `Awake()`'te kuruyor. Hem `BossAI` hem `ExperimentBossAI` aynı statik API'yi çağırıyor — §6'daki "iki bağımsız boss implementasyonu" burada tek bir paylaşılan UI'da birleşiyor. `Update()`'te can 0'a inince kendini otomatik gizliyor.

### ButtonHoverEffect.cs
`Menu/MenuButton.cs`'in (yeni `MenuTheme`/vurgu sistemi) daha basit, atası sayılabilecek bir hover efekti: `IPointerEnter/Exit/Down/Up` ile hedef `fillImage`'ın alfasını `Lerp`liyor, sabit `Color fillColor` public alanı var (tema asset'i yok). Tek kullanıcıları **`MainMenuManager.cs`** ve onun eşleniği **`PauseMenu.cs`** — yani bu, `MainMenuController`/`MenuButton`/`MenuTheme` üçlüsünün yerini aldığı eski/legacy menü hover sisteminin parçası.

### MainMenuManager.cs — eski menü, hâlâ tam işlevsel
§9'da not edildiği gibi `MainMenuController` sahnede bulunursa bunu devre dışı bırakıyor, ama script'in kendisi silinmemiş, eksiksiz bağımsız bir menü: kendi `Canvas`'ını (`BuildUI()`), CRT-vari neon-kırmızı tarama çizgilerini (`BuildScanLines`, 160 adet), giriş animasyonunu (`AnimateIn` — başlık + butonlar sırayla fade+kaydırma) kod ile inşa ediyor. PDF'in iddiası doğrulandı: `StartGameRoutine()` içinde `GameHUD.ResetProgress()` ilk satırda çağrılıyor ("yeni oyun = upload sıfır" yorumuyla), ardından müzik fade-out + `SceneManager.LoadSceneAsync` + siyah overlay + `SceneFadeIn.CarryOverlay` ile kesintisiz sahne geçişi.

**Yeni bulunan ek fark**: bu script kendi ses seviyesi ve hassasiyet ayarlarını `AudioListener.volume` / `PlayerPrefs.GetFloat("MasterVolume"/"Sensitivity")` ile **doğrudan** okuyup yazıyor — §10'da anlatılan asıl yapılandırılmış kayıt sistemi `SettingsStore`'un (JSON dosyası) **tamamen dışında**. Proje şu an iki paralel ayar kalıcılığına sahip: yeni `SettingsStore` JSON sistemi ve bu eski menünün kullandığı ham `PlayerPrefs` anahtarları; `MainMenuController` aktifken bu ikinci yol devre dışı kalıyor ama kod hâlâ orada duruyor (bkz. §23).

### IntroTextSequence.cs
Ch1 açılışındaki daktilo-tarzı itiraf metnini (`Lines[]` — sabit Türkçe metin dizisi) karakter karakter yazan sekans; `Start()`'ta `PlayerMovement`'ı devre dışı bırakıp bitince tekrar açıyor, `Input.anyKeyDown` ile atlanabiliyor, `OnFinished` static event'i tetikliyor. `Flow/IntroSalonController.cs` tarafından kullanılıyor (bkz. §16).

### Menu/MenuButton.cs, Menu/UISounds.cs
`MenuButton`, kutu yok/hover'da altın çubuk + hafif dolgu görsel dilini fare/klavye/gamepad için **aynı** durum makinesiyle (`hovered`, `selected`, `activeTab`) sürüyor; `ISelectHandler` sayesinde fare hover'ı `EventSystem` seçimini de taşıyor (gamepad↔fare geçişinde odak zıplamıyor). `UISounds`, klipleri boş bırakılabilen (ses eklenene kadar sessizce hiçbir şey yapmayan) merkezi hover/click/back ses çalıcı; `AudioRouting.Ui` mixer grubuna bağlı ve `ignoreListenerPause=true` ile menüdeki `Time.timeScale=0` durumundan etkilenmiyor.

### Notification.cs
`BossHealthUI` ile aynı "kendi kendini kuran statik singleton" deseni; `Show(message, duration)` ekranın üst kısmında altın/sarı, otomatik solan bir toast gösteriyor. Beklenenin ötesinde geniş bir kullanım alanı var: `WeaponPickup`, `Flashlight` yanında özellikle **§14 (Karanlık Sekans)** ve **§15 (Aşırı Yük)** gibi bulmaca alt sistemlerinin tüm geri bildirim katmanını tek başına taşıyor.

### UIIcons.cs, UITheme.cs
`UIIcons`, `Resources/Icons/` altındaki (game-icons.net kaynaklı) sprite'ları tembel-yükleme (`??=`) ile önbellekleyen statik sınıf; sprite bulunamazsa tüketen taraf (`DialogueUI`/`BookUI`) o alanı sessizce gizliyor. `UITheme`, `[CreateAssetMenu]` ile üretilen ve `Resources/UITheme.asset`'ten `Load()` edilen merkezi renk/font/içerik `ScriptableObject`'i; asset yoksa `null` dönüyor ve her tüketici kendi sabit varsayılanına düşüyor. Ayrıca prosedürel `VignetteSprite()`/`DotSprite()` texture üreticilerini merkezi olarak barındırıyor.

---

## 20. Editor Araçları

### 20.1 MaskMapPacker.cs

Konum: `Assets/Scripts/Editor/MaskMapPacker.cs`. `Window/Bloodrush/Mask
Map Paketleyici` menüsünden açılan bir `EditorWindow`. Amacı: HDRP/Lit'in
"Mask Map" alanı tek bir RGBA texture bekliyor (R=Metallic, G=AO, B=boş,
A=Smoothness), ama ambientCG/Poliigon tarzı indirilen PBR paketleri bu
kanalları hep ayrı dosyalar (Metallic.png, AO.png, Roughness.png) olarak
veriyor — bu araç onları birleştirip HDRP'nin beklediği formatta tek bir PNG
üretiyor. `OnGUI()` üç opsiyonel `Texture2D` alanı sunuyor; herhangi biri
boşsa sabit bir fallback değeri (`metallicFallback`, `aoFallback`,
`smoothnessFallback`) kullanılıyor. `roughnessTex` verilmişse
`invertRoughness` (varsayılan açık) roughness'ı `1 - r` ile smoothness'a
çeviriyor. Paketlemeden önce iki doğrulama yapılıyor: `AllReadable()`
(kaynak texture'ların Import Settings'inde Read/Write Enabled açık olması
şart — kapalıysa hangi texture olduğu isimle gösteriliyor) ve `SameSize()`
(hepsi aynı çözünürlükte olmalı, uyuşmazlık mesajında hangi texture'ın
hangi boyutta olduğu raporlanıyor). `PackAndSave()` piksel piksel
`Color(m, ao, 0f, s)` birleştirip `TextureFormat.RGBA32` bir `Texture2D`'ye
yazıyor (`linear: true` — mask map renk verisi değil), `EncodeToPNG()` ile
diske yazıp `AssetDatabase.ImportAsset` sonrası `TextureImporter` üzerinden
`sRGBTexture = false` + `textureType = Default` ayarlıyor — kod yorumu
sebebini açıklıyor: sRGB açık kalırsa HDRP mask map kanallarını gamma
düzeltmesine sokup bozardı. Sonda üretilen asset otomatik seçilip
`EditorGUIUtility.PingObject` ile Project penceresinde vurgulanıyor.

---

## 21. Third-Party Varlıklar

`Assets/ThirdParty/` altında üç paket:

- **`CasualHit/`** — vuruş efekti VFX paketi (materyaller, bir
  Shader Graph, prefab'lar). Kendi `Casual_Hit.cs` scripti sadece
  generic bir kamera-sarsıntısı utility'si.
  > **Düzeltme (2026-09-15)**: bu bölüm ilk yazıldığında hit-effect
  > prefab'larının (`Hit_1`–`Hit_4`) "hiçbir yerde referans edilmediği,
  > kullanılmadığı" not edilmişti — bu **yanlıştı**. GUID bazlı referans
  > taramasıyla `Hit_4_Red.prefab`'ın `Assets/Prefabs/Characters/Enemy2.prefab`
  > içine nested prefab olarak gömülü olduğu, `ArmoredHazard.prefab`'ın da
  > benzer şekilde bir `Hit_` varyantı kullandığı doğrulandı — yani bu
  > paket **kullanılıyor**. Bu hata, bir refactor planında bu paketin
  > yanlışlıkla silinmesinin önerilmesine yol açmıştı (düzeltildi, bkz.
  > `docs/BLOODRUSH_YENIDEN_YAPILANDIRMA_PLANI.md`); ders: Unity
  > projelerinde "kullanılmıyor" iddiası her zaman GUID/prefab referans
  > taramasıyla doğrulanmalı, salt script grep'i yeterli değil.
- **`FPSHandsWeaponAnimation/`** — FPS kol/silah animasyon paketi
  (Coppercube export, glock modeli/dokuları, artı 24,2 MB'lık bir
  Coppercube `.exe`'si ve 19,3 MB'lık bir `.ccb` proje dosyası). **Hiç
  script içermiyor** ve custom kodla sarmalanmamış; GUID taramasıyla
  proje genelinde **sıfır** dış referans bulundu — gerçekten kullanılmıyor
  (bkz. §7 ve `docs/TEMIZLIK_VE_YAPI_PLANI.md` Faz 1, silinmesi planlı).
- **`GabrielAguiarProductions/FreeQuickEffectsVol1/`** — ücretsiz VFX
  paketi (vendor PDF dokümantasyonuyla birlikte). `vfx_Explosion_01.prefab`'ı
  hem kendi demo sahnesinden hem de **doğrudan `Assets/Scenes/CH2.unity`**
  sahnesinden referans ediliyor — proje `Prefabs/Weapons/` altında bu
  prefab'ın kendi kopyasını da tutuyor (`Grenade.prefab` üzerinden
  kullanılan), yani CH2 sahnesi şu an proje-içi kopya yerine vendor
  paketinin orijinaline kırılgan bir referans taşıyor (bkz.
  `docs/TEMIZLIK_VE_YAPI_PLANI.md` §4.2). Ayrıca `Assets/GabrielAguiarProductions/`
  adında, kökte duran, tamamen **boş** bir yetim klasör/meta çifti de var
  (gerçek içerikle karışmasın — o, `ThirdParty/` altındaki asıl paketten
  ayrı, silinmesi planlı bir kalıntı).

Ayrıca `Assets/ThirdParty/CasualHit/Shaders/Particle.shadergraph`,
projenin tek Shader Graph asset'i (üçüncü parti, çekirdek render
pipeline'dan bağımsız).

---

## 22. Sürüm Kontrolü, Build & Test Altyapısı

- **Git**: standart Unity `.gitignore` (GitHub şablonu).
- **Git LFS**: `.gitattributes` içinde `*.wav, *.mp4, *.png, *.psd,
  *.fbx` için yapılandırılmış.
- **CI/CD**: yok — `.github/workflows` veya herhangi bir `.yml`/`.yaml`
  build config dosyası bulunamadı.
- **Test framework**: `com.unity.test-framework@1.1.33` paket olarak
  kurulu, ama proje içinde `Tests` klasörü veya hiçbir test dosyası
  yok — **hiç kullanılmıyor**.
- **Lokalizasyon**: Unity Localization paketi kurulu değil, `Assets/`
  içinde localization dosyası da yok. Türkçe, yalnızca kod
  yorumlarında, değişken/asset isimlerinde (`Assets/Veri`, `Kitap1/2`)
  ve UI metinlerinin doğrudan Türkçe hardcode edilmesiyle var —
  biçimsel bir çoklu-dil sistemi yok (`GameSettings.language` alanı
  var ama `SettingsApplier`'da "HENÜZ ETKİN DEĞİL" olarak işaretli).

---

## 23. Gözlemler & Boşluklar

Bunlar yargı değil, kod tabanında doğrudan gözlemlenen ve gelecekteki
kararları etkileyebilecek noktalar:

1. **Netcode for GameObjects tamamen atıl.** Paket kurulu, transport
   bağımlılığı çekilmiş, ama hiçbir script `NetworkBehaviour` türetmiyor,
   hiçbir sahnede `NetworkManager` yok, varsayılan network prefab
   listesi boş. Eğer multiplayer planlanmıyorsa bu, gereksiz bir
   bağımlılık (derleme süresi, paket yüzeyi); planlanıyorsa henüz
   hiç başlanmamış demektir — ikisi de netleştirilmeye değer.
2. **Biçimsel bir "oyun kaydı" (save) sistemi yok.** Tek yapılandırılmış
   persistans katmanı `SettingsStore` — sadece ayarlar için. Oyun
   ilerlemesi `lastChapter` string'i dışında görünürde saklanmıyor;
   checkpoint/ilerleme kalıcılığı nasıl çalışıyor (varsa) ayrıca
   incelenmeli.
3. **Test altyapısı kurulu ama sıfır kullanım.** `test-framework`
   paketinin varlığı bir niyeti gösteriyor olabilir, ama şu an hiçbir
   sistem (özellikle karmaşık state machine'ler — `EnemyAI`, `BossAI`,
   `PlayerMovement`) otomatik test kapsamına sahip değil.
4. **CI/CD yok.** Build/test doğrulaması tamamen manuel; bir hata
   ancak Editor'de veya build sonrası elle fark edilir.
5. ~~İki kullanılmayan third-party paket~~ **Düzeltildi (2026-09-15)**:
   `CasualHit` aslında kullanılıyor (bkz. §21) — sadece
   `FPSHandsWeaponAnimation` gerçekten atıl, silinmesi planlı.
6. **Legacy `com.unity.postprocessing` paketi dormant.** HDRP zaten
   kendi native Volume post-processing'ini kullanıyor; eski paket
   koda hiç referans edilmiyor — kaldırılabilir bir bağımlılık olabilir.
7. **Girdi mimarisinde ikilik.** Yeni Input System kurulu ve sadece
   menü navigasyonu/tuş-yeniden-atama algısı için kullanılıyor; tüm
   gerçek oynanış girdisi legacy `Input` sınıfı + özel `KeyBindings`
   katmanı üzerinden. Bu işlevsel bir sorun değil ama iki paralel
   girdi yolu tutmanın bilinçli bir tercih mi yoksa kademeli bir geçişin
   ortası mı olduğu netleştirilmeye değer.
8. **`EnemyAI`/`BossAI`/`ExperimentBossAI` arasında ortak temel sınıf
   yok.** Kod tekrarı bilinçli olarak bazı yerlerde kabul edilmiş
   (`ExperimentBossAI` kendi `HasLineOfSight()`'ını `EnemyVision`'ı
   kullanmak yerine kopyalıyor) — üç sistem büyüdükçe bu, senkronize
   tutulması gereken bir bakım yükü haline gelebilir.
9. **`Arena/WaveManager.cs` ve `Flow/WaveDirector.cs` adında birbirine
   benzeyen ama tamamen ayrı iki dalga/spawn sistemi var.** İsimlendirme
   benzerliği kod tabanına yeni katılan biri için kafa karıştırıcı
   olabilir; hangisinin hangi bölümde/senaryoda kullanıldığı açıkça
   belgelenmezse yanlış sistemde değişiklik yapma riski var.
10. **Lokalizasyon altyapısı yok ama `language` ayarı UI'da mevcut.**
    Ayar arayüzünde bir dil seçeneği duruyor ama işlevsel değil
    ("HENÜZ ETKİN DEĞİL") — kullanıcıya yanıltıcı bir vaat sunuyor
    olabilir, en azından gizlenmesi düşünülebilir.
11. **`DirectionalArmor.cs` ölü kod.** Önceden `PlayerShoot`'un hasar
    hesabına bağlıydı; kod artık `PlayerFirearm.FirePellet()`'e taşınmış
    ve orada bu sınıf hiç çağrılmıyor. Script hâlâ `ArmoredHazard.prefab`
    üzerinde duruyor ama fiilen hasar hesabını etkilemiyor — kodun kendi
    yorumu bunu "istenirse elle silinebilir" diyerek itiraf ediyor
    (bkz. §18). Temizlik planına eklenebilecek küçük, düşük riskli bir
    madde.
12. **İki dormant (kullanılmayan) FX yardımcısı**: `HitStop.cs` (hit-stop/
    zaman yavaşlatma API'si hazır ama hiçbir vuruş/parry/patlama onu
    çağırmıyor) ve `FaceCamera.cs` (billboard bileşeni, kodda hiçbir yerden
    `AddComponent` edilmiyor — sadece Editor'de manuel eklenmek üzere
    duruyor). İkisi de "gereksiz dosya" değil, kullanılmayı bekleyen hazır
    altyapı; silinmemeli ama kullanılmadıkları bilinmeli (bkz. §18).
13. **`MainMenuManager.cs`, `SettingsStore`'u atlayan ikinci bir ayar
    kalıcılığı taşıyor.** Kendi ses seviyesi/hassasiyet değerlerini
    doğrudan `PlayerPrefs`'ten (`"MasterVolume"`, `"Sensitivity"`)
    okuyup yazıyor — §10'da anlatılan yapılandırılmış `SettingsStore`
    (JSON) sisteminin tamamen dışında. `MainMenuController` sahnede
    aktifken bu eski menü (ve onun PlayerPrefs yolu) devre dışı kalıyor,
    ama kod hâlâ orada duruyor ve iki paralel kalıcılık yolu bir arada
    yaşıyor (bkz. §19).
14. **İki paralel "sunum" mekanizması**: `PresentationScreen.cs`
    (slayt-metni, dünya-uzayı canvas) ve `PresentationSequence.cs`
    (gerçek `VideoPlayer` ile video oynatan, daha yeni sistem)
    birbirinden habersiz olarak bir arada var. Hangisinin hangi sahnede
    kullanıldığı netleştirilmezse, birini güncelleyip diğerinin hâlâ eski
    davranışta kalması riski var (bkz. §16).
15. **`PauseMenu.cs` (eski) ile `PauseMenuController.cs` (yeni) bir
    arada.** `PauseMenuController.DisableLegacy()` her sahne
    yüklemesinde eskisini otomatik kapatıyor — davranışsal olarak
    tamamen ölü ama `Player.prefab` üzerinde bileşen olarak hâlâ duruyor
    (bkz. `docs/TEMIZLIK_VE_YAPI_PLANI.md`, silinmesi planlı).
16. **`GameHUD`'un "BEDEN/YÜKLEME" görsel barları kaldırılmış, ama API'si
    hâlâ besleniyor.** `AddUploadProgress()`/`UploadProgress` gibi
    statikler `DataTerminal`/`UploadTerminal`/`GameFlow`/`MainMenuManager`
    tarafından hâlâ çağrılıyor, ama ekranda artık sadece CH3'e özel
    "VERİ x/y" sayacı görünüyor — oyunun "iki ters saat" (yükleniyor
    yukarı, çöküyor aşağı) mekanik teması koda gömülü kalmış ama
    görsel karşılığı büyük ölçüde sessizleşmiş (bkz. §19).

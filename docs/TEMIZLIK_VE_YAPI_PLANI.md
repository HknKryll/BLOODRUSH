# BLOODRUSH — Temizlik ve Klasör Yapısı Planı

> Bu doküman, `Assets/` altındaki **gereksiz dosyalar**, **klasör yapısı**
> ve **.gitignore** konularında yapılan tam kapsamlı incelemenin sonucudur.
> İnceleme; `docs/TECH_OVERVIEW.md`'deki mevcut bilgi + 4 paralel araştırma
> ajanının tüm `Assets/` ağacını (1.565 dosya) satır satır taraması + GUID
> bazlı referans kontrolleriyle yapıldı. Örnekleme değil, **kapsamlı**
> tarama — Prefabs/Materials klasörlerinin %100'ü, Textures'ın büyük
> kısmı GUID referans sayımıyla tek tek kontrol edildi.
>
> Bu bir **plan** dosyasıdır — hiçbir madde henüz uygulanmadı. Her faz
> için risk seviyesi, bağımlılık ve doğrulama adımı var. Uygulamaya
> geçmeden önce fazları teker teker onaylaman gerekiyor.
>
> Hazırlanma tarihi: 2026-09-15
>
> **İsimlendirme notu (2026-09-15)**: `BLOODRUSH_YENIDEN_YAPILANDIRMA_PLANI.md`
> da kendi bağımsız "Faz 1...Faz 7" sırasını kullanıyor — iki plan aynı
> faz isimlerini paylaşıyor ama tamamen farklı işler. Karışıklığı önlemek
> için bu belgedeki fazlardan bahsederken her zaman **"Temizlik planının
> Faz N'i"** denir, diğerindekiler için **"Refactor planının Faz N'i"**.

---

## 0. Bağlam

`BLOODRUSH_YENIDEN_YAPILANDIRMA_PLANI.md` incelemesi sırasında (bkz. o
inceleme) `Assets/ThirdParty/CasualHit/` klasörünün "kullanılmıyor"
sanılıp silinmek istendiğini ama aslında `Enemy2.prefab` ve
`ArmoredHazard.prefab` tarafından nested prefab olarak kullanıldığını
GUID taramasıyla bulmuştum. Bu, salt "script grep"in Unity projelerinde
yeterli olmadığını gösterdi — bu yüzden bu turda **her iddiayı GUID
bazlı referans taramasıyla doğruladım**, sadece dosya adına/script
grep'ine güvenmedim.

Ayrıca bu araştırma sırasında `docs/TECH_OVERVIEW.md`'nin
`Assets/Scripts/Flow/` altındaki ~60 dosyalık büyük bir katmanı (greybox
oda inşa araçları, "Karanlık Sekans", "Aşırı Yük" odası, GameFlow/
GameProgress, asansör kazası sekansı, puzzle sistemleri) hiç
kapsamadığı ortaya çıktı. Bu, bu planın kapsamı dışında ama ayrı bir
takip maddesi olarak §8'de not edildi — TECH_OVERVIEW.md'nin
genişletilmesi **onaylandı** (2026-09-15), ayrı bir iş turu olarak
yapılacak.

> **2026-09-15 güncellemesi**: Bu plandaki tüm açık kararlar kullanıcıyla
> netleştirildi ve ilgili bölümlere işlendi. Ayrıca: `docs/STORY_DESIGN.md`
> git geçmişinden kurtarılıp kaydedildi; `docs/BLOODRUSH_YENIDEN_YAPILANDIRMA_PLANI.md`
> ve `docs/BLOODRUSH_MIMARI_KURALLARI.md` artık repoda (önceki sürümler
> sadece sohbete yapıştırılmıştı). **Genel silme politikası değişti**:
> kullanıcı, "referanssız ama içerik değeri olan" öğelerin (legacy
> materyal/texture kümesi, WIP prefab'lar, ham `.obj`/`.mtl` export'ları)
> doğrudan silinmek yerine bir **yedek klasörüne** taşınmasını tercih
> ediyor — bkz. §5 ve §9.

---

## 1. Bulgular Özeti

| Kategori | Bulgu | Tahmini boyut |
|---|---|---|
| Duplicate içerik (12 grup) | Aynı byte'lara sahip dosyalar farklı klasörlerde | ~72,2 MB |
| Unity'nin tanımadığı format | .exe, .ccb, .blend, .tres, .usdc, .mtlx | ~48,4 MB |
| Referanssız (kullanılmayan) texture | Materials/Textures altında GUID taramasıyla doğrulandı | 46 dosya |
| Referanssız materyal | Legacy karakter shading seti | 11 dosya |
| Referanssız prefab | Muhtemelen WIP/gelecek içerik | 6 dosya |
| Tamamen boş klasör | Sadece `.meta`, içerik yok | 4 klasör |
| İsimlendirme sorunu | Türkçe karakter/boşluk/typo/case tutarsızlığı | ~25 öğe |
| .gitignore eksiği | `.claude/`, `.remember/` eksik | 2 satır |

**Toplam kazanılabilir alan: ~120 MB+** (duplicate + yabancı format,
legacy materyal/texture kümesi hariç — onun boyutu ayrıca ölçülmedi ama
muhtemelen birkaç MB daha eklerdi).

> **Ek bulgu (2026-09-15) — `Assets/Video/VERITAS_Sunum.mp4`**: Kullanıcı
> "sunumda fotoğraf mı oynuyor bilmiyorum" diye sordu. Kod incelemesi net
> cevap veriyor: `Flow/PresentationSequence.cs` bunu gerçek bir
> `VideoPlayer` + `VideoClip` bileşeniyle oynatıyor (fotoğraf/slayt değil,
> gerçek video) ve dosya **`Assets/Scenes/CH1.unity`'den aktif olarak
> referans ediliyor** — yani şu an canlı, kullanılan içerik. Bu bir
> "gereksiz dosya" maddesi değil. Kullanıcı bu videoyu muhtemelen
> değiştirecek (arkadaşı yapmıştı) ama bu bir içerik/tasarım kararı,
> temizlik planının kapsamına girmiyor.

> **Düzeltme (bu tablo yazıldıktan sonra GUID doğrulamasıyla bulundu)**:
> "12 duplicate grup"dan biri (`vfx_Explosion_01.prefab`, 945 KB) aslında
> gerçek bir duplicate değilmiş — her iki kopya da farklı yerlerden
> kullanılıyor (bkz. §4.2). Bu grubu "kazanılabilir alan"dan çıkarın;
> gerçek tekrarlanan içerik 11 grup + `Assets/Animation/` klasörü (yeni
> bulundu, aşağıda Faz 1'de) civarında.

---

## 2. Faz 1 — Kesin Güvenli Silmeler
**Risk: Sıfır.** Bunlar hem script hem GUID taramasıyla doğrulandı;
projenin hiçbir yerinden referans edilmiyorlar.

> ✅ **UYGULANDI (2026-09-15)**: Aşağıdaki tüm maddeler `git rm` ile
> silindi. `PauseMenu.cs` için önce `Assets/Prefabs/Characters/Player.prefab`
> içindeki bileşen referansı (GameObject `fileID 6435531861348867916`'nın
> `m_Component` listesinden `fileID 4538079662790876094`) elle YAML
> düzenlemesiyle temizlendi, GUID taramasıyla başka hiçbir yerde
> referans kalmadığı doğrulandı, sonra script silindi.
>
> ⚠️ **Bulunan ve düzeltilen hata**: Unity Editor'de compile hatası
> çıktı — `PauseMenuController.cs`'nin `DisableLegacy()` metodu
> `FindFirstObjectByType<PauseMenu>()` ile **kaynak kodda** (GUID/prefab
> değil, C# tip referansı) `PauseMenu` sınıfına referans veriyordu; bunu
> GUID taramasında yakalamadım çünkü kaynak kod derleme-zamanı
> referansları GUID sisteminden farklı bir katman. `DisableLegacy()`
> metodu ve çağrı yerleri tamamen kaldırıldı (zaten amacı artık var
> olmayan legacy bileşeni kapatmaktı), sınıf yorumu güncellendi.
> Proje genelinde `\bPauseMenu\b` taramasıyla başka kod referansı
> kalmadığı doğrulandı (sadece 2 açıklayıcı yorum satırı kaldı, derleme
> hatası vermez). **Ders**: bundan sonraki script silmelerinde GUID
> taraması yetmiyor — aynı zamanda `Assets/Scripts/` genelinde silinen
> sınıfın adına kod-seviyesinde (`\b<SınıfAdı>\b`) grep atılmalı.
>
> Henüz commit edilmedi — Unity Editor'de doğrulama (ESC ile duraklatma
> menüsü, sahne açılışları, konsolda başka hata olmadığı) **kullanıcı
> tarafından yapılacak**, ondan sonra commit'lenecek.

| Öğe | Yol | Not |
|---|---|---|
| Boş klasör | `Assets/GabrielAguiarProductions/` (+ `.meta`) | Gerçek içerik `ThirdParty/GabrielAguiarProductions/`'da (108 dosya, 20MB) zaten var |
| Boş klasör | `Assets/ThirdParty/FPSHandsWeaponAnimation/Dara/` | İçi boş |
| Boş klasör | `Assets/ThirdParty/FPSHandsWeaponAnimation/Img/` | İçi boş |
| Boş klasör | `Assets/ThirdParty/FPSHandsWeaponAnimation/Scripts/` | İçi boş |
| **Tüm `FPSHandsWeaponAnimation/` klasörü** | `Assets/ThirdParty/FPSHandsWeaponAnimation/` | Önceki GUID taramasında proje genelinde **sıfır** dış referans bulundu (ne script, ne prefab, ne materyal). İçinde 24,2 MB'lık bir **Windows .exe** (`FPS-with Animated Cycles-Coppercube6.5.1.exe`) ve 19,3 MB'lık bir Coppercube proje dosyası (`.ccb`) var. **Çakışma notu**: bu madde `BLOODRUSH_YENIDEN_YAPILANDIRMA_PLANI.md`'nin de kendi Faz 1'inde ayrıca listeleniyor (`DefaultNetworkPrefabs.asset` gibi düzgün devredilmemiş, kazara iki planda birden yer almış). Hangi plan önce uygulanırsa bu klasörü o siler; diğer plan bu adıma geldiğinde dosya zaten yok olacağı için sorunsuz atlanır — iki kez silme girişimi zarar vermez. |
| **Tüm `Assets/Animation/` klasörü** | `Assets/Animation/` (+ `.meta`) | İçindeki tek dosya (`Animation.controller`) GUID taramasıyla doğrulandı: hiçbir yerden referans edilmiyor. `anim/` klasörüyle isim çakışması da (bkz. §6.1) bunu gereksiz kılıyor. |
| Ölü prefab | `Assets/Prefabs/Environment/Door.prefab` (+ `.meta`) | GUID taramasıyla doğrulandı: referanssız. **Kök `Assets/door.prefab` (küçük harf) ile karıştırma** — o, `CH4.unity` tarafından kullanılan yaşayan kapı, o SİLİNMEYECEK (bkz. §5.2). |
| Ölü script (2026-09-15 doğrulandı) | `Assets/Scripts/UI/PauseMenu.cs` (+ `.meta`) | Kod yorumunda **açıkça belgelenmiş** bir legacy durum: `PauseMenuController.cs`'nin kendi yorumu şöyle diyor — *"eski PauseMenu, Player.prefab üzerinde bir bileşendi... PauseMenu bileşenini bulup kapatıyoruz ki ESC'e iki menü birden cevap vermesin."* `PauseMenuController.DisableLegacy()` her sahne yüklendiğinde `FindFirstObjectByType<PauseMenu>()` ile bu bileşeni bulup kapatıyor — yani her zaman devre dışı, davranışsal olarak tamamen ölü. **İki adımlı silme**: (1) `Assets/Prefabs/Characters/Player.prefab` üzerinden `PauseMenu` bileşenini Inspector'da kaldır, (2) `PauseMenu.cs`'i sil. |

**Doğrulama**: Silme sonrası proje Unity Editor'de hatasız açılmalı,
konsol'da "missing script/reference" uyarısı çıkmamalı; ayrıca ESC ile
duraklatma menüsünün hâlâ (tek, doğru şekilde) açıldığı bir sahnede
test edilmeli.

---

## 3. Faz 2 — Kontrol Sonrası Silinecekler
**Risk: Düşük — kontrol tamamlandı (2026-09-15), silinebilir.**

> ✅ **UYGULANDI (2026-09-15)**: 3 yabancı format dosyası (`.tres`/`.usdc`/`.mtlx`)
> ve 3 `.blend` kaynak dosyası `git rm` ile silindi (yanlarındaki
> `textures/` alt klasörleri dokunulmadan korundu). `plastered_wall_02_4k.blend/`
> klasörü ve eşlik eden `.meta`'sı `plastered_wall_02_4k/` olarak
> `git mv` ile yeniden adlandırıldı — GUID'ler korunarak (rename olarak
> izlendi, içerik/GUID değişmedi).

| Öğe | Yol | Sonuç |
|---|---|---|
| `.tres`/`.usdc`/`.mtlx` (3 dosya) | `Assets/Ch1 Palette/metalzeminch2/Metal024_2K-JPG.{tres,usdc,mtlx}` | Bunlar Godot/USD/MaterialX formatı — Unity'nin import pipeline'ı hiç tanımıyor. **Doğrudan silinebilir**. |
| `.blend` dosyaları (2 adet + 1 yapısal anomali) — **GUID ile kontrol edildi** | `Assets/Ch1 Palette/metalzemin/rusty_metal_sheet_4k.blend`, `Assets/Ch1 Palette/metalzeminch2/Metal024_2K-JPG.blend`, `Assets/Ch1 Palette/plastered_wall_02_4k.blend/plastered_wall_02_4k.blend` | Üçünün de GUID'i projede (hiçbir `.prefab`/`.unity`/`.asset` dosyasında) **hiç referans edilmiyor** — yani hiçbiri mesh kaynağı olarak kullanılmıyor, sadece yanlarındaki `textures/` alt klasörleri (materyal dokuları) işe yarıyor. **Karar**: `.blend` kaynak dosyalarını sil, `textures/` alt klasörlerini koru. `Assets/Ch1 Palette/plastered_wall_02_4k.blend/` klasörünü (adı `.blend` uzantısıyla bitiyor, kafa karıştırıcı) `plastered_wall_02_4k/` olarak yeniden adlandır. |

**CasualHit — SİLİNMEYECEK** (hatırlatma): önceki incelemede
`Assets/ThirdParty/CasualHit/` klasörünün `Enemy2.prefab` ve
`ArmoredHazard.prefab` içinde nested prefab olarak (`Hit_4_Red` vb.)
kullanıldığı GUID ile doğrulanmıştı. `BLOODRUSH_YENIDEN_YAPILANDIRMA_PLANI.md`'nin
Faz 1'indeki silme talimatı bu yüzden **yanlış** — o plana geri
dönüldüğünde bu düzeltilmeli.

---

## 4. Faz 3 — Duplicate İçerik Birleştirme
**Risk: Orta** — her grupta en az bir kopya gerçekten kullanılıyor,
kaynak-of-truth seçip diğerini silip referansları güncellemek gerekiyor.
Sırasıyla, büyükten küçüğe:

> ✅ **UYGULANDI (2026-09-15)**: Bu bölüm ile §6.1'deki (Faz 5) karakter
> konsolidasyonu **birlikte, tek bir GUID-doğrulamalı iş turunda**
> gerçekleştirildi (plan zaten bunu öneriyordu). Önce bir araştırma ajanı
> her 4 karakter için hangi dosyaların gerçekten sahne/prefab tarafından
> referans edildiğini (GUID zinciriyle, prefab'ların `m_Modifications`
> override'larına kadar inerek) çıkardı — sonuç, ilk varsayımdan çok
> daha ince taneliydi:
>
> - **Büyük Düşman** ve **Crimson Wretch (düşman1)**: `Prefabs/Characters/`
>   altındaki `.obj` tabanlı prefab, aslında `anim/` altındaki FBX rig'i
>   **içine gömen bir "sarmalayıcı" PrefabInstance** — OBJ'nin kendi mesh'i
>   `m_RemovedGameObjects` ile kaldırılıp yerine rigli FBX nest edilmiş,
>   Animator Controller ve materyal override'larla `anim/` klasörünün
>   dosyalarına yönlendirilmiş. **Bu yüzden `.obj`/`.mtl` dosyaları görsel
>   olarak hiç render edilmese bile PrefabInstance zincirinin yapısal
>   kaynağı olarak kalmak zorunda** — §5.4'teki "muhtemelen gereksiz ham
>   export" varsayımı bu iki karakter için **yanlış** çıktı, silinemezler.
> - **Zırhlı**: gerçekten tamamen kendi kendine yeten, saf OBJ — hiçbir
>   `anim/` karşılığı yok.
> - **Resepsiyonist**: canlı prefab kökte, ama Animator'ın Avatar'ı
>   `resepsiyopnist düz/düz.fbx`'ten, materyalin albedo dokusu da aynı
>   klasördeki `base_color.jpg`'den geliyordu — "düz" klasörü görünüşte
>   tamamen atılabilir bir kopya gibi dururken aslında canlı bağımlılık
>   taşıyordu.
> - Adı "Büyük Düşman"a benzeyen `EnemyBig.prefab`, adında "zırhlı" geçen
>   `ArmoredHazard.prefab`, ve `HighSpeedEnemy.prefab/.fbx` bu 4
>   karakterle **hiç ilgisi olmayan**, ayrı, referanssız eski prototipler
>   çıktı (zaten §5.2'nin 6 prefab'lık listesinde var) — isim
>   benzerliğine aldanılmadı, dokunulmadı.
>
> **Sonuç yapı** (tüm taşımalar `git mv` ile, GUID'ler taşıma sonrası
> tek tek doğrulandı — dört karakterin de kritik prefab/controller GUID'i
> taşımadan önce/sonra birebir aynı, `CH3.unity`'nin `WaveDirector`
> alanları hâlâ doğru şekilde çözülüyor):
>
> ```
> Assets/Characters/
>   BuyukDusman/           (.obj kabuğu + Rig/ altında canlı FBX+controller+materyal, kullanılmayan "Walking" animasyonu yedeklendi)
>   Zirhli/                 (tamamen kendi kendine yeten .obj+.mtl+.png+.prefab)
>   CrimsonWretch/           (.obj kabuğu + Hit_4_Red + vfx_Projectile_01 + Rig/ altında 3 farklı eski klasörden derlenen tek canlı gövde+2 animasyon+controller)
>   Resepsiyonist/           (kök prefab+controller+materyal + Konusma/ + Duz/ alt klasörleri, sadece canlı dosyalarla)
> ```
>
> `Assets/anim/` klasörü artık **tamamen yok** (tüm içeriği ya taşındı ya
> yedeklendi). Referanssız kalan tüm dosyalar (~30+ dosya: kullanılmayan
> animasyon klipleri, kullanılmayan materyal/doku setleri, `düz.prefab`,
> `Material.001.mat` vb.) `_Yedek_Silinecekler/<Karakter>_orphan/`
> altına taşındı, silinmedi.
>
> Henüz commit edilmedi — Unity Editor'de tüm 4 karakterin görsel/animasyon
> olarak hâlâ doğru çalıştığı (CH2 "EnemyBigArena", CH3 dalga spawn'ları,
> CH1 resepsiyon NPC'si) **kullanıcı tarafından doğrulanacak**.

### 4.1 Karakter doku setleri (asıl büyük kazanım, ~40 MB+)
Aynı iki karakterin (Meshy AI ile üretilmiş "Crimson Wretch" zombi ve
"Office Worker" resepsiyonist) doku setleri **3-4 farklı klasöre**
kopyalanmış:

- **Crimson Wretch** dokuları şurada tekrarlanıyor: `Assets/anim/Meshy_AI_Crimson_Wretch_biped/`, `Assets/anim/Meshy_AI_Crimson_Wretch_biped/Meshy_AI_Crimson_Wretch_biped 1/`, `Assets/anim/idlezombi/Meshy_AI_Crimson_Wretch_biped/`, `Assets/Prefabs/Characters/düşman1/` (4 kopya × ~18 MB'a kadar dosyalar = diffuse/normal/metallic/roughness)
- **Office Worker/Resepsiyonist** dokuları: `Assets/anim/Resepsiyonist/`, `Assets/anim/Resepsiyonist/konuşma/`, `Assets/anim/Resepsiyonist/resepsiyopnist düz/`, artı bir kopyası doğrudan `Assets/Textures/Material_1_baseColor.png` olarak (3-4 kopya × ~6-7 MB)

**Kök neden**: karakter varlıkları tek bir kanonik konumda tutulmuyor —
her yeni animasyon/pose export edildiğinde texture'lar da beraberinde
yeniden kopyalanmış. Bu, Faz 5'teki klasör birleştirmesiyle asıl kalıcı
çözümü bulacak; burada sadece **fazlalık kopyaları silmek**:

1. Her karakter için **tek bir kanonik doku klasörü** seç (önerim:
   `Prefabs/Characters/<KarakterAdı>/` — çünkü asıl kullanılan prefab
   zaten orada).
2. Diğer kopyaları sil, ama önce o kopyaları referans eden
   materyal/prefab varsa (`grep` ile GUID kontrolü) kanonik konuma
   yönlendir.
3. `Assets/Textures/Material_1_baseColor.png` gibi "yanlışlıkla ana
   Textures klasörüne düşmüş" tekil dosyaları özellikle önceliklendir.

### 4.2 `vfx_Explosion_01.prefab` (945 KB) — **DÜZELTME: bu bir "sil" durumu değil**
GUID bazlı referans kontrolü yapıldı ve ilk varsayımım yanlış çıktı:
**her iki kopya da gerçekten kullanılıyor**, farklı yerlerden:
- `Assets/Prefabs/Weapons/vfx_Explosion_01.prefab` → `Assets/Prefabs/Weapons/Grenade.prefab` tarafından referans ediliyor (nested prefab)
- `Assets/ThirdParty/GabrielAguiarProductions/FreeQuickEffectsVol1/Prefabs/vfx_Explosion_01.prefab` → **doğrudan `Assets/Scenes/CH2.unity` sahnesinden** referans ediliyor (paketin kendi demo sahnesi `FreeQuickEffectsVol1.unity` da ayrıca referans ediyor, bu beklenen/normal)

**Asıl sorun bu değil, şu**: CH2 sahnesi proje-içi kopya yerine
doğrudan vendor paketinin orijinal prefabına bakıyor. Bu **kırılgan bir
referans** — `ThirdParty/GabrielAguiarProductions/` klasörü ileride
güncellenir/silinirse (paket versiyonu değişirse vb.) CH2 sahnesi
sessizce kırılır. **Öneri**: hiçbir dosyayı silme; CH2.unity'de
`vfx_Explosion_01` referansını `Prefabs/Weapons/` kopyasına yönlendir
(Unity Editor'de sahneyi aç, ilgili GameObject'in prefab referansını
proje-içi kopyaya değiştir). Bu bir "gereksiz dosya" maddesi değil,
"kırılgan bağımlılık" maddesi — Faz 3'ten çıkarıp ayrı not ediyorum.

### 4.3 `Audio/damage.wav` = `Audio/mixkit-fast-blow-2144.wav` (197 KB)
Aynı ses dosyası iki farklı isimle mevcut. Hangi isim kod içinde
referans ediliyorsa (`grep "damage.wav\|mixkit-fast-blow"`  ile
`AudioClip` alanlarını kontrol et — muhtemelen Inspector'da direkt
dosya referansı var, isim önemli değil) o kalsın, diğeri silinsin.

### 4.4 `Scenes/CH3` ve `Scenes/CH4` içindeki `VolumeProfileAsset.png`/`Volume.png` (düşük öncelik)
Muhtemelen HDRP'nin sahne başına otomatik ürettiği Volume önizleme
görselleri — her sahne için ayrı ayrı üretilmiş olması normal, isteyerek
aynı olmaları teknik bir zorunluluk değilse de bu ikisi muhtemelen
**gerçek bir sorun değil** (iki farklı sahnenin varsayılan Volume
profili görsel olarak aynı görünüyor olabilir). Bu grup için önerim:
**dokunma**, düşük değerli/riskli bir hedef.

---

## 5. Faz 4 — Kullanılmayan Prefab/Materyal/Texture Temizliği
**Risk: Orta-Yüksek** — "referanssız" ile "gereksiz" aynı şey değil;
bazıları gelecekte kullanılacak WIP içerik olabilir.

> **2026-09-15 karar — Yedek Klasör politikası**: Bu fazdaki hiçbir öğe
> doğrudan `Assets/`'ten kalıcı silinmeyecek. Bunun yerine `Assets/`
> **dışında**, proje kökünde `_Yedek_Silinecekler/` adlı bir klasöre
> taşınacak (Unity `Assets/` dışındaki dosyaları import etmediği için bu
> klasör Library/derleme süresini etkilemez, ama dosyalar git ile takip
> edilmeye devam eder — istenirse geri taşınabilir). Bu, "sonradan ekleme
> maliyeti düşükse sil, ama yedekte dursun" tercihini karşılıyor. Taşıma
> **dosya + `.meta` birlikte** yapılmalı (Unity Editor kapalıyken).

### 5.1 Yüksek güvenilirlikli ölü küme: Legacy karakter materyal/texture seti
`Assets/Materials/` altında **11 materyal** (`Body_MAT`, `Brows_MAT`,
`Eye_MAT`, `Eye_Spec_MAT`, `Exo_MAT`, `Ch15_body`, `Ch15_body1`,
`Ch35_body`, `Ch35_body1`, `Ch35_body2`, `BloodMaterial`) ve bunlara
bağlı **~40 texture** (specular/glossiness/diffuse/normal/roughness
kanalları) projenin **hiçbir yerinden** (sahne, prefab, başka materyal)
referans edilmiyor. Bu, `SharedCharacterMaterial.mat`/
`SharedAccentMaterial.mat` (aktif kullanılan, projede referans edilen)
tarafından **yerini almış eski bir karakter shading iterasyonu** gibi
görünüyor — isimlendirme deseni de (Ch15/Ch35 = muhtemelen eski
karakter model versiyon numaraları) bunu destekliyor.

**Karar (2026-09-15)**: Kullanıcı Editor'de gözle kontrol ettikten sonra
silmeyi onayladı — ama kalıcı silme yerine `_Yedek_Silinecekler/Materials_Legacy/`
klasörüne taşınacak. Bu, en yüksek güvenle önerebileceğim gruptu; Editor
doğrulaması **kullanıcı tarafından akşam yapılacak** (uzaktan bağlantıyla
şu an iş yerinde, Unity Editor'e erişimi yok).

### 5.2 Düşük güvenilirlikli: 6 referanssız prefab
`ArmoredHazard.prefab`, `EnemyBig.prefab`, `HighSpeedEnemy.prefab`,
`Cube.prefab`, `Door.prefab` (`Prefabs/Environment/`), `HorizontalBlock.prefab`
— hiçbir sahne/prefab/WaveManager-WaveDirector spawn dizisinden
referans edilmiyor.

**Dikkat**: `Flow/WaveDirector.cs` kod yorumunda açıkça "fast/jumper/
exploder alanları kullanılmıyor — Inspector'da boş kalabilir" deniyor.
Yani `HighSpeedEnemy.prefab` (→ muhtemelen `fastEnemy` alanına
atanacaktı) ve `EnemyBig.prefab` (→ `bigEnemy`? ama `WaveDirector`'da
`bigEnemy` zaten dolu olabilir, kontrol edilmeli) **bilinçli olarak
henüz devreye alınmamış düşman çeşitliliği** olabilir — yani "gereksiz"
değil, "henüz kullanılmayan gelecek içerik". `ArmoredHazard.prefab` da
aynı şekilde bir düşman varyantı gibi duruyor.

**Karar (2026-09-15)**: "Sonradan ekleme maliyeti çok değilse silebilirsin,
ama yedek için bir klasöre atabiliriz." Bu 6 prefab kalıcı silinmeyecek —
`_Yedek_Silinecekler/Prefabs_Kullanilmayan/` klasörüne taşınacak. Böylece
`Assets/`'ten çıkıyorlar (proje temizleniyor) ama fiziksel olarak
duruyorlar — ileride bu düşman tipleri oyuna girerse maliyetsiz geri
taşınabilir.

**Kapı prefabı netleşti (GUID doğrulaması yapıldı)**: `Assets/door.prefab`
(kök, küçük harf `d`) **`Assets/Scenes/CH4.unity` tarafından kullanılıyor**
— bu yaşayan, gerçek kapı. `Assets/Prefabs/Environment/Door.prefab`
(büyük `D`) ise hiçbir yerden referans edilmiyor — bu listedeki diğer 6
prefab'ın aksine, bunun WIP/gelecek içerik olma ihtimali düşük
(muhtemelen kök'teki `door.prefab`'ın önceki bir taşıma denemesi/kopyası).
**Bu ikisi için özel öneri**: `Prefabs/Environment/Door.prefab`'ı Faz 1
güvenliğinde sil, ardından Faz 5'te kök `door.prefab`'ı (artık isim
çakışması olmadan) `Prefabs/Environment/`'a taşı.

### 5.3 Çevre texture'ları (Ch1/Ch3) — muhtemelen "eksik bağlantı", "gereksiz" değil
`Ch1/Zemin/Tiles036_2K-JPG_Roughness.jpg`, `Ch1/Asansör/Metal009_2K-JPG_Metalness.jpg`
gibi bazı dokular hiçbir materyale bağlı değil — ama bunlar
roughness/metalness/normal gibi **PBR kanal dokuları**. Bunun anlamı
muhtemelen "bu doku gereksiz" değil, "bu materyalde bu kanal hiç
doldurulmamış" (örn. `Asasnsör.mat`'ın Metalness slotu boş bırakılmış
olabilir). **Öneri**: silme, önce ilgili materyalleri Editor'de açıp
hangi kanalların gerçekten boş bırakıldığını kontrol et — bu bir
temizlik değil, olası bir görsel kalite eksikliği (silinecek gereksiz
dosya değil, tamamlanmamış iş).

> **2026-09-15 durum**: "Kontrol edilecek" — bu, Unity Editor'de
> materyalleri açıp kanalları gözle kontrol etmeyi gerektiriyor.
> Kullanıcı şu an işte, uzaktan bağlantıyla çalışıyor ve Editor'e
> erişimi yok; bu tür manuel Editor kontrollerini **akşam eve gelince**
> yapacak. Bu madde beklemede — text/GUID taramasıyla çözülemez, gerçek
> görsel doğrulama gerekiyor.

### 5.4 Ham `.obj`/`.mtl` mesh export'ları (yeni bulundu, ~190 MB)
`Assets/Prefabs/Characters/` altında, dönüştürülmüş `.prefab`'ların
yanında AI-export'un ham `.obj`/`.mtl` dosyaları da duruyor (7 çift,
en büyükleri `Büyük_Düşman/...texture.obj` 63,1 MB ve
`Zırhlı/...texture.obj` 60,9 MB). Bu dosyalar prefab'lar zaten içe
aktarılmış model/materyal referansı taşıdığı için genelde gereksiz —
ama Unity `.obj` dosyasını da native import edebildiğinden, bazı
prefab'lar `.obj`'u doğrudan mesh kaynağı olarak kullanıyor olabilir.

> ✅ **SONUÇ (2026-09-15) — varsayım YANLIŞ çıktı**: Karakter konsolidasyonu
> sırasında yapılan GUID zinciri doğrulaması gösterdi ki Büyük Düşman ve
> Crimson Wretch'in `.obj`/`.mtl` dosyaları **silinemez** — görsel olarak
> render edilmeseler bile, `anim/` klasöründeki gerçek rigli FBX'i içine
> gömen "sarmalayıcı" PrefabInstance'ın **yapısal kaynağı** olarak
> kalıyorlar (Unity, bir PrefabInstance'ın kaynağı silinirse referansı
> kırar). Zırhlı'nınki zaten tek temsil biçimi olduğu için hiç "ham
> export" değil. **Hiçbiri yedek klasöre taşınmadı** — hepsi ilgili
> karakterin `Assets/Characters/<İsim>/` klasörüne canlı içerik olarak
> taşındı (bkz. §4 başındaki uygulama notu).

---

## 6. Faz 5 — Klasör Yeniden Yapılandırma
**Risk: Orta** — GUID'ler dosya içeriğinde saklı olduğu için dosya +
`.meta`'sı birlikte taşınırsa referanslar bozulmaz, ama **Unity Editor
kapalıyken** toplu taşıma yapılması önerilir (Editor açıkken yapılan
taşımalar Unity'nin kendi iç senkronizasyonuyla çakışabilir).

### 6.1 Karakter varlıklarını tek bir yapıya topla (asıl kök çözüm)

> ✅ **UYGULANDI (2026-09-15)** — bkz. §4 başındaki uygulama notu, aynı
> iş turunda tamamlandı.

`anim/`, `anim/idlezombi/`, `Prefabs/Characters/` arasında dağılmış
karakter içeriğini (bkz. §4.1 duplicate analizi) tek bir konvansiyona
taşı. Önerilen hedef yapı:

```
Assets/Characters/
  BuyukDusman/          (eski: anim/BüyükDüşman + Prefabs/Characters/Büyük_Düşman)
    Model/
    Materials/
    Animations/
    BuyukDusman.controller
  CrimsonWretch/         (eski: anim/Meshy_AI_Crimson_Wretch_biped + anim/idlezombi + Prefabs/Characters/düşman1)
    ...
  Resepsiyonist/         (eski: anim/Resepsiyonist + tüm alt-varyantları)
    ...
  Zirhli/                 (eski: Prefabs/Characters/Zırhlı — Türkçe karakter kaldırıldı)
    ...
```

Bu, hem §4.1'deki duplicate'lerin **kök nedenini** ortadan kaldırır
(tek konum = tekrar kopyalama ihtiyacı kalmaz), hem de `anim/` ile
`Animation/` arasındaki kafa karıştırıcı isim çakışmasını çözer.
**Doğrulandı**: `Assets/Animation/Animation.controller` (klasördeki tek
dosya) GUID taramasında da hiçbir yerden referans edilmediği çıktı —
yani `Animation/` klasörünün tamamı ölü, Faz 1 güvenliğinde silinebilir
(ayrı bir "kontrol et" maddesi değil, kesinleşmiş bir bulgu).

> **Netlik notu — "üst seviye klasör yapısı değişmiyor" kararıyla çelişki mi? (ONAYLANDI)**
> `BLOODRUSH_YENIDEN_YAPILANDIRMA_PLANI.md`'nin karar tablosundaki "Üst
> seviye klasör yapısı: Değişmiyor" maddesinin `Assets/Scripts/` domain
> klasörlerini (Player/Enemy/Weapons/...) kapsadığı, 3D içerik
> klasörlerini (`Materials`, `Textures`, `Prefabs`, `anim`) kapsamadığı
> kullanıcıyla netleştirildi ve **onaylandı** — yeni `Assets/Characters/`
> üst-seviye klasörü ile devam ediliyor.

**Bu en büyük ve en riskli tek adım** — her karakter prefabının/
Animator Controller'ının/materyalin referanslarının bu taşımadan sonra
hâlâ doğru çalıştığı Editor'de tek tek doğrulanmalı.

### 6.2 Kök seviyedeki başıboş dosyalar
- `Assets/door.prefab` → `Assets/Prefabs/Environment/` (bu adımda artık
  isim çakışması yok — ölü `Door.prefab`, büyük harf, zaten Faz 1'de
  silinmiş olacak, bkz. §2 ve §5.2)
- `Assets/DefaultNetworkPrefabs.asset` → zaten
  `BLOODRUSH_YENIDEN_YAPILANDIRMA_PLANI.md` Faz 1'de Netcode paketiyle
  birlikte silinmesi planlanmış, bu planla çakışmıyor.

### 6.3 Bölüm (chapter) isimlendirme tutarsızlığını düzelt (ONAYLANDI)
Şu an üç farklı kural aynı anda kullanılıyor:
- `Assets/Scenes/`: `CH2`, `CH3`, `CH4` (büyük harf, `CH1` yok — muhtemelen `CH1.unity` kök seviyede duruyor, alt klasör değil)
- `Assets/Textures/`: `Ch1`, `Ch3` (karışık case, `Ch2`/`Ch4` yok)
- `Assets/Ch1 Palette/`: `Ch1` + boşluk

**Karar (2026-09-15)**: önerdiğim kural uygulanacak — hep `CH1`, `CH2`,
`CH3`, `CH4` (sahne isimleriyle birebir tutarlı, büyük harf).
`Textures/` altındaki `Ch1`→`CH1`, `Ch3`→`CH3` olarak yeniden adlanacak,
eksik `CH2`/`CH4` alt klasörleri (o bölümlerin özel dokuları varsa) aynı
düzene sokulacak.

**`Ch1 Palette` klasörünün yeni yeri — incelendi, karar verildi**:
İçeriği kontrol edildi — `SurfacePalette.asset` (bölümün görsel kimliğini
tutan `ScriptableObject`, `Flow/SceneDresser.cs` tarafından kullanılıyor)
+ ham `metalzemin`/`metalzeminch2`/`plastered_wall_02_4k` malzeme
kaynakları içeriyor; yani bu klasör zaten kavramsal olarak "CH1'in
doku/materyal paleti". **Karar**: `Assets/Ch1 Palette/` →
`Assets/Textures/CH1/Palette/` altına taşınacak — ayrı bir üst-seviye
"palette" kavramı yerine, zaten var olan `Textures/CH1/` yapısının bir
alt klasörü olacak (Ch3'ün `Wall/`, `Flooor/` alt klasörleriyle aynı
mantık).

### 6.4 Yazım hatalarını düzelt
- `Ch1/Asansör/Asasnsör.mat` → `Asansor.mat` (hem yazım hatası hem
  Türkçe karakter düzeltilir — materyal ismindeki referanslar
  güncellenmeli)
- `Ch3/Flooor/` → `Floor/`
- `anim/Resepsiyonist/resepsiyopnist düz/` → §6.1'deki konsolidasyonla
  ortadan kalkacak (klasör tamamen `Assets/Characters/Resepsiyonist/`
  içine eritilecek)

### 6.5 Türkçe özel karakterleri kaldır (ONAYLANDI — kapsam netleşti)
**Karar**: sadece Türkçe özel karakterler (ı, ğ, ü, ş, ö, ç, İ) dosya/
klasör isimlerinden ASCII'ye çevrilecek. Halihazırda ASCII olan Türkçe
kelimeler (`Veri`, `Kitap1`, `Kitap2`, `Resepsiyonist` gibi — bunlarda
zaten özel karakter yok) **değiştirilmeyecek**. Oyun içi metin/diyalog/
UI (örn. `Kitap1.asset` içindeki `title: "GEÇMİŞ"` gibi) bu maddeyle
**hiç ilgili değil** — sadece dosya sistemi isimleri değişiyor, oyunun
gösterdiği Türkçe metinlere dokunulmuyor.

> ✅ Tablodaki karakter-ilişkili 6 satır (Büyük_Düşman, BüyükDüşman,
> Zırhlı, düşman1, KüçükDüşmanController, konuşma, resepsiyopnist düz)
> §4/§6.1'in karakter konsolidasyonuyla birlikte uygulandı. Sadece
> `Textures/Ch1/Asansör/` satırı hâlâ bekliyor (bölüm isimlendirmesiyle
> birlikte ele alınacak, §6.3/6.4).

**Kesin yeniden adlandırma listesi** (özel karakter içeren tüm öğeler):

| Eski isim | Yeni isim | Not |
|---|---|---|
| `Prefabs/Characters/Büyük_Düşman/` | `BuyukDusman` | §6.1 ile `Assets/Characters/BuyukDusman/` altına taşınacak |
| `anim/BüyükDüşman/` | (yukarıyla birleşecek) | §6.1 konsolidasyonu |
| `Prefabs/Characters/Zırhlı/` | `Zirhli` | §6.1 ile `Assets/Characters/Zirhli/` |
| `Prefabs/Characters/düşman1/` | `dusman1` | §6.1 ile Crimson Wretch konsolidasyonuna dahil |
| `Textures/Ch1/Asansör/` | `Asansor` | §6.4'teki yazım hatası düzeltmesiyle birlikte |
| `anim/KüçükDüşmanController.controller` | `KucukDusmanController.controller` | Taşınacağı konum §6.1'de netleşecek |
| `anim/Resepsiyonist/konuşma/` | `konusma` | §6.1 ile `Assets/Characters/Resepsiyonist/` altına |
| `anim/Resepsiyonist/resepsiyopnist düz/` | (§6.1 ile eriyecek, ayrı isim kalmayacak) | Yazım hatası da düzeliyor |

`Assets/Veri/`, `Kitap1.asset`, `Kitap2.asset`, `Resepsiyonist` (üst
klasör adı) gibi zaten özel karakter içermeyen Türkçe kelimeler
**değişmeden kalıyor**.

### 6.6a Casing tutarsızlığı (`Boss.prefab` vs `boss.fbx`) — 2026-09-15 tarandı
Kullanıcı isteği üzerine `Prefabs/Characters/` ve `Prefabs/Weapons/`
genelinde benzer prefab-adı/kaynak-model-adı case uyuşmazlıkları için
tarama yapıldı (prefab ile aynı isimdeki `.fbx`/`.obj` dosyaları
karşılaştırıldı). **Sonuç**: `Boss.prefab`/`boss.fbx` dışında başka bir
uyuşmazlık bulunamadı — bu tek başına duran bir örnek. **Karar**:
düzeltilecek — `boss.fbx` → `Boss.fbx` olarak yeniden adlandırılıp
prefab'ın model referansı Editor'de teyit edilecek (dosya adı değişince
Unity genelde GUID'i korur ama görsel kontrol önerilir).

### 6.6 Küçük isimlendirme temizlikleri
- ✅ `Assets/anim/Meshy_AI_Crimson_Wretch_biped/Meshy_AI_Crimson_Wretch_biped 1/` — §6.1 konsolidasyonuyla ortadan kalktı (canlı gövde `Assets/Characters/CrimsonWretch/Rig/`'e taşındı)
- `Assets/Materials/New Material.mat`, `Assets/Textures/Ch3/Flooor/textures/New Material.mat` — varsayılan isimle bırakılmış materyaller, açıklayıcı isim verilmeli (hâlâ bekliyor)
- `Assets/Textures/Ch3/Wall/Wall 1.mat` → `Wall_02.mat` gibi anlamlı bir isim (hâlâ bekliyor)
- `Assets/Scenes/OutdoorsScene/NavMesh-Plane 1.asset` → " 1" kaldır (hâlâ bekliyor)
- ✅ `Büyük_Düşman` (alt çizgi) vs `BüyükDüşman` (bitişik) — §6.1 ile tek isimde (`BuyukDusman`) birleşti

---

## 7. Faz 6 — .gitignore Güncelleme
**Risk: Sıfır.**

> ✅ **UYGULANDI (2026-09-15)**: `.claude/` ve `.remember/` satırları
> `.gitignore`'un sonuna eklendi.

Mevcut `.gitignore` standart ve sağlıklı (Library/Temp/Logs/UserSettings
doğru ignore edilmiş, hiçbiri yanlışlıkla track edilmiyor). Tek eksik:

```gitignore
# Yerel araç durumu — proje içeriği değil
.claude/
.remember/
```

Bu satırları `.gitignore`'un sonuna eklemek yeterli. Şu an bu klasörler
track edilmiyor (kimse `git add` etmemiş) ama kural olmadığı için biri
`git add -A` çekerse yanlışlıkla commit'lenebilirler.

---

## 8. Faz 7 — Belgeleme Takip Maddeleri
**Risk: Sıfır, ama bu plandan bağımsız, ayrı ele alınmalı.**

1. ✅ **ÇÖZÜLDÜ (2026-09-15)**: `docs/TECH_OVERVIEW.md` genişletildi —
   6 paralel araştırmayla `Assets/Scripts/Flow/` altındaki tüm eksik
   katman (Puzzle/Objektif sistemleri, Karanlık Sekans modülü, Aşırı Yük
   odası, Anlatı/Akış altyapısı — GameFlow, Harita İnşa Araçları, Ek
   Düşman Tipleri/FX Yardımcıları, UI Ek Bileşenleri, Editor Araçları)
   dokümante edildi; `docs/BLOODRUSH_Gelistirme_Ozeti.pdf` birincil
   kaynak olarak kullanılıp kodla çapraz doğrulandı. Belge 430 satırdan
   1637 satıra çıktı (§1-§23). Yol boyunca birkaç **önemli düzeltme**
   de yapıldı: `PresentationSequence.cs`'in gerçek video oynattığı
   (eski "video bulunamadı" notu yanlıştı), `DirectionalArmor.cs`'nin
   artık ölü kod olduğu, `GameHUD`'un BEDEN/YÜKLEME barlarının
   kaldırıldığı (PDF'teki iddia doğrulandı), `MainMenuManager`'ın
   `SettingsStore`'u atlayan paralel bir `PlayerPrefs` kalıcılığı
   taşıdığı, ve §13 Third-Party bölümündeki eski CasualHit hatasının
   (yanlışlıkla "kullanılmıyor" denmesi) resmi olarak düzeltildiği.
   **Artık bu planın fazlarına başlanabilir** — §2'den itibaren.
2. ✅ **ÇÖZÜLDÜ**: `Assets/Scripts/UI/PauseMenu.cs` vs `PauseMenuController.cs`
   — kontrol edildi, `PauseMenu.cs`'nin `PauseMenuController.DisableLegacy()`
   tarafından her sahne yüklemesinde otomatik kapatıldığı kod yorumuyla
   doğrulandı (davranışsal olarak tamamen ölü). Silme adımı Faz 1'e
   taşındı (bkz. §2).
3. ✅ **ÇÖZÜLDÜ**: `STORY_DESIGN.md` `git show ac481e9~1:STORY_DESIGN.md`
   ile geri getirildi, `docs/STORY_DESIGN.md` olarak kaydedildi.
4. ✅ **ÇÖZÜLDÜ (2026-09-15)**: `BLOODRUSH_Gelistirme_Ozeti.pdf` ve
   `DEGISIKLIKLER.txt` `docs/`'a taşındı (`git mv`, geçmiş korunarak).
   **Önemli bulgu**: PDF beklenenden çok daha değerliydi — 15 Temmuz 2026
   tarihli, kök-sebep analizli bir "Geliştirme Özeti & Bug Fix Raporu";
   `GameFlow`, `Arena`, `WaveDirector`, terminal sistemleri, oda inşa
   araçları (`RoomBuilder`/`ArenaBuilder`/`StairBuilder`/`LightGrid`/
   `AutoLightRig`), `IntroSalonController`, `EndingSequence`,
   `PresentationScreen` gibi birçok sistemi zaten anlatıyor — bu,
   `TECH_OVERVIEW.md` genişletme çalışmasında (bkz. §8 madde 1) birincil
   kaynaklardan biri olarak kullanılacak (kodla çapraz doğrulanarak,
   çünkü Temmuz'dan beri eklenen sistemler — Karanlık Sekans, Aşırı Yük
   odası — bu belgede yok).
   **Root'ta kalanlar (bilinçli, taşınmadı)**: `README.md` ve `LICENSE`
   — GitHub konvansiyonu bu ikisinin repo kökünde olmasını bekliyor
   (otomatik gösterim için); `docs/`'a taşınmadılar.
5. ✅ **ÇÖZÜLDÜ**: `docs/BLOODRUSH_YENIDEN_YAPILANDIRMA_PLANI.md` ve
   `docs/BLOODRUSH_MIMARI_KURALLARI.md` kaydedildi, bugünkü kararlarla
   güncellendi (Faz 6 birleştirme, Enemy AI tam refactor, girdi
   fazlarının bölünmesi, save sistemi kapsam dışı, CasualHit düzeltmesi,
   faz sıralaması değişikliği).

---

## 9. Önerilen Uygulama Sırası ve Bağımlılıklar

```
Faz 1 (sıfır risk, hemen)
Faz 6 (.gitignore, sıfır risk, hemen, Faz 1'den bağımsız)
   │
   ▼
Faz 2 (kontrol sonrası silme — Ch1 Palette .blend'ler)
   │
   ▼
Faz 3 (duplicate birleştirme — özellikle §4.1 karakter dokuları)
   │
   ▼
Faz 5 (klasör yeniden yapılandırma — §6.1 karakter konsolidasyonu,
        Faz 3'ün duplicate temizliğiyle aynı anda planlanmalı çünkü
        ikisi de aynı dosyalara dokunuyor — ayrı ayrı yapılırsa iş
        tekrarı olur)
   │
   ▼
Faz 4 (kullanılmayan prefab/materyal/texture — §5.1/§5.2/§5.4'ün
        yedek-klasöre-taşıma kararı zaten alındı; §5.2'deki "bu düşman
        tipleri oyuna girecek mi" tasarım sorusu hâlâ açık ama bu,
        taşıma işlemini engellemiyor — açık kalabilir)
   │
   ▼
Faz 7 (belgeleme — herhangi bir zamanda, bağımsız)
```

**Önemli not**: Faz 3 ve Faz 5'in §6.1'i **birlikte** ele alınmalı —
karakter dosyalarını hem duplicate'lerden temizleyip hem de yeni bir
klasör yapısına taşımak tek bir iş turu olmalı, iki ayrı turda yapılırsa
(önce sil, sonra taşı) gereksiz risk ve çakışma ihtimali artar.

> **Çalışma şekli (2026-09-15 karar): fazlar teker teker onaylanarak
> ilerlenecek.** "Tüm fazları tek seferde yapmaya çalışmak hataları
> doğurabilir. Tek tek gidersek yaptığımızı detaylı bir şekilde
> yapabiliriz." — her faz bitince durup senin onayını bekleyeceğim,
> otomatik olarak bir sonrakine geçmeyeceğim.
>
> **Yedek klasör konumu**: `_Yedek_Silinecekler/` (proje kökünde,
> `Assets/` dışında — bkz. §5 giriş notu). Alt klasörler: `Materials_Legacy/`
> (§5.1), `Prefabs_Kullanilmayan/` (§5.2), `RawExports_OBJ/` (§5.4).
>
> **Editor-bağımlı bekleyen maddeler** (kullanıcı akşam yapacak):
> §5.1'in son görsel onayı, §5.3 (Ch1/Ch3 materyal kanalları), ve genel
> olarak her fazın "Editor'de aç, test et" doğrulama adımları.

---

## 10. Doğrulama Planı

Otomatik test altyapısı olmadığından (bkz. `TECH_OVERVIEW.md` §22),
her faz sonrası manuel doğrulama şart:

- **Faz 1-2 sonrası**: Unity Editor'de proje açılır, konsolda hata/
  uyarı olmadığı kontrol edilir, `CH1`–`CH4` sahneleri tek tek açılıp
  Play Mode'a girilir.
- **Faz 3-5 sonrası (karakter taşıma/birleştirme)**: her karakterin
  (`BüyükDüşman`, `Zırhlı`, `düşman1`/Crimson Wretch, Resepsiyonist)
  kullanıldığı prefab Inspector'da açılıp model/materyal/animasyonun
  hâlâ doğru bağlı olduğu (pembe/missing materyal yok) doğrulanır;
  ilgili sahnede o karakter görsel olarak kontrol edilir.
- **Faz 4 sonrası**: `_Yedek_Silinecekler/`'e taşınan materyal/prefab/
  `.obj`'ların hâlâ hiçbir yerden referans edilmediğinden emin olmak için
  tekrar bir GUID taraması yapılabilir (bu planı hazırlayan aynı
  yöntemle) — taşıma sonrası kırık referans varsa Unity Editor'de
  "missing" olarak görünür.
- **Her fazdan sonra**: `git status` ile nelerin değiştiği gözden
  geçirilir, anlamlı bir commit mesajıyla kaydedilir (fazlar ayrı
  commit'ler olarak tutulmalı — bir şey ters giderse tek fazı geri
  almak mümkün olsun).

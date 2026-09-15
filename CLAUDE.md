# BLOODRUSH — Proje Notları (Claude Code için)

Unity 2022.3.62f3 LTS, HDRP 14.0.12, Mono scripting backend. Tek oyunculu,
bölüm bazlı (Ch1–Ch4) FPS/aksiyon oyunu. Erken/orta geliştirme aşaması —
büyük refactor'lara açık.

## Doküman haritası

Bu dosya sadece kısa bir özet/indekstir. Detaylar için:

- **`docs/TECH_OVERVIEW.md`** — projenin teknik envanteri (tüm sistemler,
  klasör yapısı, sınıf ilişkileri). Yeni bir sistemi anlamadan önce buraya bak.
- **`docs/BLOODRUSH_MIMARI_KURALLARI.md`** — kod yazarken uyulacak kurallar
  (katmanlama, singleton politikası, pooling sözleşmesi, adlandırma, event/
  decoupling, input kuralı). **Yeni kod yazmadan önce mutlaka oku.**
- **`docs/BLOODRUSH_YENIDEN_YAPILANDIRMA_PLANI.md`** — "**Refactor planı**".
  Faz 1-7, **hepsi tamamlandı** (2026-09-15). Her fazın ✅ UYGULANDI notu var —
  ne yapıldığı, hangi kararların bilerek daraltıldığı, ne test edilmesi
  gerektiği orada. Bir konuda "neden böyle yapıldı" sorusu varsa önce burayı ara.
- **`docs/TEMIZLIK_VE_YAPI_PLANI.md`** — "**Temizlik planı**" (asset/klasör
  temizliği, GUID doğrulamalı duplikat/orphan analizi). Kod/dosya tarafı
  tamamlandı; sadece 3 Editor-bağımlı madde kullanıcıda kaldı (§9 sonu).
- **`docs/STORY_DESIGN.md`** — anlatı/hikaye tasarımı.

> **İsimlendirme notu**: iki plan dosyası da kendi bağımsız "Faz 1...Faz 7"
> sırasını kullanıyor ama tamamen farklı işler. Bahsederken her zaman
> **"Refactor planının Faz N'i"** / **"Temizlik planının Faz N'i"** de,
> yoksa karışır.

## Mimari kurallar — çok kısa özet

Tam metin `docs/BLOODRUSH_MIMARI_KURALLARI.md`'de. En sık unutulanlar:

- Domain klasörleri (`Player`, `Enemy`, `Weapons`, `Arena`, `Flow`, `UI`,
  `Shared`) korunuyor — üst seviye `Core/Systems/Presentation` gibi bir
  yeniden gruplama YOK. Katmanlama (Veri/Mantık/Sunum) her domain'in
  **içinde** sağlanır.
- Denge/config değerleri (hasar, menzil, süre, dalga parametreleri vb.)
  `ScriptableObject` veri katmanına çıkarılır (`WeaponData`, `WaveData` —
  `Assets/Veri/` altında asset instance'ları var).
- Sınıf/namespace/arayüz adları **İngilizce**. Metod/değişken isimleri
  **Türkçe-ASCII** serbest (ı→i, ğ→g, ü→u, ş→s, ö→o, ç→c) — ama sadece
  **yeni yazılan/o an refactor edilen** kodda; mevcut İngilizce isimler
  sırf dil değişikliği için toplu rename edilmez.
- Sık `Instantiate`/`Destroy` edilen nesneler `PoolManager.Get<T>()` /
  `PoolManager.Release()` üzerinden (`Assets/Scripts/Shared/Pooling/`).
  Tek seferlik/nadir nesneler (`WeaponPickup`, `AmmoPickup`, düşman
  spawn'ları — bkz. aşağı) muaf.
- Yeni bir singleton eklemeden önce mimari kurallar §2'deki 3 soruyu
  cevapla, gerekçeyi yorum olarak yaz.
- Cross-domain iletişim event/interface üzerinden (`IParryable` gibi),
  somut sınıf tipini bilerek değil.
- **Yeni yazılan hiçbir kod legacy `Input` sınıfına dokunmaz** — sadece
  Input System / `KeyBindings.Down/Held/Up`, `DownKey/HeldKey/UpKey`,
  `MouseDelta`.

## Teknik durum (2026-09-15 itibarıyla)

- `activeInputHandler: 2` ("Both") — legacy Input Manager ve yeni Input
  System aynı anda etkin. `KeyBindings.cs`'in depolama katmanı (`KeyCode[]`
  cache) bilerek legacy'de kaldı (rebind sistemi ona bağlı), sorgu katmanı
  yeni Input System üzerinden okuyor.
- Enemy AI ortak taban oturdu: `EnemyAIBase` → `EnemyAI`, `EnemyAIBase` →
  `BossAIBase` → `BossAI`/`ExperimentBossAI`. State machine'ler **bilerek
  ayrı** kaldı (her sınıfın kendi `State` enum'u + switch'i) — zorla tek
  bir FSM'e sıkıştırılmadı.
- Dalga sistemi birleşti: `Assets/Scripts/Arena/WaveDirector.cs` (eski
  `WaveManager`+`WaveDirector`). Mod seçimi `WaveData.hasFixedWaveCount` —
  sabit dalga (Arena) vs. heat-bazlı eskalasyon (Ch3 Server Core), ikisi
  kendi orijinal algoritmasını korudu.
- Silah verisi `WeaponData` SO'larında (`Assets/Veri/Weapons/`), dalga
  verisi `WaveData` SO'larında (`Assets/Veri/Arena/`).

## Bilinen açık noktalar (Editor'de doğrulanmalı)

- **`CH3.unity`'de Player GameObject'inde muhtemelen çift `PlayerShoot`
  komponenti var** (biri prefab'tan, biri sahne-instance seviyesinde
  `m_AddedComponents` ile eklenmiş orphan bir kopya — muhtemelen eski bir
  prefab restructure kalıntısı). Inspector'da kontrol et, fazlaysa sil.
  Detay: Refactor planı Faz 3 notu.
- Temizlik planı §5.1 (görsel onay), §5.3 (Ch1/Ch3 materyal kanalları),
  Refactor planı Faz 4'ün §4.2'si (`vfx_Explosion_01`'in CH2.unity'deki
  referansını Editor GUI'den repoint etme) — hâlâ Editor-bağımlı, yapılmadı.
- Düşman spawn'ları (`WaveDirector`) **bilerek pool'lanmadı** (Refactor
  planı Faz 6 notu) — doğru havuzlama `EnemyAIBase`'e tam bir "yeniden
  doğuş" sözleşmesi gerektiriyor, ayrı bir iş olarak bırakıldı.

## Bu projede çalışırken dikkat edilecekler

- **Unity asset/prefab/scene değişikliklerinde GUID doğrulaması şart** —
  bir script/asset'in "kullanılıp kullanılmadığını" script-adı grep'iyle
  değil, `.meta` dosyasındaki GUID'i sahne/prefab dosyalarında arayarak
  doğrula. Script adı aynı olsa bile içeriği farklı iki dosya olabilir.
- Asset taşırken `git mv` kullan (içerik+`.meta` birlikte) — GUID korunur,
  referanslar kırılmaz.
- Windows'ta `core.ignorecase=true` — sadece büyük/küçük harf değişen bir
  rename tek adımda çalışmaz, geçici bir ara isim üzerinden iki adımda yapılmalı.
- `PrefabInstance`'ın `m_Modifications` içindeki fileID'ler kaynak prefab'ın
  kendi iç yapısına bağlı — bir prefab referansını text üzerinden repoint
  etmek riskli, böyle durumlar Editor GUI'ye bırakılır.
- **`UnityEvent` persistent call'lar (`m_TargetAssemblyTypeName`) sınıf adını
  GUID değil STRING olarak tutar** — bir sınıf/namespace yeniden
  adlandırılırsa, sahne dosyalarındaki bu referanslar da elle güncellenmeli
  (aksi halde buton/event'ler sessizce çalışmaz kalır).
- Belirsiz/riskli silmeler yerine proje kökündeki (Assets/ dışında)
  `_Yedek_Silinecekler/` klasörüne taşıma tercih edilir.
- Commit mesajlarına `Co-Authored-By` eklenmez (global kullanıcı tercihi).
- `HealPickup.prefab`'a dokunulmaz — kullanıcının kendi bekleyen/uncommitted
  değişikliği, her commit'te bilerek dışarıda bırakılıyor.
- Otomatik test altyapısı yok (`TECH_OVERVIEW.md` §22) — her önemli
  değişiklik sonrası kullanıcı Editor'de manuel test ediyor; kod tarafında
  yapılabilecek her şey yapılır, Editor-bağımlı doğrulama adımları açıkça
  "Editor'de şunu doğrula" diye not edilip kullanıcıya bırakılır.

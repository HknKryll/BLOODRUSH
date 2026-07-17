# BLOODRUSH — Hikaye ve Dünya Tasarım Dokümanı

> Bu doküman, BLOODRUSH oyununun hikaye, dünya, düşman ve seviye tasarımı kararlarını
> içerir. Amaç: bu bilgiyi okuyan bir geliştiricinin (insan ya da AI) projenin
> anlatısal iskeletini ve bunun oynanışla nasıl bağlandığını net şekilde anlaması.

## 1. Oyun Hakkında (Bağlam)

- **Tür:** ULTRAKILL esintili, retro tarzda, arcade hızlı FPS
- **Motor:** Unity 2022.3.62f3, HDRP
- **Hedef oynanış süresi:** ~1.5–2 saat (kısa, yoğun, tekrar oynanabilir bir deneyim)
- **Geliştirme:** İki kişilik ekip, GitHub üzerinden collaborative

## 2. Temel Öncül (Logline)

> Sistem seni "Seçilmiş" ilan etti. Ama sen seçilmedin — sadece sıradaydın.
> Senden önce gönderilenlerin hepsi öldü, ve sen onların üzerine kurulu bir
> yalanı taşıyorsun.

Karakter, büyük bir kahramanlık hikâyesinin merkezinde değil. **Bir kurban, bir piyon.**
Oyuncu oynadıkça bunu level level anlıyor; final'de bu gerçek tamamen açığa çıkıyor.

## 3. Dünya ve Kurumsal Yapı

### 3.1 Konsey (Sistem)
- Modern teknoloji çağında geçiyor (antik/mistik teknoloji YOK, zaman-katmanlı yapı YOK).
- Konsey, eskiden bir tür araştırma/askeri kurum olarak kurulmuş; artık daha çok
  kapalı kapılar ardında çalışan, gizliliğini koruyan bir kurum/loca gibi işliyor.
- Konsey düzenli olarak bir "Aday" seçiyor, eğitiyor ve göreve gönderiyor.
- **Kirli sır:** Gönderilen adayların hepsi ölüyor. Görev, fiziksel/mantıksal
  olarak neredeyse imkansız bir tehdide karşı kurulu. Konsey bunu biliyor ama
  adaylara söylemiyor.
- Adayların eğitildiği yer bir **müze/mezarlık hibriti**: önceki adayların
  silahları, zırhları, son sözleri sergileniyor — ilham kaynağı gibi sunuluyor,
  aslında bir uyarı.

### 3.2 Gerçek Düşman: Kaçmış Deney
- Konsey'in kendi geçmişte yürüttüğü bir deney/proje kontrolden çıkmış ve
  tesisin derinliklerine yerleşmiş/yayılmış.
- Konsey bunu resmi ordu ile temizletemiyor çünkü bu, kendi suçlarını itiraf
  etmek anlamına geliyor. Bu yüzden "gönüllü adaylar" gönderip bunu kutsal bir
  görev gibi sunuyorlar.
- Karakter aslında bir **temizlik işçisi** — "seçilmiş" değil, Konsey'in kendi
  kirini örtmek için harcanan biri.

### 3.3 Tema (Anlatının Omurgası)
"Seçilmişlik" bir yalan. Bu tema iki ayrı biçimde işlenebilir (ikisi de
tasarlandı, hangisinin kullanılacağına veya nasıl birleştirileceğine ekip
karar verecek — bkz. Ek A):

1. **"Kimse seni seçmedi, kendi kendine inandın"** — seçilmişlik efsanesi hiç
   resmi/gerçek bir sistem değil, halk arasında kendiliğinden oluşmuş bir
   inanç; karakter bunu kendi ihtiyacından ötürü benimsemiş.
2. **"Sen seçilmiş değildin, sadece sıradaydın"** *(ana hikâye için seçilen
   yön)* — seçilmişlik sistemi gerçek ve düzenli işliyor, ama "özel" olan
   kimse yok; herkes aynı kaderi paylaşıyor.

> Ana oyun **2. yönü** kullanıyor: modern teknoloji, gerçek bir Konsey, gerçek
> bir görev, gerçek bir düşman (kaçmış deney) — ama "seçilmişlik" duygusunun
> kendisi sahte.

## 4. Karakter

- Şu anki "Aday". Önceki adaylarla sürekli kıyaslanıyor (kalıntılar, notlar,
  ses kayıtları üzerinden — bkz. Bölüm 7).
- Kendini özel/farklı kanıtlamaya çalışıyor; oyun boyunca bunun boş bir çaba
  olduğunu (oyuncu önce, karakter sonra) anlıyor.
- **Suikastçı alt-teması — KARARLAŞTIRILDI (bkz. Bölüm 9):** görev, Konsey
  tarafından karaktere bir "hedefi nötralize etme" operasyonu ("Proje Atlas")
  olarak sunuluyor. Bu, Konsey'in resmi örtü hikâyesi. Gerçekte "hedef" diye
  bir şey yok, ya da hiç önemli değildi — asıl mesele kaçmış deneyin
  temizlenmesi. Oyun içi propaganda ekranları (bkz. `PresentationScreen.cs`)
  bu örtü hikâyesini karaktere/oyuncuya "başarı raporu" olarak sunar; final'de
  gerçek anlaşılır.

## 5. Final

- Klasik "kahraman kazanır" finali YOK.
- Karakter göreve gerçekten en güçlü/en özel olduğu için değil, **sistemin
  birini seçmek zorunda olduğu için** seçildiğini anlıyor.
- Görev "başarılabilir" ama final sahnesinde bir sonraki adayın zaten
  hazırlanmakta olduğu gösterilir — döngü kapanmıyor. (Uygulandı: bkz.
  `EndingSequence.cs` kapanış metni.)
- **Final boss kimliği — KARARLAŞTIRILDI (bkz. Bölüm 9):** kaçmış deneyin
  kendisi, en gelişmiş/nihai evrimi.

## 6. Silah Sistemi

| Tip | Kategori | Durum |
|---|---|---|
| Tabanca / Revolver | Ateşli silah | **Var.** `PlayerShoot.cs` — hitscan, namlu altı fırlatıcı (grenade/flash), havada mermi vurma kombosu. Ayrı bir "Tabanca" silahı yok — Revolver bu rolü de karşılıyor. |
| Shotgun | Ateşli silah | **Kod tarafı var** (`PlayerFirearm.cs` üzerinden `PlayerShoot.cs`'e eklendi — çoklu saçma/mesafeye göre hasar düşüşü). Silah görseli/animasyonu YOK — ekip model/animasyon ekleyene kadar mevcut silah mesh'i paylaşılıyor. |
| Hafif Makineli Tüfek (LMG) | Ateşli silah | **Kod tarafı var** (aynı sistem — hızlı ateş, geniş şarjör). Silah görseli/animasyonu YOK. |
| Bomba | Fırlatılabilir | Var. Alan hasarı. |
| Flash | Fırlatılabilir | Var. Kör etme/kontrol. |
| Kanca (Grappling Hook) | Hareket | Var. Mobilite, pozisyon değiştirme. |

**Kaldırılan mekanik:** "Anchor" (küp fırlatan mekanik) — açık alan
tasarımı olmadığı için gerekçesi kalmadığından projeden çıkarıldı.
*(Uygulandı: `ThrowableAnchor.cs`, `AnchorPrefab.prefab` silindi;
`PlayerShoot`/`GrapplingHook`/`WeaponHUD` içindeki referanslar temizlendi.)*

**Silah değiştirme:** Revolver/Shotgun/LMG arasında `1`/`2`/`3` tuşlarıyla
geçiş yapılıyor (bkz. `PlayerShoot.cs`). Bomba/Flash ayrı bir sistem
(namlu altı fırlatıcı, `Q` ile mod değişimi), kanca ayrı bir sistem (`E`).

## 7. Seviye Tasarımı

- **Yapı:** Tek, doğrusal, anıtsal bir güzergah. Tesis: **yeraltına gömülü,
  terk edilmiş bir askeri-endüstriyel üretim kompleksi** (KARARLAŞTIRILDI,
  bkz. Bölüm 9). `OutdoorsScene` bu tesisin harap/dış çevre girişi olarak
  düşünülebilir — oyuncu dışarıdan başlayıp yeraltına iniyor.
- **Anlatım yöntemi (diyalog minimum, çevresel anlatım öncelikli):**
  - Önceki adaylardan kalan kırık silahlar, bitmemiş mesajlar
  - Bazı bölümlerde önceki adayın "hayalet" versiyonu düşman olarak
    karşımıza çıkabilir (deneyin bir mutasyonu/çürümüş hali olarak
    gerekçelendirilebilir)
  - Duvar yazıları, kayıtlar, NPC'lerin kısa çelişkili tepkileri
  - **Not:** bu içeriklerin sahneye yerleştirilmesi (TutorialHint/DataTerminal
    metinleri, duvar yazıları) Unity Editor'de sahne üzerinde yapılacak bir iş —
    kod tarafından hazır değil, ekip tarafından level tasarımı sırasında
    eklenmeli.
- **Zorluk eğrisi = Hikâye eğrisi:** Geç levellerde daha çok önceki aday
  kalıntısı, daha çok "buraya kadar gelenler de yeterli değildi" hissi.
- **Bölümleme (1.5–2 saatlik oyun için 3 kabaca eşit dilim):**
  1. **İlk üçte bir:** Oyuncu mekanikleri öğreniyor. Temel piyade tipi
     düşmanlar (bkz. Bölüm 8, Tip 1) ve Konsey'in kendi muhafızları
     (Tip 3) tanıtılır.
  2. **Orta üçte bir:** Sıçrayıcı (Tip 2), Şişkin/Patlayıcı (Tip 4),
     Uzaktan Saldıran (Tip 5) tipleri eklenir; kombinasyonlar başlar.
  3. **Son üçte bir:** Tüm tipler karışık + 1 mini-boss + final boss.

## 8. Düşman Roster'ı

Kısa oyun için az sayıda ama birbirini tamamlayan, net siluetli tipler
tercih edildi (ULTRAKILL/DOOM tarzı okunabilirlik):

1. **Yürüyen Numuneler** — Deneyin ilk/başarısız aşamaları. Yarı-insan,
   yarı-mutasyon. Zayıf, sürü halinde, yakın-orta mesafe rush davranışı.
   Shotgun/tabanca ile karşılanmaya uygun.
   **Durum: Var** — `Enemy.prefab` (`EnemyAI`, behavior: Melee).

2. **Sıçrayıcılar** — Deneyin daha gelişmiş bir evrimi. Duvarlara sıçrayan,
   hızlı, oyuncuyu köşeye sıkıştırmaya çalışan tip. Kanca kullanımını
   (kaçış/pozisyon değiştirme) zorlayan bir tasarım amacı taşır.
   **Durum: Kod tarafı var** — `EnemyLeapBehavior.cs` + `EnemyAI.isJumper`
   toggle'ı eklendi (fizik tabanlı sıçrayış, orta menzilde tetiklenir).
   Model/animasyon YOK; `HighSpeedEnemy.prefab` mevcut mesh ile placeholder
   olarak kullanılabilir, ekip kendi model/animasyonunu ekleyince `isJumper`
   işaretlenip parametreler ayarlanmalı.

3. **Zırhlı Muhafızlar** — Deney DEĞİL, Konsey'in kendi "temizlik ekibi"
   (insan). Taktiksel davranır, siper alır, hafif makineli/tabanca
   kullanır. Deney canavarlarıyla aynı alanda bulunduğunda çapraz ateş
   dinamiği (bazen ikisi de oyuncuya saldırır, bazen birbirlerine)
   kurulabilir *(bu çapraz ateş dinamiği henüz kodda yok — EnemyAI şu an
   sadece oyuncuyu hedefliyor; ileride ayrı bir iş olarak ele alınmalı)*.
   **Durum: Var** — `ArmoredHazard.prefab` ("zırhlı", behavior: Ranged, 160 HP).

4. **Şişkin/Patlayıcı Tip** — Deneyin dengesiz bir mutasyonu. Yavaş
   hareket eder; yaklaşınca patlar ya da zehirli/asit alanı bırakır.
   Oyuncuyu geri çekilmeye ya da bomba kullanmaya zorlar.
   **Durum: Kod tarafı var** — `EnemyExplosive.cs` (yaklaşınca telegraph +
   patlama, ölünce de patlama, opsiyonel zehirli/asit alanı `HazardZone.cs`).
   Model/animasyon YOK; `Enemy2.prefab` (büyük/tanky mesh) placeholder olarak
   kullanılabilir — ekip modeli bulunca `EnemyExplosive` bileşenini prefabına
   eklemeli.

5. **Uzaktan Saldıran Tip** — Deneyin "biyolojik silah" işlevi gören bir
   parçası. Mesafeden mermi/asit/enerji fırlatır. Oyuncuyu sürekli
   hareket ettirmeyi ve siperden sipere geçmeyi teşvik eder; kanca burada
   da devreye girer.
   **Durum: Kısmen var** — `EnemyAI` behavior: Ranged bunu genel olarak
   karşılıyor, ama şu an Tip 3 (insan muhafız) ile aynı jenerik "Ranged"
   davranışını paylaşıyor; görsel/silüet farkı dışında ayrı bir mekanik
   ayrımı yok. Ayrı bir "biyolojik" ranged varyantı istenirse (örn. asit
   izi bırakan mermi) ayrı bir iş olarak ele alınmalı.

6. **Mini-Boss / Ara Aşama** (2–3 adet yeterli) — Deneyin "başarılı"
   sayılan gelişmiş evrimleri. Her biri farklı bir oynanış mekaniğini
   zorlamalı.
   **Durum: 1/2-3 var.** `Boss.prefab` + `BossAI.cs` kod yorumunda açıkça
   "Ch2 Boss" olarak işaretli — yani bu, Konsey'in insan+silah kullanan bir
   "denetçi" tipi mini-boss'u (pompalı + kabza melee + faz geçişinde
   karanlık/ışınlanma). **Final boss değil.** 1-2 mini-boss daha ve gerçek
   final boss (bkz. Bölüm 9) henüz projede yok — bunlar için model/tasarım
   gerekiyor, kod iskeleti bu doküman kapsamında oluşturulmadı.

## 9. Ekip İçin Açık Kararlar — ÇÖZÜLDÜ

Bu kararlar daha önce ekip tartışmasına açıktı; proje sahibiyle birlikte
netleştirildi (2026-07-17). İleride fikir değişirse burası güncellenmeli.

- [x] **Final boss:** deneyin kendisi (en gelişmiş/nihai evrimi). Gerekçe:
      mevcut `Boss.prefab` zaten "Konsey figürü" mini-boss slotunu
      (Ch2, insan+silah) dolduruyor; final'de gerçek düşmanla (kaçmış deney)
      yüzleşmek tematik "gerçek düşman" vurgusunu tamamlıyor ve iki boss
      arasında çeşitlilik sağlıyor. **Not:** final boss'un kendisi (model,
      AI, arena) henüz projede yok — bu sadece kimlik kararı, uygulama ayrı iş.
- [x] **Suikastçı/"yanlış hedef" alt-teması:** ana hikâyeye entegre edildi.
      "Hedef nötralize etme" (Proje Atlas), Konsey'in karaktere sunduğu resmi
      örtü hikâyesi; gerçekte mesele kaçmış deneyin temizlenmesi. Bu, oyunda
      zaten var olan `PresentationScreen.cs` "hedef nötralize" propaganda
      ekranlarıyla organik olarak örtüşüyor (bkz. Bölüm 4).
- [x] **Karakterin önceki adaylarla ilişkisi:** kişisel bir bağ YOK —
      tamamen bağımsız, tanımadığı biri. Gerekçe: "sen özel değilsin, sadece
      sıradaydın" temasını en güçlü şekilde bu kuruyor; kişisel/tanıdık bir
      ilişki teması yumuşatır. Önceki adaylarla bağ tamamen çevresel
      anlatımla (kalıntılar, kayıtlar) kuruluyor, karakter onları hiç tanımıyor.
- [x] **Tesisin somut kimliği:** yeraltına gömülü, terk edilmiş bir
      askeri-endüstriyel üretim kompleksi. `OutdoorsScene` bu tesisin
      harap dış/giriş bölümü olarak kullanılabilir.

## Ek A: Değerlendirilip Elenen/Bekletilen Konseptler

Referans için — ileride başka bir proje ya da genişleme için kullanılabilir:

- Ödünç alınan beden, unutma cezası, ikiz karar, terk edilmiş tanrının
  postu ve benzeri soyut/deneysel konseptler değerlendirildi ama ana
  hikâye için seçilmedi.
- "Vücut içi" (dev bir canlının içinde geçen) setting fikri açıkça
  reddedildi — kullanılmayacak.
- Cyberpunk: Edgerunners'ın "seçilmiş olduğunu sanan ama sonunda ölen
  karakter" temasından ilham alındı; bu, ana temanın çekirdeğini oluşturdu.

## Ek B: Bu Oturumda Yapılan Kod Değişiklikleri (2026-07-17)

Bu doküman ilk kez bu oturumda projeye eklendi (daha önce sadece harici bir
referans olarak vardı, repo'da dosya olarak bulunmuyordu). Doküman
doğrultusunda yapılan somut kod değişiklikleri:

- Anchor mekaniği tamamen kaldırıldı (`ThrowableAnchor.cs`, `AnchorPrefab.prefab`
  silindi; `PlayerShoot.cs`, `GrapplingHook.cs`, `WeaponHUD.cs` temizlendi).
- Anlatı metinleri Konsey/Aday temasına göre yeniden yazıldı:
  `IntroTextSequence.cs`, `EndingSequence.cs`, `PresentationScreen.cs`.
- Şişkin/Patlayıcı düşman tipi için `EnemyExplosive.cs` + `HazardZone.cs` eklendi.
- Sıçrayıcı düşman tipi için `EnemyLeapBehavior.cs` eklendi, `EnemyAI.cs`'e
  `isJumper` toggle'ı ve sıçrama tetikleyicisi wire edildi.
- Shotgun ve LMG, `PlayerFirearm.cs` (yeniden kullanılabilir hitscan silah
  sınıfı) üzerinden `PlayerShoot.cs`'e eklendi; `1`/`2`/`3` ile silah değişimi.
- `WeaponHUD.cs`'e aktif silah adı göstergesi eklendi.

**Henüz yapılmadı / ekip işi:** yeni düşman/silah türleri için 3B model ve
animasyon, final boss'un tam uygulaması (model + AI + arena), 2. ve 3.
mini-boss, çevresel anlatım içeriğinin (duvar yazıları, ses kayıtları, önceki
aday kalıntıları) sahnelere yerleştirilmesi, Zırhlı Muhafız ↔ deney çapraz
ateş dinamiği.

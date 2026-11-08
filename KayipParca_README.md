# Hatpiece: Unity projesi

Yarım kalmış bir yapbozun eksik parçasısın. Altı farklı mekânda yerini arıyorsun.
Tur bazlı, ızgara üzerinde oynanan bir bulmaca. Önce PC için, klavyeyle.

## Kurulum

1. Unity Hub'da bir URP projesi açın. Universal 2D ya da Universal 3D şablonu olabilir.
2. Bu paketteki `Assets` klasörünün içeriğini projenin `Assets` klasörüne kopyalayın.
3. Unity derlemeyi bitirince kurulum kendiliğinden yapılır: 2D ışık renderer'ı bağlanır, oyuncu prefabı (`Assets/Prefabs/KayipParca/Parca.prefab`) oluşturulur, `Assets/Scenes/KayipParca.unity` sahnesi kurulur. Play'e basmanız yeterli.
   Otomatik kurulum çalışmazsa üst menüden **Kayıp Parça > 3**, ardından **1** ve **2**'ye basın. Oyuncu prefabı silinirse **4** onu yeniden oluşturup sahnedeki LevelManager'a bağlar.

Bir ışık sahneyi etkilemiyorsa ışığın **Target Sorting Layers** ayarını "All" yapın.

## Kontroller

| Tuş | İş |
|---|---|
| WASD / Oklar | Bir kare ilerle (vinç modunda kancayı sür) |
| E / Q | Saat yönünde / tersine döndür |
| Boşluk | Bir tur bekle (vinç modunda: Yamalı'yı tut / bırak) |
| Z | Geri al |
| R | Seviyeyi yeniden başlat |
| Enter / Esc | Hikâye ekranında devam / geç |
| N / B | Hile: sonraki / önceki seviye |
| 1-9 | Hile: o numaralı seviyeye git |
| G | Hile: seviyeyi geçmiş say (kazanma, son ve bölüm ekranlarıyla) |
| Tab | Hile: seviye listesi, tıklayarak seç |

Hileler Editor'de ve build'de çalışır. `LevelManager > Cheats` kutusuyla kapatılır; oyunu dağıtmadan önce kapatın. Hikâye ekranındayken de seviye atlanabilir.

`LevelManager > Start Level` 1 ise oyun başlık ekranı ve giriş hikâyesiyle açılır. Başka bir sayı verirsen doğrudan o seviyeden başlar (test için).

## Hikâye

Kahraman, "Uyuyan Ejderha" yapbozunun binici parçası: ejderhanın sırtındaki eyerde oturur. Yamalı adında yabancı bir parça onu masadan itip eyere kuruluyor ve ejderhayı sürmeye başlıyor. Ejderha kahramanın önüne engeller koyup kaçıyor. Finalde oyuncu bir oyuncak vinçle Yamalı'yı eyerden söküyor ve iki sondan birini seçiyor. (Seviye dosyasındaki `CHEST=` ve koddaki `chest*` adları eski "göğüs" mekaniğinden kalmadır: kanat kalkınca eyer açılır.)

## Seviyeler (Bölüm 1: Ejderhanın Binicisi)

| # | Seviye | Mekanik |
|---|---|---|
| 1 | Kutunun Dibi | Hareket + döndürme |
| 2 | Halı Ormanı | Tek kullanımlık şekil istasyonları, toz canavarları |
| 3 | Mutfak Fayansları | Ayna klon |
| 4 | Oyuncak Kalesi | Devriye gezen çirkin parçalar, vinç kumandası bulunur |
| 5 | Çalışma Masası | Yanıp sönen lazerler (değersen parçalanırsın) |
| 6 | Gece Koridoru | Karanlık |
| 7 | Kedinin Yatağı | Kedi patileri (ara boss) |
| 8 | Resmin İçi | Ateş topu atan ejderha, vinç ve iki son |

Şekil istasyonları tek kullanımlıktır. Oyuncu ya da ayna klon bir kez kullanınca istasyon söner ve düz zemine döner. Geri al ve yeniden başlat söndürülen istasyonları geri getirir.

Tüm seviyeler `Tools/solver.py` ile çözülebilir olarak doğrulandı.

**Yeni bölüm eklemek:** `Levels/bolumler.txt` dosyasına yeni bir `BOLUM=Bölüm 2: ...` satırı ekleyip altına seviye dosyalarını yazmak yeterli.

## Görsel stil: siyah-beyaz doodle

Bölüm 1 siyah zemin üstünde beyaz çizimlerle oynanır. Zemin, duvar, engel, istasyon, lazer ve yuva doodle stilinde çizilir (`Tools/art_doodle.py`). Karakterler "blob" stilindedir (`Tools/art_blob.py`): tek parça tombul beyaz siluet, siyah nokta gözler, kontur yok (bacak yok: alt çıkıntıyla karışıyordu). Yapboz çıkıntıları (K D G B) blob üstünde yumuşak tümsekler olarak kalır; şapka sol üst köşede küçüktür, üst çıkıntıyı örtmez. Kahraman beyaz; kötü parçalar ve Yamalı ters renkte (siyah gövde, kalın beyaz kenar, beyaz gözler). Kayıp parça gri. Karakterler 2x çözünürlükte (64 px, PPU 64; ejderha 256x384), hikâye ekranları 1280x720 ve yumuşak filtreyle gelir, harita parçaları 32 px piksel olarak kalır (`KayipParcaTextureImporter` ayırır).

Arayüz de aynı mürekkeple: başlık, sayaç ve tuş kapakları Fredoka Bold, küçük etiketler Fredoka Medium, cümleler Nunito (piksel yazı tipi kaldırıldı), kontur yok. Hamle sayacı beyaz hap, kurtarılan parçalar gözlü halkalar, tuş ipuçları beyaz tuş kapakları (`KeyHints`: "[W][A][S][D] hareket" metnini kapaklara çevirir), orta mesaj beyaz konuşma balonu, hedef ikonları içi boş blob çerçeve (`Player/frame_*`). HUD `LevelManager.BuildHud` ile kurulur; Editor'de de çalıştığı için Play'e girmeden önizlenebilir.

- **Yuva:** kesintisiz çerçeve. Şekil uymuyorsa soluk gri, uyunca parlak beyaz olur ve nabız gibi parlayan bir ışık açılır.
- **Duvarlar:** 16 autotile (`wall_LRUD`), dış köşeler yuvarlak. Bazı duvarlarda temaya özel küçük bir işaret var (`wallm_`). İç köşeleri `Common/fillet_*` dolguları yuvarlatır.
- **Işık:** `ThemeManager > Monochrome` açıkken ışıklar beyaz, kamera arka planı siyah. Gece temasının karanlığı korunur. Kapatırsanız temaların renkli ışıkları geri gelir.
- Harita görselleri `Tools/art_doodle.py`, karakterler ve hikâye ekranları `Tools/art_blob.py` ile üretilir: `python3 Tools/art_blob.py .` (kontrol tablosu için `--sheet yol`). Renkli eski hâller git geçmişinde duruyor. Yamalı bölümü de aynı üreticiden çıkar; eski Yama Korsanları sahnesinin (`Art/B2`) görselleri renkli kaldı, o sahne oyunda değil.

## Kayıp parçalar ve 4 son

1, 2, 4, 5, 6 ve 7. seviyelerde yolunu kaybetmiş gri bir parça var. Üstüne basınca seni bir kare arkadan takip eder. Düşman, lazer ya da ateş ona değerse korkup kendi yerine kaçar. Onu yanında yuvaya ulaşırsan kurtarılmış olur. Kurtarmak isteğe bağlı ama daha fazla hamle ister. Sayaç sağ üstte görünür ve kaydedilir.

| Son | Koşul |
|---|---|
| Gizli son: Yeni Yamalı | Hiç parça kurtarılmadı |
| Yalnız Binici | 1-3 parça kurtarıldı |
| Binici Yerine Oturdu | En az 4 parça + Yamalı çöpe |
| Gerçek son: Kimse Eksik Kalmasın | En az 4 parça + Yamalı kendi kutusuna |

## Bölüm 2: Yamalı'nın İntikamı (platform)

Ayrı sahne: `Assets/Scenes/Yamali.unity`, kod `Scripts/KayipParca/Yamali/YamaliGame.cs`, seviyeler `Resources/KayipParca/Yamali/yamali1..8.txt`, metinler `Story/yamali.txt`. Bu kez Yamalı'yı oynarsın: çöp kutusundan çıkıp evde kaybolmuş yama parçalarını toplar, ordu kurar ve resme döner. Finalde Yamalı'nın ordusu ile Şapkalı'nın (Bölüm 1'de kurtardığı) parçaları karşılaşır; üç son vardır.

- **A / D:** yürü. **Boşluk:** zıpla (basılı tut, daha yükseğe). **S:** tek yönlü platformdan in. **R:** yeniden.
- Görseller blob stilinde ve 2x (`Tools/art_blob.py`, `Art/Yamali`, `Story/y_*`): Yamalı ters renkli blob + beyaz tombul bacaklar, silgi bekçisi, yuvarlak uçlu dikenler, tek yönlü platform, asansör, yay, bayrak, çıkış kapısı. Duvarlar Bölüm 1 temalarının doodle karolarıdır.
- HUD Bölüm 1 ile aynı dil: sol üstte bölüm + seviye adı, sağ üstte "Yama ordusu" halkaları (toplanan yama = siyah gövdeli beyaz gözlü top), altta ipucu ve tuş kapakları, seviye girişinde siyah perde.
- `Tools/art_yamali.py` eski doodle üreticidir, artık kullanılmıyor (üzerine yazmamak için çalıştırmayın).

## Eski Bölüm 2: Yama Korsanları (gerçek zamanlı, oyundan çıkarıldı)

Ayrı bir sahne: `Assets/Scenes/Bolum2.unity`. Sahne otomatik kurulur, kurulmazsa menüden **Kayıp Parça > 5) Bölüm 2 Sahnesini Kur**'a basın. Bölüm 1 bitince oyun bu sahneye kendiliğinden geçer. Doğrudan test etmek için Bolum2 sahnesini açıp Play'e basın. `Bolum2 > Start At` ile istediğiniz dalgadan ya da doğrudan boss'tan başlayabilirsiniz.

- **W / S:** şerit değiştir. **Boşluk:** ateş (basılı tutulabilir).
- **3 dalga:** Yama askerleri, şerit değiştiren balonlar (ok uyarısı), gülle atan gemiler (kırmızı şerit uyarısı).
- **Yama virüsü:** Sana değen yama +15, yakından patlattığın yama +5. Virüs seni güçlendirir: %25'te hızlı ateş, %50'de mor ve delici ateş, %75'te gülleleri vurabilir ve çift hasar verirsin.
- **Boss, Korsan Gemisi:** (1) zırhlı toplar ard arda gülle atar, (2) toplar ateş etmeden hemen önce vurulabilir, (3) Yama Kaptan: durduğu şerit güvenlidir, zayıf noktası göz yaması. Son vuruşta yaması ejderhaya yapışır ve Bölüm 3'e geçilir.
- Metinler: `Resources/KayipParca/Story/bolum2.txt`. Görseller: `Art/B2` (`Tools/art_bolum2.py` ile üretilir).

## Seviye dosyası biçimi

```
NAME=Çalışma Masası
THEME=6                 0 Kutu, 1 Halı, 2 Mutfak, 3 Oyuncak, 4 Gece, 5 Kedi, 6 Masa, 7 Resim
MOVES=32
SHAPE=1000              başlangıç şekli (K D G B kenarları, 1 = çıkıntı)
GOAL_G=1011             yuvanın istediği şekil (E = 2. yuva, g = klon yuvası, Y = ejderhanın kalbi)
ENEMY=1,7,14,7          x1,y1,x2,y2: ileri geri devriye
LASER=6,0,S,2,1,0       duvardaki kaynak x,y, yön, açık tur, kapalı tur, faz
FIRE=12,3,W,4,0         ateş topu çıkış karesi x,y, yön, kaç turda bir, faz
CHEST=1,4,3             ejderhanın göğsü: açık tur, döngü, faz
STORY= / HINT= / OUTRO= / END_T= / END_B= / END_ALONE= / END_DARK=   metinler (| = yeni satır)
MAP
#  duvar   .  zemin   ,  desenli zemin   o  engel   X  tehlike   S  istasyon (tek kullanımlık)
P  oyuncu  G/E  yuva  C  ayna klon  g  klon yuvası
D  ejderha  Y  kalp/Yamalı  K  vinç kumandası  H  kanca  T  çöp kutusu  B  Yamalı'nın kutusu
L  kayıp parça
```

## Dosyalar

```
Assets/Resources/KayipParca/
  Levels/level1..6.txt        Seviye haritaları (metin, kolayca düzenlenir)
  Art/Player/                 16 parça şekli, yüz animasyonu (4 kare + mutlu + ölü), şapka
  Art/Slots/                  16 yuva şekli (beyaz, oyunda boyanır)
  Art/Enemies/                Çirkin parça (4 kare), kedi patisi (4 kare)
  Art/Common/                 Şekil istasyonu, rota noktası
  Art/Themes/T0..T5/          Her tema: floor, floor_alt, wall, hazard, obstacle,
                              bg_far + bg_near (parallax, 640x360)
Assets/Scripts/KayipParca/    Oyun kodu + Editor/ kurulum menüsü
Assets/Prefabs/KayipParca/Parca.prefab   Oyuncu / ayna klon: gövde, yüz, şapka katmanları Inspector'dan bağlı
Previews/                     Seviye çizimleri (PNG)
Tools/solver.py               Seviye çözücü: python3 Tools/solver.py Assets/Resources/KayipParca/Levels
Tools/art.py                  Eski renkli görselleri üretir (artık kullanılmıyor): python3 Tools/art.py .
Tools/art_doodle.py           Harita parçaları, siyah-beyaz doodle
Tools/art_blob.py             Karakterler + hikâye ekranları, blob stili
Tools/render.py               Seviye önizlemelerini çizer: python3 Tools/render.py .
```

### Seviye dosyası biçimi

```
NAME=Kutunun Dibi
THEME=0            0-5
MOVES=20           hamle limiti
SHAPE=1000         başlangıç şekli
GOAL_G=0010        yuvanın istediği şekil (E = 2. son, g = klon yuvası)
ENEMY=1,7,14,7     x1,y1,x2,y2: düz çizgide ileri geri devriye
MAP
#  duvar   .  zemin   ,  desenli zemin   o  engel   X  tehlike
S  istasyon (tek kullanımlık)   P  oyuncu   G/E  yuva   C  ayna klon   g  klon yuvası
```

Yeni bir seviye ekledikten sonra `solver.py` ile çözülebilir olduğunu kontrol edin.

## Kapsanan görevler (2D + diğerleri)

Bu paketle şu görevler kapsanıyor:
- Tilemap (2)
- Parallax (2)
- 2D dinamik ışık (2)
- Ana karakter insan değil (1)
- Ana karakterde şapka (1)
- Flat stil (1)
- En az 5 asset en fazla 3 renk (1)
- 3+ kareli animasyon (1)
- Çirkin varlık (2)
- Tur bazlı oyun (2)
- Yansıma mekaniği (2)
- Boss (2)
- Birden fazla son (1)
- Intro yazısı
- Ayar menüsü: henüz yok.

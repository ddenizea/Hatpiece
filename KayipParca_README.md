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
| N | Sonraki seviye (sadece Editor'de) |

`LevelManager > Start Level` 1 ise oyun başlık ekranı ve giriş hikâyesiyle açılır. Başka bir sayı verirsen doğrudan o seviyeden başlar (test için).

## Hikâye

Kahraman, "Uyuyan Ejderha" yapbozunun kalp parçası. Yamalı adında yabancı bir parça onu masadan itip yerini kapıyor ve ejderhayı yönetmeye başlıyor. Ejderha kahramanın önüne engeller koyup kaçıyor. Finalde oyuncu bir oyuncak vinçle Yamalı'yı ejderhanın göğsünden söküyor ve iki sondan birini seçiyor.

## Seviyeler (Bölüm 1: Ejderhanın Kalbi)

| # | Seviye | Mekanik |
|---|---|---|
| 1 | Kutunun Dibi | Hareket + döndürme |
| 2 | Halı Ormanı | Şekil istasyonu, toz canavarları |
| 3 | Mutfak Fayansları | Ayna klon |
| 4 | Oyuncak Kalesi | Devriye gezen çirkin parçalar, vinç kumandası bulunur |
| 5 | Çalışma Masası | Yanıp sönen lazerler (değersen parçalanırsın) |
| 6 | Gece Koridoru | Karanlık |
| 7 | Kedinin Yatağı | Kedi patileri (ara boss) |
| 8 | Resmin İçi | Ateş topu atan ejderha, vinç ve iki son |

Tüm seviyeler `Tools/solver.py` ile çözülebilir olarak doğrulandı.

**Yeni bölüm eklemek:** `Levels/bolumler.txt` dosyasına yeni bir `BOLUM=Bölüm 2: ...` satırı ekleyip altına seviye dosyalarını yazmak yeterli.

## Kayıp parçalar ve 4 son

1, 2, 4, 5, 6 ve 7. seviyelerde yolunu kaybetmiş gri bir parça var. Üstüne basınca seni bir kare arkadan takip eder. Düşman, lazer ya da ateş ona değerse korkup kendi yerine kaçar. Onu yanında yuvaya ulaşırsan kurtarılmış olur. Kurtarmak isteğe bağlı ama daha fazla hamle ister. Sayaç sağ üstte görünür ve kaydedilir.

| Son | Koşul |
|---|---|
| Gizli son: Yeni Yamalı | Hiç parça kurtarılmadı |
| Yalnız Kalp | 1-3 parça kurtarıldı |
| Kalp Yerine Oturdu | En az 4 parça + Yamalı çöpe |
| Gerçek son: Kimse Eksik Kalmasın | En az 4 parça + Yamalı kendi kutusuna |

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
#  duvar   .  zemin   ,  desenli zemin   o  engel   X  tehlike   S  istasyon
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
Tools/art.py                  Tüm görselleri yeniden üretir: python3 Tools/art.py .
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
S  istasyon   P  oyuncu   G/E  yuva   C  ayna klon   g  klon yuvası
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

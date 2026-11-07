# Kayıp Parça: Unity başlangıç paketi

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
| WASD / Oklar | Bir kare ilerle |
| E / Q | Saat yönünde / tersine döndür (1 hamle) |
| Z | Geri al |
| R | Seviyeyi yeniden başlat |
| N | Sonraki seviye (sadece Editor'de, test için) |

## Kurallar

- Parçanın her kenarı ya **çıkıntı (1)** ya **girinti (0)**. Şekil `KDGB` sırasıyla yazılır: Kuzey, Doğu, Güney, Batı. Örnek: `1010`.
- **Yuva**ya sadece şeklin birebir uyuyorsa girebilirsin.
- **Şekil istasyonu**na girerken, öne bakan kenarın çıkıntıdan girintiye ya da tersine döner.
- **Tehlike** karesine basarsan ölürsün. Hamlen biterse de ölürsün.
- Duvara çarpmak hamle harcamaz.
- **Düşmanlar** her hamlede bir kare devriye atar. Rotaları noktalarla gösterilir.
- **Ayna klon** (3. seviye) yatayda tersine hareket eder ve tersine döner. İkiniz de yuvanızdayken kazanırsın.

## Seviyeler

| # | Tema | Yeni mekanik | Işık / ses hissi |
|---|---|---|---|
| 1 | Kutunun Dibi (karton) | Hareket + döndürme | Sıcak sabah ışığı |
| 2 | Halı Ormanı (kanepe altı) | Şekil istasyonu, toz topu | Loş gün batımı |
| 3 | Mutfak Fayansları | Ayna klon (yansıma görevi) | Soğuk, parlak floresan |
| 4 | Oyuncak Kalesi | Devriye gezen çirkin parçalar | Aydınlık öğle |
| 5 | Gece Koridoru | Karanlık, sadece senin ışığın | Çok karanlık, mavi gece |
| 6 | Kedinin Yatağı (boss) | Kedi patileri, **iki son** | Kırmızı, nabız gibi atan ışık |

Son seviyede iki yuva var:
- **Beyaz yuva (`1111`)**: "Yerleştin", 1. son.
- **Yeşil yuva (`0000`)**: "Kendi kutuna döndün", 2. son.

Tüm seviyeler bir çözücüyle test edildi. En kısa çözümler sırasıyla 15, 18, 20, 20, 29 hamle. Son seviyede ise 9 ve 21 hamle.

## Işık ve ses geçişi

`Oyun` objesindeki **ThemeManager**'da her tema için şu ayarlar var: global ışık rengi ve şiddeti, oyuncu ışığı, nabız, kamera rengi, **müzik** ve **ambiyans** klibi.
Seviye bitince eski tema kararır ve müzik kısılır. Ardından yeni tema yeni renk ve müzikle açılır.
Sesleri hazırladığınızda ilgili temaya sürükleyip bırakmanız yeterli. Efekt sesleri (hareket, döndürme, istasyon, çarpma, ölüm, oturma, kazanma) ise **LevelManager**'da duruyor. Hepsi boş bırakılabilir.

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

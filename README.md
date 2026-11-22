# ROTA — Lojistik Simülatörü

Yol çizme türünde bir lojistik oyunu. Depolardan mağazalara sınırlı yol karosuyla ağ kuruyorsun, kamyonlar rotayı kendileri buluyor. Talep her hafta artıyor ve bir mağazanın siparişleri taşarsa oyun bitiyor. Görünüm SimCity havasında bir minyatür şehir: PBR dokular, HDRI gökyüzü, yumuşak gölgeler, SSAO ve tilt-shift alan derinliği.

Unity **6000.6.10f1**, URP 17.6, Input System, Cinemachine 3, UI Toolkit.

## Oynamak

`Assets/_Project/Scenes/Main.unity` sahnesini açıp Play'e bas.

| Girdi | İşlev |
|---|---|
| Sol tık + sürükle | Yol çiz (suya girince köprü kendiliğinden tamamlanır) |
| Sağ tık + sürükle | Yol sil (karo iade edilir) |
| R / T / H | Yol / göbek kavşak / otoyol aracı |
| WASD, oklar, orta tık sürükle | Kamerayı kaydır |
| Tekerlek · Q / E | Yakınlaş · 90° döndür |
| Boşluk · 1 / 2 · Esc | Duraklat · hız · duraklatma menüsü |

## Kurallar

- **Depolar:** 2×2 boyutundadır ve renkleri yük türünü gösterir (Gıda, Elektronik, Yakıt, Tekstil, İnşaat).
- **Mağazalar:** Kendi renklerinde sipariş üretir. Bekleyen siparişler binanın üstünde küp olarak görünür.
- **Taşma:** Bekleyen sipariş sayısı 5'e ulaşınca mağazanın çevresinde bir halka dolmaya başlar. Halka dolarsa oyun biter.
- **Kamyon kapasitesi:** Her yeni mağaza, kendi rengindeki en yakın depoya bir kamyon getirir.
- **Trafik:** Kavşaklara aynı anda tek kamyon girer, göbek kavşakta bu sayı üçtür.
- **Haftalık yükseltme:** Her hafta sonunda +20 yol karosu gelir ve iki karttan biri seçilir: köprü, göbek kavşak, otoyol, ek kamyon veya +15 yol.
- **Denge:** Bütün değerler `Assets/_Project/Data/GameConfig.asset` içinde.

## Proje yapısı

```
Assets/_Project/
  Scripts/Runtime/   Logistics.Runtime
    Simulation/World.cs       MonoBehaviour'suz simülasyon (test edilebilir)
    Grid/ Roads/ Pathfinding/ Traffic/ Economy/ Spawning/ Upgrades/
    Visuals/                  Prosedürel mesh: yol, köprü, otoyol, bina, ağaç, kamyon
    CameraRig/ UI/ Audio/ Core/
    DebugTools/               Doğrulama yardımcıları (yalnızca Editor/Debug)
  Scripts/Editor/    Materyal, render ve sahne kurulumu
  Tests/EditMode/    Simülasyon testleri
  UI/                Game.uxml, Game.uss, PanelSettings, font
Assets/ThirdParty/   Poly Haven dokuları + HDRI (CC0), Barlow fontu (OFL) — bkz. CREDITS.md
```

Bütün renkler `Core/Palette.cs` dosyasından gelir (art bible).

## Yeniden üretme

Materyaller, render ayarları ve sahne kodla üretilir. Elle düzenlemek yerine şu menüleri sırayla çalıştır:

1. **Tools ▸ Logistics ▸ 1 Build Materials:** PBR materyaller, URP maske dokuları, su normal haritası.
2. **Tools ▸ Logistics ▸ 2 Configure Rendering and UI Assets:** URP kalitesi, SSAO, post-processing, HDRI gökyüzü, font asset, PanelSettings.
3. **Tools ▸ Logistics ▸ 3 Build Main Scene:** Main sahnesini baştan kurar.

## Testler

Test Runner'da EditMode altındaki `Logistics.Tests` testlerini çalıştır. Kapsananlar:
- Yol maskeleri ve bütçe.
- A* yol bulma.
- Köprü.
- Rota geometrisi.
- Teslimat döngüsü.
- Taşma ve oyun sonu.
- Kilitlenmeme.

## Bilinen sınırlar

- **Yollar:** Yalnızca 4 yönlü, çapraz yol yok.
- **Kapsam:** Gece/gündüz döngüsü, kayıt sistemi ve trafik ışığı yok.
- **Ses:** Kodla sentezleniyor. Müzik ve efekt seviyeleri statik bir kazanç katmanıyla yönetiliyor. `MusicVol` ve `SFXVol` parametrelerini dışa açan bir AudioMixer, Editor arayüzünde yapılacak bir sonraki adım.
- **Geometri:** Prosedürel. Daha fotogerçekçi bir sonuç için Asset Store'daki bina ve araç modelleri `BuildingFactory` ve `TruckFactory` yerine bağlanabilir.

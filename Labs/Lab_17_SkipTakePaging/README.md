# Lab_17 — Skip/Take Sayfalama

İlgili yazı: [#17 — Skip/Take Sayfalama — Sayfa 500'de Veritabanı 10.000 Satır Okur, Size 20 Verir](https://blog.furkantural.com/Home/Post/17)

## İddia

Sayfa 500 ile sayfa 1 istemciye aynı 20 satırı gönderir. Ölçülebilir fark iki yerdedir:
sayfalamanın **nerede** yapıldığı, ve liste kaydığında kullanıcının kaç **farklı** satır
gördüğü.

## Kanıt

```text
Senaryo                        Satır  Beklenen      Süre     Bellek  Sonuç
────────────────────────────  ──────  ─────────  ───────  ─────────  ─────
1) Bellekte sayfalama         10.000  = 10000     158 ms    10,2 MB  GEÇTİ
2) Offset, sayfa 1                20  = 20         13 ms     209 KB  GEÇTİ
3) Offset, sayfa 500              20  = 20          7 ms     128 KB  GEÇTİ
4) Keyset, aynı derinlik          20  = 20         20 ms     233 KB  GEÇTİ
5) Offset — liste kaydığında      39  = 39        219 ms     1,3 MB  GEÇTİ
6) Keyset — liste kaydığında      40  = 40         31 ms     361 KB  GEÇTİ
```

1–4'te sayı veritabanından çıkan satırdır; 5–6'da iki sayfada kullanıcıya gösterilen
**benzersiz** satırdır.

Okuma notları:

- **3, yazının başlığındaki "size 20 verir" kısmını doğrular.** "10.000 satır okur"
  kısmı ise ölçülemez: atılan 9.980 satır sunucunun içinde kalır, ağdan geçmez. 10.000
  satırlık bir tabloda süreye de yansımaz — offset'in bedeli tablo büyüdükçe doğar.
  Bu laboratuvarın kanıtladığı şey maliyetin büyüklüğü değil, **satır sayısının aynı
  kalması**; yani "yavaşladı mı" sorusunun cevabı istemci tarafındaki sayıda yok.
- **4'te 21 satır istendi, 20 geldi.** Sayfa 500 son sayfa. `pageSize + 1` çekmek
  "devamı var mı" sorusunu ayrı bir `COUNT` sorgusu olmadan cevaplar — burada cevap yok.
- **5, offset'in az bilinen kusuru.** Sayfa 1 okunduktan sonra listenin başına bir kayıt
  giriyor; sayfa 2 sabit bir sıra numarasına güvendiği için 40 satır gösteriliyor ama
  39'u farklı. Bir kayıt iki kez göründü, bir kayıt hiç görünmedi. Sonsuz kaydırmada
  "arada bir tekrar eden kart" olarak bildirilen hatanın kök nedeni budur.
- **6, aynı senaryoda keyset'in bozulmadığını gösterir.** Devam noktası sıra numarası
  değil satırın kendisi olduğu için araya giren kayıt sırayı kaydırmıyor: 40 satır, 40'ı
  farklı.

Keyset'in bedeli de görünüyor: 500. sayfanın devam noktası **ölçümün dışında** hazırlandı,
çünkü keyset rastgele sayfaya atlamayı desteklemez. Gerçek bir API'de bu değer önceki
sayfanın son satırından gelir.

## Çalıştır

```powershell
dotnet user-secrets set "ConnectionStrings:LabsConnection" "<baglanti-dizesi>" --project .
dotnet run
```

## Neden bu sayılar sabit

Tohum 10.000 blog içerir ve `PageSize = 20`; yani tam 500 sayfa. Sayfa 500'ün `Skip`
değeri 9.980, yazının başlığındaki sayı da buradan gelir. 5 ve 6. senaryolardaki ekleme
transaction içinde yapılıp geri alınır: kayıt gerçekten eklenir, aynı bağlantıdan
görünür, sonra silinir — diğer laboratuvarların saydığı 10.000 satır bozulmaz.

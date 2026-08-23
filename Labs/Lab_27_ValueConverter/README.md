# Lab_27 — Value Converter

İlgili yazı: [#27 — Value Converter](https://blog.furkantural.com/Home/Post/27)

## İddia

Aynı 30 kaydın durumu **iki sütunda birden** duruyor: `DurumSayi` converter'sız (int),
`DurumMetin` ise `HasConversion<string>()` ile (nvarchar). Değerler aynı, satırlar aynı,
sorgular aynı.

Ölçülen şey sorgunun döndürdüğü satır sayısı. Değişen tek şey değerin diskte hangi tiple
yattığı.

## Kanıt

```text
Senaryo                                          Satır  Beklenen      Süre     Bellek  Sonuç
──────────────────────────────────────────────  ──────  ─────────  ───────  ─────────  ─────
1) int sütun, eşitlik (== Yayinda)                  10  = 10        365 ms     4,4 MB  GEÇTİ
2) metin sütun, eşitlik (== Yayinda)                10  = 10         43 ms     1,1 MB  GEÇTİ
3) int sütun, aralık (>= Yayinda)                   20  = 20        139 ms     365 KB  GEÇTİ
4) metin sütun, aralık (>= Yayinda)                 10  = 10         52 ms     375 KB  GEÇTİ
5) int sütun, sıralamanın ilk 10'unda Taslak        10  = 10         43 ms     393 KB  GEÇTİ
6) metin sütun, sıralamanın ilk 10'unda Taslak       0  = 0          36 ms     388 KB  GEÇTİ
7) metin sütun, Contains ile küme                   20  = 20         65 ms     782 KB  GEÇTİ
8) int sütun, Contains ile küme                     20  = 20         34 ms     399 KB  GEÇTİ
```

Enum'un iki sırası bilerek birbirini tutmuyor: sayıya göre `Taslak(0) < Yayinda(1) <
Arsiv(2)`, harfe göre `Arsiv < Taslak < Yayinda`. Ölçümün tamamı bu iki sıranın farkı.

Okuma notları:

- **1 ve 2, converter'ın sorunsuz göründüğü yer.** Eşitlik iki saklamada da aynı 10 kaydı
  buluyor. Converter değeri `N'Yayinda'`ya çevirip karşılaştırıyor; kod tarafında hiçbir
  fark hissedilmiyor. Bir alanı metne çevirme kararı genelde burada verilir — ve burada
  hiçbir bedeli yoktur.
- **3, iş kuralının doğru cevabı.** "Yayınlanmış ve sonrası" 20 kayıt. Sayısal sırada
  `Yayinda` 1, `Arsiv` 2; ikisi de `>= 1`.
- **4, tablonun sebebi.** Birebir aynı C# ifadesi, yarısı kadar sonuç. `nvarchar` sütunda
  `>=` alfabetik karşılaştırır ve `'Arsiv' < 'Yayinda'` olduğu için arşiv kayıtları kümenin
  dışında kalır. Ne hata var ne uyarı; sorgu başarıyla çalışıp yanlış cevabı döndürüyor.
- **5 ve 6, aynı kayma sıralamada.** Sayısal sıranın başı `Taslak(0)`, alfabetik sıranın
  başı `'Arsiv'`. İlk 10 kayıtta int sütunda 10 taslak var, metin sütunda hiç yok. Sayma
  her iki senaryoda da **int sütundan** yapılıyor, yani ölçülen şey sıralamanın kendisi.
- **7 ve 8, sıraya bakmayan yazım.** Aynı küme `Contains` ile arandığında iki saklamada da
  20 dönüyor. Küme üyeliğinin sıralamayla işi yok; `IN` listesi converter'ın alfabetik
  sırasını devreye sokmuyor.

## Çalıştır

```powershell
dotnet run
```

Veritabanı gerekir; kurulum için depo kökündeki README'ye bak.

## Neden bu sayılar sabit

Beklentiler ölçümden değil **kararın nerede verildiğinden** geliyor ve koda gömülü:
karşılaştırma da sıralama da sunucuda, sütunun tipine göre yapılır — C#'taki enum sırasına
göre değil. Dolayısıyla:

- eşitlik değeri çevirip arar, sıraya bakmaz → 10
- aralık int sütunda sayısal sıraya bakar → 20
- aralık nvarchar sütunda alfabetik sıraya bakar → 10
- sıralamanın başı int'te `Taslak`, metinde `Arsiv` → 10 ve 0
- küme üyeliği sıraya hiç bakmaz → iki tipte de 20

Sekizi de ilk koşuda tuttu.

**4. senaryo istemcide değerlendirilmiyor.** Üretilen SQL şu:

```sql
SELECT COUNT(*) ... WHERE [l].[DurumMetin] >= N'Yayinda'
```

Kanıt sayının kendisinde de var: karşılaştırma belleğe çekilip C# tarafında yapılsaydı
enum'un sayısal sırası geçerli olur ve sonuç 20 çıkardı. 10 çıkması, karşılaştırmanın
SQL Server'da ve harf sırasına göre yapıldığı anlamına gelir.

`Contains` senaryolarının SQL'i `IN (N'Yayinda', N'Arsiv')` değil, parametreli
`IN (@p1, @p2)` biçiminde üretilir. Sonucu değiştirmez ama tablodaki 20'nin sabit
literal'den gelmediğini not etmek gerekir.

Alfabetik sıra collation'a bağlıdır. Buradaki üç ad (`Arsiv`, `Taslak`, `Yayinda`) hem
`SQL_Latin1_General_CP1_CI_AS` hem Türkçe collation'larda aynı sırayı verdiği için ölçüm
sunucu ayarından bağımsızdır — ama bu bir tesadüf, kural değil: enum adları başka
harflerle başlasaydı sıra collation'a göre kayabilirdi.

Ölçüm tablosu paylaşılan tohum verisine dokunmuyor. `Lab27_Kayitlar` tablosu her senaryonun
kendi transaction'ında `CREATE TABLE` ile doğuyor, fikstür oraya yazılıyor ve senaryo
bitince rollback ile birlikte tablo da kayboluyor. Entity paylaşılan `LabsDbContext`'e
değil, yalnız bu laboratuvara ait bir türeve tanımlı; model önbelleği context tipine göre
ayrıldığı için öteki laboratuvarların modeli değişmiyor.

Kayıtlar `Taslak, Yayinda, Arsiv, Taslak, …` şeklinde serpiştirilerek ekleniyor: Id sırası
durum sırasıyla aynı olmasın ki 5 ve 6. senaryolardaki `ORDER BY` ölçümü eklenme sırasının
tesadüfünden değil sütunun kendisinden gelsin.

Süre ve bellek sütunları denetlenmez ve yazıda kullanılmaz. 1. senaryo ilk koşan senaryo
olduğu için EF'in model kurulumunu üstleniyor (365 ms, 4,4 MB) — bu sayı senaryonun
maliyeti değil, ölçüm sırasının. Denetlenen tek sayı dönen satır adedidir.

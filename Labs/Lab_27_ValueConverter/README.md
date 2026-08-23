# Lab_27 — Value Converter

İlgili yazı: [#27 — Value Converter](https://blog.furkantural.com/Home/Post/27)

## İddia

Aynı 30 kaydın durumu **iki sütunda birden** duruyor: `DurumSayi` converter'sız (int),
`DurumMetin` ise `HasConversion<string>()` ile (nvarchar(20)). Değerler aynı, satırlar aynı,
sorgular aynı.

Ölçülen şey sorgunun döndürdüğü satır sayısı. Değişen tek şey değerin diskte hangi tiple
yattığı.

## Kanıt

```text
Senaryo                                          Satır  Beklenen      Süre     Bellek  Sonuç
──────────────────────────────────────────────  ──────  ─────────  ───────  ─────────  ─────
1) int sütun, eşitlik (== Yayinda)                  10  = 10        392 ms     4,4 MB  GEÇTİ
2) metin sütun, eşitlik (== Yayinda)                10  = 10         42 ms     1,1 MB  GEÇTİ
3) int sütun, aralık (>= Yayinda)                   20  = 20        138 ms     366 KB  GEÇTİ
4) metin sütun, aralık (>= Yayinda)                 10  = 10         51 ms     373 KB  GEÇTİ
5) int sütun, sıralamanın ilk 10'unda Taslak        10  = 10         43 ms     393 KB  GEÇTİ
6) metin sütun, sıralamanın ilk 10'unda Taslak       0  = 0          36 ms     390 KB  GEÇTİ
7) metin sütun, Contains ile küme                   20  = 20         69 ms     783 KB  GEÇTİ
8) int sütun, Contains ile küme                     20  = 20         35 ms     399 KB  GEÇTİ
9) metin sütun, tanınmayan değer                     0  = 0          69 ms     482 KB  GEÇTİ
10) int sütun, tanınmayan değer                     30  = 30         41 ms     314 KB  GEÇTİ
```

Enum'un iki sırası bilerek birbirini tutmuyor: sayıya göre `Taslak(0) < Yayinda(1) <
Arsiv(2)`, harfe göre `Arsiv < Taslak < Yayinda`. 1–8. senaryolar bu iki sıranın farkını
ölçüyor; 9 ve 10 saklanan değerin kendisi tanınmadığında ne olduğunu.

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
- **9, sessizlik burada bitiyor.** Sütuna enum'da karşılığı olmayan bir değer yazıldığında
  EF onu varsayılana düşürmüyor: okuma anında `InvalidOperationException` fırlatıyor —
  *Cannot convert string value 'Arsivlendi' from the database to any value in the mapped
  'Durum' enum.* 30 satırın yalnız 10'u bozuk ama kısmi sonuç diye bir şey yok; sağlam 20
  satır da elde edilemiyor.
- **10, aynı bozulmanın sayısal karşılığı.** `DurumSayi = 99` hiçbir hata üretmiyor, 30
  satırın hepsi geliyor. Enum'un temel tipi int ve C# tanımsız değerleri taşımaya izin
  veriyor. İki saklama biçiminin kırılganlığı zıt yönde: metin gürültülü patlıyor, sayı
  sessizce geçiriyor.

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
- metinden enum'a çeviri ad eşleşmesi ister; eşleşme yoksa materyalizasyon düşer → 0
- int'ten enum'a çeviri yoktur, değer olduğu gibi taşınır → 30

Onu da ilk koşuda tuttu. 9 ve 10 tek istisna: beklentileri "sessizce varsayılana düşer"
tahminiyle yazılmış, ölçüm bunu çürütmüştü — düzeltilen beklenti değil, **tahmindi**;
senaryolar ölçümün gösterdiği davranışa göre yeniden kuruldu.

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

Alfabetik sıra sütunun collation'ına bağlıdır ve **ölçüm tek bir collation altında
yapılmıştır**. Buradaki üç ad farklı harflerle başladığı için sonucun yaygın collation'lar
arasında değişmesi beklenmez, ama bu ölçülmedi; enum adları birbirine yakın ya da Türkçe
karaktere dayanan adlar olsaydı sıranın sunucu ayarına bağlı hale gelmesi mümkündür.

Ölçüm tablosu paylaşılan tohum verisine dokunmuyor. `Lab27_Kayitlar` tablosu her senaryonun
kendi transaction'ında `CREATE TABLE` ile doğuyor, fikstür oraya yazılıyor ve senaryo
bitince rollback ile birlikte tablo da kayboluyor. Entity paylaşılan `LabsDbContext`'e
değil, yalnız bu laboratuvara ait bir türeve tanımlı; model önbelleği context tipine göre
ayrıldığı için öteki laboratuvarların modeli değişmiyor.

Kayıtlar `Taslak, Yayinda, Arsiv, Taslak, …` şeklinde serpiştirilerek ekleniyor: Id sırası
durum sırasıyla aynı olmasın ki 5 ve 6. senaryolardaki `ORDER BY` ölçümü eklenme sırasının
tesadüfünden değil sütunun kendisinden gelsin.

Süre ve bellek sütunları denetlenmez ve yazıda kullanılmaz. 1. senaryo ilk koşan senaryo
olduğu için EF'in model kurulumunu üstleniyor (392 ms, 4,4 MB) — bu sayı senaryonun
maliyeti değil, ölçüm sırasının. Denetlenen tek sayı elde edilen satır adedidir.

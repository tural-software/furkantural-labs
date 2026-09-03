# Lab_28 — Optimistic Concurrency

İlgili yazı: [#28 — Optimistic Concurrency](https://blog.furkantural.com/Home/Post/28)

## İddia

Tek satırlık bir tablo, bir görüntülenme sayacı: `Deger` başlangıçta 0. **On yazar** aynı
satırı okuyor, her biri sayacı bir artırıp kaydediyor. Okumalar hep yazmalardan önce
bitiyor; yani her yazar 0 görüyor. Web'de iki isteğin aynı kaydı düzenlemesi tam olarak bu
sıralamadır: her istek kendi context'iyle okur, sonra yazar.

Ölçülen şey işin sonunda veritabanındaki sayaç değeri. Doğru cevap 10. Değişen tek şey
EF'in `UPDATE`'in `WHERE` kısmına ne yazdığı ve çakışma yakalandığında kodun ne yaptığı.

## Kanıt

```text
Senaryo                                         Sayaç  Beklenen      Süre     Bellek  Sonuç
─────────────────────────────────────────────  ──────  ─────────  ───────  ─────────  ─────
1) Token yok, son yazan kazanır                     1  = 1         282 ms     7,4 MB  GEÇTİ
2) rowversion, çakışmada vazgeç                     1  = 1          89 ms     4,3 MB  GEÇTİ
3) rowversion, çakışmada istemci kazanır            1  = 1         105 ms     2,3 MB  GEÇTİ
4) rowversion, çakışmada yeniden oku ve artır      10  = 10         71 ms     1,8 MB  GEÇTİ
5) Token Deger sütunu, yeniden oku ve artır        10  = 10        100 ms     4,6 MB  GEÇTİ
6) Token Ad sütunu, yeniden oku ve artır            1  = 1          59 ms     3,9 MB  GEÇTİ
7) ExecuteUpdate, artış sunucuda                   10  = 10         45 ms     1,1 MB  GEÇTİ
```

Okuma notları:

- **1, tuzağın kendisi.** `UPDATE ... WHERE Id = @p`. Her yazar 0 okudu, 1 yazdı; hiçbiri
  hata almadı, hiçbir satır "etkilenmedi" demedi. On artıştan biri kaldı, dokuzu ne log'a
  düştü ne istisnaya. Üretimde bu satır bir sayaç değil de bir formsa, kaybolan şey öteki
  kullanıcının düzenlemesidir.
- **2, aynı kayıp, artık görünür.** `WHERE Id = @p AND Surum = @okunan`. İlk yazar geçiyor,
  SQL Server satırın `rowversion`'ını ilerletiyor, kalan yazarların koşulu tutmuyor: sıfır
  satır etkileniyor ve EF `DbUpdateConcurrencyException` fırlatıyor. Sayaç yine 1 —
  rowversion kaybı **haber verir**, geri almaz. Yakalayıp vazgeçen kod kaybı kabullenmiştir.
- **3, belgelerdeki "client wins" kalıbı.** Çakışmada `OriginalValues` veritabanının güncel
  hâline çekiliyor, elindeki değer yazılıyor. Elindeki değer 0+1, yani 1. SQL log'unda
  çakışan her yazar için `UPDATE` → `SELECT` → `UPDATE` gidiyor ve ikinci `UPDATE` başarılı:
  çakışma çözüldü, artış yine kayıp. Kalıp, kullanıcının **gördüğü** alanları korumak için
  var; hesaplanmış bir değer için değil.
- **4, çözüm.** `Reload` elindeki kopyayı veritabanıyla eşitliyor, artış taze değerin
  üstüne biniyor, kayıt tekrar deneniyor. Sayaç 10. Çözümün adı "çakışmayı çöz" değil
  "işi yeniden yap": okunan değer bayatladıysa hesap da bayatlamıştır.
- **5, yeni sütun gerekmiyor.** `Deger` üstüne `IsConcurrencyToken()`; EF `WHERE`'e
  `AND Deger = @okunan` ekliyor. Aynı çakışma aynı yerde yakalanıyor çünkü değişen sütun
  tam olarak o. Şema değiştiremediğiniz tablolarda seçenek bu.
- **6, token yanlış sütunda.** Aynı yeniden deneme kodu, aynı `catch`; hiç tetiklenmiyor.
  `Ad`'ı kimse değiştirmediği için `WHERE ... AND Ad = @okunan` her seferinde tutuyor.
  Property token yalnız kendi sütununu izler; `rowversion` ise satırın tamamını. Token
  "var" olmak korumak değildir.
- **7, döngüyü hiç kurmamak.** `SET Deger = Deger + 1`. Eski değer istemciye hiç gelmediği
  için bayatlayacak bir şey yok; token da yeniden deneme de gerekmiyor. Sayaç, stok,
  bakiye gibi **hesaplanan** alanlar için doğru araç bu; formdan gelen alanlar için değil.

## Çalıştır

```powershell
dotnet run
```

Veritabanı gerekir; kurulum için depo kökündeki README'ye bak.

## Neden bu sayılar sabit

Beklentiler ölçümden değil **`UPDATE`'in `WHERE` kısmından** geliyor ve koda gömülü. On
yazarın hepsi yazmadan önce okuduğu için hepsinin elinde 0 var; sonrası koşulun neyi
karşılaştırdığına bağlı:

- `WHERE Id = @p` her seferinde tutar → herkes 1 yazar → 1
- `WHERE ... AND Surum = @okunan` ilk yazardan sonra tutmaz; vazgeçen yazar hiç yazmaz → 1
- aynı koşul, "istemci kazanır": koşul düzeltilir, elindeki 1 yazılır → 1
- aynı koşul, yeniden oku ve artır: her çakışma taze değerle tekrar denenir → 10
- `WHERE ... AND Deger = @okunan` aynı çakışmayı yakalar → 10
- `WHERE ... AND Ad = @okunan` hep tutar, çakışma hiç görülmez → 1
- `SET Deger = Deger + 1` istemcideki değeri hiç kullanmaz → 10

Yedisi de ilk koşuda tuttu. `WHERE` şekilleri SQL log'undan doğrulandı: token'sız
context yalnız `[Id]`, rowversion'lı context `AND [Surum]`, property token'lı context'ler
`AND [Deger]` ve `AND [Ad]` yazıyor; 3, 4 ve 5. senaryolarda çakışan her yazar bir
`SELECT TOP(1)` ile satırı yeniden okuyor, 6. senaryoda bu okuma hiç yok.

**Yazarlar aynı bağlantıyı ve aynı transaction'ı paylaşıyor.** Bu bir kısayol değil, ölçümün
tanımı: okuma-değiştir-yaz kaybı bağlantı sayısından ya da izolasyon seviyesinden değil,
**okumanın yazmadan önce bayatlamasından** doğar. Her yazar kendi `DbContext`'ini kuruyor
(`UseSqlServer(baglanti)` + `UseTransaction`), satırı kendisi okuyor ve kendi kopyasını
kaydediyor — tıpkı on ayrı web isteği gibi. Sıralama aynı olduğu sürece sonuç aynı.

`Surum` sütunu **her senaryoda** tabloda var ve SQL Server onu her `UPDATE`'te ilerletiyor.
Yalnız `SurumContext` onu modele alıyor; ötekiler `Ignore` ediyor. Böylece 1. senaryo
sütunun yokluğunu değil, EF'in ondan habersiz olmasını ölçüyor — üretimde tuzak tam olarak
böyle kurulur: sütun eklenmiştir, model haberdar edilmemiştir.

Ölçüm tablosu paylaşılan tohum verisine dokunmuyor. `Lab28_Sayaclar` her senaryonun kendi
transaction'ında `CREATE TABLE` ile doğuyor, on yazar o transaction'ın içinde çalışıyor ve
senaryo bitince rollback ile birlikte tablo da kayboluyor. Entity paylaşılan `LabsDbContext`'e
değil, bu laboratuvara ait dört türeve tanımlı; model önbelleği context tipine göre
ayrıldığı için aynı tablo dört ayrı modelle okunabiliyor ve öteki laboratuvarların modeli
değişmiyor.

Süre ve bellek sütunları denetlenmez ve yazıda kullanılmaz. 1. senaryo ilk koşan senaryo
olduğu için EF'in model kurulumunu üstleniyor (282 ms, 7,4 MB) — bu sayı senaryonun
maliyeti değil, ölçüm sırasının. Denetlenen tek sayı sayacın son değeridir.

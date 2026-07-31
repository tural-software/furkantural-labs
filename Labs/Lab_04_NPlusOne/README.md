# Lab_04 — N+1 Problemi: Include() ve Lazy Loading Tuzakları

İlgili yazı: [#4 — N+1 Problemi — EF Core'da Include() ve Lazy Loading Tuzakları](https://blog.furkantural.com/Home/Post/4)

## İddia

100 bloğu yorumlarıyla birlikte okumanın maliyeti, yazdığın koda değil EF'in ürettiği
**sorgu sayısına** bağlıdır. `Include` unutulduğunda 101 sorgu çalışır; lazy loading
proxy'leri açıksa aynı 101 sorgu **kodda hiç görünmeden** çalışır.

Ve ezberin yanlış olan tarafı: `Include` "her zaman tek sorgu" demek değildir.

## Kanıt

`dotnet run` çıktısı — sayılar `DbCommandInterceptor` ile komut düzeyinde sayılır,
log satırı ayrıştırılmaz:

```
Senaryo                     Sorgu  Beklenen      Süre  Sonuç
─────────────────────────  ──────  ─────────  ───────  ─────
1) Explicit load, döngüde     101  = 101       210 ms  GEÇTİ
2) Lazy loading proxy         101  = 101       206 ms  GEÇTİ
3) Include                      1  = 1         109 ms  GEÇTİ
4) Include + AsSplitQuery       2  = 2          58 ms  GEÇTİ
5) Projeksiyon (Select)         1  = 1          22 ms  GEÇTİ
```

Okuma notları:

- **1 ile 2 aynı sayıyı veriyor.** Fark yalnız görünürlükte: birincide sorguları sen
  yazdın, ikincide `blogs.Sum(b => b.Comments.Count)` tek satırı yazdın. Maliyet aynı,
  ama ikincisi kod incelemesinde fark edilmez. Lazy loading'in asıl tehlikesi budur.
- **3 ile 4 arasındaki fark bir tercih, bir hata değil.** `Include` tek sorgu üretir ama
  JOIN sonucu blog satırlarını yorum sayısı kadar tekrarlar (kartezyen genişleme).
  `AsSplitQuery` tekrarı keser, karşılığında sorgu sayısını 2'ye çıkarır. Az yorumlu
  çok blogda 3, çok yorumlu az blogda 4 kazanır.
- **5 en ucuzu ve çoğu zaman doğru olanı.** Yalnız yorum sayısı lazımsa yorum gövdelerini
  ağdan geçirmenin anlamı yok; sayım veritabanında yapılır ve change tracker boş kalır.

Beklentiler koda gömülüdür (`Expectation.Exactly(...)`). Tutmazsa süreç `1` ile biter —
bu laboratuvar aynı zamanda yazının regresyon testidir.

## Çalıştır

Bağlantı dizesini bir kez tanımla (repo public; dize `appsettings.json`'a yazılmaz):

```powershell
dotnet user-secrets set "ConnectionStrings:LabsConnection" "<baglanti-dizesi>" --project .
```

Sonra:

```powershell
dotnet run
```

İlk çalıştırma veritabanını oluşturur ve 10.000 blog + ~3.500 yorum + 10 kategori
tohumlar (bir dakika sürebilir). Sonraki çalıştırmalar veriyi olduğu gibi kullanır.

## Neden bu sayılar sabit

Seed sabit tohumlu (`new Random(20260731)`) ve satır sayıları `LabsSeeder` içinde
sabit olarak tanımlı. Yazıdaki rakamlar bu tablodan gelir; seed değişirse beklentiler
kırmızı yanar ve yazının güncellenmesi gerektiğini haber verir.

# Lab_22 — AsSplitQuery

İlgili yazı: [#22 — AsSplitQuery — İkinci Include'u Yazdığınızda Veritabanı Kaç Satır Döndürüyor?](https://blog.furkantural.com/Home/Post/22)

## İddia

Aynı 10 bloğun yorumları ve kategorileri yedi ayrı yoldan çekiliyor. Ölçülen şey,
veritabanının okuyucuya **kaç satır verdiği** — "Include'u ikinci kez yazmak neye mal
oluyor" sorusunun tek dürüst karşılığı.

## Kanıt

```text
Senaryo                            Satır  Beklenen      Süre     Bellek  Sonuç
────────────────────────────────  ──────  ─────────  ───────  ─────────  ─────
1) Include(Comments)                 200  = 200       128 ms     3,4 MB  GEÇTİ
2) + Include(Categories)           2.000  = 2000      208 ms    21,1 MB  GEÇTİ
3) AsSplitQuery                      310  = 310       105 ms     2,2 MB  GEÇTİ
4) Tek koleksiyon + AsSplitQuery     210  = 210        34 ms     1,2 MB  GEÇTİ
5) Include(Comment.Blog)             200  = 200        27 ms     2,4 MB  GEÇTİ
6) Select projeksiyonu             2.000  = 2000       48 ms     2,0 MB  GEÇTİ
7) Select ile yalnız adet             10  = 10         26 ms     257 KB  GEÇTİ
```

Satır sayısı EF Core interceptor'ının sardığı okuyucudan gelir: `Read()` çağrılarının
kaçının satır döndürdüğü sayılır. "Tek sorgu çalıştı" demek yeterli değil — bu tablodaki
1., 2. ve 6. senaryolar da tek sorgu çalıştırır.

Okuma notları:

- **1, kimsenin şikâyet etmediği hâl.** Tek koleksiyon Include edildiğinde ana kayıt çocuk
  sayısı kadar tekrar eder: 10 blog × 20 yorum. Buraya kadar herkesin beklediği şey oluyor.
- **2, tek satırlık değişikliğin faturası.** İkinci `Include` eklendi, sorgu yine tek —
  ama iki `LEFT JOIN` birbirini kesti ve her yorum her kategoriyle eşleşti. 200 satır 2.000
  oldu: JOIN toplamaz, çarpar. Bellek sütunu asıl bedeli gösteriyor — 3,4 MB'tan 21,1 MB'a.
  Çoğalan satır sayısı değil, o satırların hepsinde yeniden gönderilen blog gövdesi.
- **3, çarpımı toplamaya çeviriyor.** `AsSplitQuery` üç ayrı sorgu üretir ve her koleksiyon
  kendi satırlarını getirir: 10 + 200 + 100. 2.000 satır 310'a, 21,1 MB 2,2 MB'a indi.
- **4, bölmenin ücretsiz olmadığını söylüyor.** 1. senaryonun aynısı, üstüne 10 satır: ana
  tablo ikinci sorguda yeniden okundu. Çarpacak ikinci koleksiyon yokken bölmek kazanç
  değil doğrudan zarardır — `UseQuerySplittingBehavior(SplitQuery)` ile global açanların
  her sorguda ödediği bedel budur.
- **5, suçlunun Include olmadığını gösteriyor.** Aynı veri ters yönden: 200 yorum, her biri
  kendi bloğuyla. Referans navigation tek satıra bağlanır ve hiçbir şeyi çoğaltmaz. Belirleyici
  olan `Include`'un sayısı değil, koleksiyon olup olmadığı.
- **6, "Include yerine Select yaz" refleksinin sınırı.** Kolonlar gerçekten daraldı —
  21,1 MB yerine 2,0 MB — ama satır sayısı 2. senaryonun birebir aynısı. Projeksiyon
  taşınan kolonu küçültür, koleksiyonların aynı sorguda kesişmesini engellemez.
- **7, taşınmayan satırın maliyeti yok.** Liste ekranlarının çoğu koleksiyonun kendisini
  değil adedini gösterir. Adet veritabanında hesaplandığında 10 satır okunuyor ve tek bir
  çocuk satır taşınmıyor.

## Çalıştır

```powershell
dotnet run
```

Veritabanı gerekir; kurulum için depo kökündeki README'ye bak.

## Neden bu sayılar sabit

Ölçüm kümesini laboratuvar kendisi kurar: 10 blog, her birine tam 20 yorum ve 10
kategorinin hepsi. Tohumdaki bloglara 2–5 yorum ve 1–3 kategori düşer; o oranlarda çarpım
ile toplam birbirine çok yakın çıkar ve fark okunmaz. İki koleksiyon da bilerek kalabalık
seçildi, çünkü kanıtlanan şey çarpımın toplamdan ne kadar hızlı büyüdüğü.

Kurulum `DataSandbox` içinde yapılır ve sonunda geri alınır — tohum verisi diğer
laboratuvarlar için olduğu gibi kalır. Kurulumdan sonra `ChangeTracker` boşaltılır ve tüm
senaryolar `AsNoTracking` ile çalışır; ölçülen satırlar veritabanından okunanlardır,
tracker'dan dönenler değil.

Beklentiler ölçümden değil çarpım tablosundan gelir ve koda gömülüdür: tek koleksiyon için
`blog × yorum`, iki koleksiyon için `blog × yorum × kategori`, bölünmüş sorgu için
`blog + yorum + kategori`. Yedisi de ilk koşuda tuttu.

Süre sütunu koşudan koşuya değişir ve denetlenmez; denetlenen tek sayı satırdır. Bellek
`GC.GetTotalAllocatedBytes` ile senaryo başına ölçülür: MB ölçeğindeki altı değer sekiz
koşuda da gösterilen hassasiyette aynı kaldı, yalnızca 7. senaryonun KB ölçeğindeki değeri
birkaç yüz bayt oynuyor. Bu yüzden yazı o tek sayıyı kullanmaz — yeniden üretilemeyen bir
rakam kanıt sayılmaz.

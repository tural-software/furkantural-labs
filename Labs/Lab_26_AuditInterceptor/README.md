# Lab_26 — Audit Interceptor

İlgili yazı: [#26 — Audit damgası](https://blog.furkantural.com/Home/Post/26)

## İddia

Her senaryoda aynı **10 satır** güncelleniyor. Ölçülen şey, güncellemeden sonra
veritabanında `UpdatedAt` damgası taşıyan satır sayısı.

Değişen tek şey yazmanın hangi yoldan yapıldığı. Satırlar, güncellenen alan ve damganın
değeri sekiz senaryoda da aynı.

## Kanıt

```text
Senaryo                                      Damgalı  Beklenen      Süre     Bellek  Sonuç
───────────────────────────────────────────  ───────  ─────────  ───────  ─────────  ─────
1) Interceptor yok, damga unutulmuş                0  = 0         289 ms     5,4 MB  GEÇTİ
2) Interceptor yok, damga elle                    10  = 10         29 ms     1,3 MB  GEÇTİ
3) Interceptor açık, izlenen güncelleme           10  = 10         70 ms     482 KB  GEÇTİ
4) Interceptor açık, bağlantısız güncelleme       10  = 10         18 ms     752 KB  GEÇTİ
5) Interceptor açık, hiçbir alan değişmedi         0  = 0           9 ms     394 KB  GEÇTİ
6) Interceptor açık, ExecuteUpdateAsync            0  = 0          35 ms     417 KB  GEÇTİ
7) ExecuteUpdate + SetProperty(UpdatedAt)         10  = 10         14 ms     422 KB  GEÇTİ
8) Interceptor açık, ham SQL                       0  = 0          13 ms     317 KB  GEÇTİ
```

Sayı `ChangeTracker`'dan değil **veritabanından** okunur: her senaryonun sonunda tracker
boşaltılır ve satırlar `AsNoTracking` ile yeniden sorgulanır. Ölçülen şey damganın
bellekteki nesneye yazılması değil, diske gitmesi.

Okuma notları:

- **1, damga koddayken.** Güncelleme başarılı, satırlar değişti, tek bir damga yok. Hata
  yalnız birileri "bu kayıt en son ne zaman değişti" diye sorduğunda görünür — o da
  genelde aylar sonra.
- **2, aynı iş iki satır fazlasıyla.** Elle damgalamak yanlış değil; sorun her serviste
  tekrarlanması ve bir kez unutulduğunda 1. senaryoya dönmesi.
- **3, interceptor açıkken.** 1. senaryonun **birebir aynı kodu** 10 damga üretiyor. Damga
  serviste değil kaydetme yolunda; unutulacak bir yer kalmıyor.
- **4, bağlantısız güncelleme de sayılır.** Nesneler izlenmeden okundu, dışarıda
  değiştirildi, `Update` ile geri bağlandı. Belirleyici olan nesnenin nereden geldiği
  değil, hangi `EntityState` ile kaydedildiği.
- **5, damga niyeti değil durumu izler.** `SaveChanges` çağrıldı ama hiçbir alan
  değişmedi; girdi `Modified` olmadığı için damga da basılmadı. "Kaydet" düğmesine
  basılması kaydın değiştiği anlamına gelmiyor.
- **6, tablonun sebebi.** `ExecuteUpdateAsync` toplu güncellemenin doğru aracıdır ve nesne
  yüklemediği için hızlıdır — ama tracker'a hiç uğramaz, yani interceptor'ın dinlediği
  yerden geçmez. Tek sorgu, sıfır damga, hiçbir uyarı yok.
- **7, düzeltmesi tek satır.** `SetProperty` ile `UpdatedAt` de yazılınca 10 damga geri
  geliyor. Ama dikkat: toplu yolda damga yine **koda** dönmüş oluyor, yani 2. senaryodaki
  unutma riski bu yolda hâlâ duruyor.
- **8, kural `ExecuteUpdate`'e özgü değil.** Ham SQL ile açılan bir güncelleme de
  damgasız kalır. Kayıp aracın değil, tracker'ı atlamanın sonucu; Dapper ile yazan bir
  rapor işi de bu satırdadır.

## Çalıştır

```powershell
dotnet run
```

Veritabanı gerekir; kurulum için depo kökündeki README'ye bak.

## Neden bu sayılar sabit

Beklentiler ölçümden değil interceptor'ın çalıştığı yerden geliyor ve koda gömülü. Damga
`ChangeTracker` girdileri üzerinden basılır, dolayısıyla:

- tracker'dan geçen her yazma damgalanır → 10
- tracker'a uğramayan her yazma damgasız kalır → 0
- hiçbir alanı değişmemiş girdi `Modified` olmadığı için damgalanmaz → 0
- interceptor yokken damga koda kalır: unutulursa 0, yazılırsa 10

Sekizi de ilk koşuda tuttu.

Damganın değeri sabit bir tarihtir (tohumun başlangıcından bir yıl sonrası), saatten
okunmaz — ölçüm koşu anına bağlı olmasın diye. Fikstür her senaryoda sıfırdan kurulur ve
`UpdatedAt`'i boş 10 satırla başlar; tohumdaki bloglar kullanılmadı, çünkü onların audit
alanları başka laboratuvarların da baktığı ortak veridir.

Interceptor paylaşılan kayda **eklenmedi**: seçenek düzeyinde takıldığı için aynı
`LabsDbContext` tipinin iki seçenek kümesiyle iki örneği yetiyor. Ortak kayda eklenseydi
diğer laboratuvarların yazdığı satırlar da damgalanır ve onların sayıları değişebilirdi.

Bütün senaryolar `DataSandbox` içinde çalışır ve sonunda geri alınır; tohum verisi olduğu
gibi kalır.

Süre ve bellek sütunları denetlenmez ve yazıda kullanılmaz. 1. senaryo ilk koşan senaryo
olduğu için EF'in model kurulumunu üstleniyor (289 ms, 5,4 MB) — bu sayı senaryonun
maliyeti değil, ölçüm sırasının. Denetlenen tek sayı damgalı satır adedidir ve dört
koşuda da birebir aynı çıktı.

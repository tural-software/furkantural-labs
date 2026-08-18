# Lab_23 — Global Query Filter

İlgili yazı: [#23 — Global Query Filter](https://blog.furkantural.com/Home/Post/23)

## İddia

Aynı 10 blog ve 100 yorum sekiz ayrı yoldan sorgulanıyor. Ölçülen şey, veritabanının
okuyucuya **kaç satır verdiği** — "modeldeki filtre benim sorgumu ne kadar değiştiriyor"
sorusunun tek dürüst karşılığı.

## Kanıt

```text
Senaryo                           Satır  Beklenen      Süre     Bellek  Sonuç
───────────────────────────────  ──────  ─────────  ───────  ─────────  ─────
1) Blogs (iki filtre açık)            3  = 3          33 ms     987 KB  GEÇTİ
2) IgnoreQueryFilters()              10  = 10          5 ms     229 KB  GEÇTİ
3) + Include(Comments)               21  = 21         46 ms     684 KB  GEÇTİ
4) IgnoreQueryFilters + Include     100  = 100        15 ms     684 KB  GEÇTİ
5) Ignore(SoftDelete)                 7  = 7           7 ms     235 KB  GEÇTİ
6) Ignore(Published)                  5  = 5           5 ms     235 KB  GEÇTİ
7) Comments (doğrudan)               70  = 70          5 ms     270 KB  GEÇTİ
8) Comments + blog koşulu            21  = 21         14 ms     307 KB  GEÇTİ
```

Satır sayısı EF Core interceptor'ının sardığı okuyucudan gelir: `Read()` çağrılarının
kaçının satır döndürdüğü sayılır. Sekiz senaryonun hiçbirinde `IsDeleted` ya da
`PublishedAt` koşulu **elle yazılmadı**; tablodaki bütün daralmaları modeldeki iki filtre
yaptı.

Okuma notları:

- **1, kimsenin görmediği WHERE.** Sorguda `blogIds.Contains(b.Id)` dışında koşul yok, yine
  de 10 satırın 3'ü geldi. Filtre modelde durur ve o entity'ye giden **her** sorguya
  eklenir; kodu okuyan biri neden 3 satır döndüğünü sorgunun kendisinden anlayamaz.
- **2, silinen satırın nerede olduğunu söylüyor.** `IgnoreQueryFilters()` ile 10 satırın
  tamamı geliyor. Soft delete bir görünürlük kararıdır: satır diskte, indekste ve
  yedekte durmaya devam eder — yalnızca sorgular ona bakmaz.
- **3, filtrenin köke özel olmadığını gösteriyor.** `Include` edilen `Comments`
  koleksiyonu kendi filtresini taşıyor ve o da uygulanıyor: 3 blog × 7 canlı yorum. İki
  daralma toplanmıyor, çarpılıyor — ekrandan kaybolan yorum sayısı bu yüzden beklenenden
  fazla olur.
- **4, kapatma anahtarının kalınlığı.** "Silinmiş bloğu da göreyim" diye yazılan tek çağrı
  21 satırı 100'e çıkardı. İstenen blog filtresini kapatmaktı; kapanan, sorgudaki bütün
  filtreler oldu — silinmiş yorumlar da geri geldi. Yönetim ekranlarında en sık yapılan
  hata bu satırdır.
- **5 ve 6, EF Core 10'un adlandırılmış filtreleri.** Aynı sorgu, aynı veri, iki farklı
  cevap: `Ignore("SoftDelete")` → 7 satır (silinmişler döndü, yayın anı gelmemiş 3 blog
  hâlâ dışarıda), `Ignore("Published")` → 5 satır (beklemedekiler döndü, silinmişler
  dışarıda). EF Core 9'a kadar bu ayrım yoktu: filtreler tek bir ifadede birleşir ve
  ancak topluca kapatılabilirdi.
- **7, filtrenin kalıtsal olmadığını gösteriyor.** 10 bloğun 7'si listede görünmüyor, ama
  yorum tablosuna doğrudan sorulduğunda o blogların yorumları geliyor: 70 satır. Bloğu
  silmek yorumlarını silmez ve hiçbir global filtre bunu telafi etmez — çocuk kendi
  bayrağına bakar, ebeveynininkine değil.
- **8, aynı verinin ikinci gerçeği.** Tek fark sorgunun bloğa uzanması. Koşul
  (`Title.StartsWith(...)`) 10 bloğun onu için de doğru, buna rağmen 70 satır 21'e düştü:
  sorgu bloğa dokunduğu an bloğun filtresi de devreye girdi. Yani "silinmiş bloğun
  yorumları görünür mü" sorusunun cevabı veriye değil, sorgunun şekline bağlı.

## Çalıştır

```powershell
dotnet run
```

Veritabanı gerekir; kurulum için depo kökündeki README'ye bak.

## Neden bu sayılar sabit

Filtre paylaşılan `LabsDbContext`'e **konulmadı**. Konsaydı diğer 22 laboratuvarın saydığı
satırlar sessizce değişirdi — ve tam olarak bu yazının anlattığı şey başımıza gelirdi.
Filtreler yalnız bu laboratuvara ait `SoftDeleteContext` türevinde tanımlı; EF model
önbelleğini context tipine göre ayırdığı için tohum verisi ve öteki laboratuvarlar
etkilenmiyor.

Ölçüm kümesini laboratuvar kendisi kurar: iki eksen çaprazlanmış 10 blog — 3 canlı ve
yayında, 2 canlı ve beklemede, 4 silinmiş ve yayında, 1 silinmiş ve beklemede. Hücreler
**bilerek eşit değil**: eşit olsalardı 5. ve 6. senaryolar aynı sayıyı verir ve hangi
filtrenin neyi tuttuğu tablodan okunamazdı. Her bloğa 10 yorum düşer, son 3'ü silinmiş; bu
sayı bloğun durumundan bağımsızdır, çünkü kanıtlanan şeylerden biri soft delete'in kaskad
etmediği.

"Yayında" için tohumun başlangıcı (2024), "beklemede" için bir yüzyıl sonrası kullanıldı.
`Published` filtresi `PublishedAt <= DateTime.UtcNow` yazıldığı ve `GETUTCDATE()` ile
çevrildiği için sınır ölçüm anına yakın olsaydı sayı saate bağlanırdı; yüz yıllık pay bunu
imkânsız kılıyor.

Kurulum `DataSandbox` içinde yapılır ve sonunda geri alınır — tohum verisi diğer
laboratuvarlar için olduğu gibi kalır. Kurulumdan sonra `ChangeTracker` boşaltılır ve tüm
senaryolar `AsNoTracking` ile çalışır; ölçülen satırlar veritabanından okunanlardır,
tracker'dan dönenler değil.

Beklentiler ölçümden değil hücre tablosundan gelir ve koda gömülüdür: iki filtre açıkken
`canlı ∩ yayında`, `Ignore("SoftDelete")` için `yayında`, `Include`'lu senaryolar için
`blog × canlı yorum`. Sekizi de ilk koşuda tuttu.

Süre sütunu koşudan koşuya değişir ve denetlenmez. Bellek de denetlenmez: bu laboratuvarın
sekiz değeri de KB ölçeğinde ve koşular arasında birkaç KB oynuyor. Bu yüzden yazı bellek
sayısı kullanmaz — yeniden üretilemeyen bir rakam kanıt sayılmaz. Denetlenen tek sayı
satırdır ve dört koşuda da birebir aynı çıktı.

# FurkanTural Labs

[blog.furkantural.com](https://blog.furkantural.com) üzerindeki her yazının **çalışan
kanıtı**. Her laboratuvar tek bir işi yapar: yazının iddiasını ölçülebilir bir sayıya
çevirir ve iddia tutmazsa sıfırdan farklı çıkış koduyla biter.

Bu bir örnek kod deposu değildir. "Doğru kullanım şudur" diyen bir demo, yazının
kendisinden daha az şey anlatır — çünkü yazıda hiç değilse gerekçe vardır. Buradaki
laboratuvarlar **yanlışı çalıştırıp maliyetini gösterir**, sonra doğrusunu aynı ölçekte
ölçer. Fark okunarak değil sayılarak görülür.

## Laboratuvarlar

| # | Yazı | Lab | Durum |
|---|---|---|---|
| 01 | IQueryable vs IEnumerable | [`Labs/Lab_01_QueryableVsEnumerable`](Labs/Lab_01_QueryableVsEnumerable) | ✅ |
| 02 | AsNoTracking() | [`Labs/Lab_02_AsNoTracking`](Labs/Lab_02_AsNoTracking) | ✅ |
| 03 | Middleware Pipeline | [`Labs/Lab_03_MiddlewarePipeline`](Labs/Lab_03_MiddlewarePipeline) | ✅ |
| 04 | N+1 Problemi | [`Labs/Lab_04_NPlusOne`](Labs/Lab_04_NPlusOne) | ✅ |
| 05 | DI Scope Hataları | [`Labs/Lab_05_DiScope`](Labs/Lab_05_DiScope) | ✅ |
| 06 | Global Exception Handling | [`Labs/Lab_06_GlobalExceptionHandling`](Labs/Lab_06_GlobalExceptionHandling) | ✅ |
| 07 | C# Record Types | [`Labs/Lab_07_RecordTypes`](Labs/Lab_07_RecordTypes) | ✅ |
| 08 | CancellationToken | [`Labs/Lab_08_CancellationToken`](Labs/Lab_08_CancellationToken) | ✅ |
| 09 | HttpClient / Socket Exhaustion | [`Labs/Lab_09_HttpClientSockets`](Labs/Lab_09_HttpClientSockets) | ✅ |
| 10 | Specification Pattern | [`Labs/Lab_10_Specification`](Labs/Lab_10_Specification) | ✅ |
| 11 | Memory vs Distributed Cache | [`Labs/Lab_11_MemoryVsDistributedCache`](Labs/Lab_11_MemoryVsDistributedCache) | ✅ |
| 12 | DateTime vs DateTimeOffset | [`Labs/Lab_12_DateTimeVsOffset`](Labs/Lab_12_DateTimeVsOffset) | ✅ |
| 13 | IOptions / Snapshot / Monitor | [`Labs/Lab_13_OptionsLifetimes`](Labs/Lab_13_OptionsLifetimes) | ✅ |
| 14 | Rate Limiting | [`Labs/Lab_14_RateLimiting`](Labs/Lab_14_RateLimiting) | ✅ |
| 15 | Channel&lt;T&gt; | [`Labs/Lab_15_Channel`](Labs/Lab_15_Channel) | ✅ |
| 16 | Policy-Based Authorization | [`Labs/Lab_16_PolicyAuthorization`](Labs/Lab_16_PolicyAuthorization) | ✅ |
| 17 | Skip/Take Sayfalama | [`Labs/Lab_17_SkipTakePaging`](Labs/Lab_17_SkipTakePaging) | ✅ |
| 18 | Structured Logging | [`Labs/Lab_18_StructuredLogging`](Labs/Lab_18_StructuredLogging) | ✅ |
| 19 | ExecuteUpdateAsync | [`Labs/Lab_19_ExecuteUpdate`](Labs/Lab_19_ExecuteUpdate) | ✅ |
| 20 | CORS | [`Labs/Lab_20_Cors`](Labs/Lab_20_Cors) | ✅ |
| 21 | Output Caching | [`Labs/Lab_21_OutputCaching`](Labs/Lab_21_OutputCaching) | ✅ |
| 22 | AsSplitQuery | [`Labs/Lab_22_SplitQuery`](Labs/Lab_22_SplitQuery) | ✅ |
| 23 | Global Query Filter | [`Labs/Lab_23_QueryFilter`](Labs/Lab_23_QueryFilter) | ✅ |
| 24 | Async'te Kaybolan İstisnalar | [`Labs/Lab_24_AsyncExceptions`](Labs/Lab_24_AsyncExceptions) | ✅ |
| 25 | System.Text.Json Sözleşmesi | [`Labs/Lab_25_JsonContract`](Labs/Lab_25_JsonContract) | ✅ |
| 26 | Audit Interceptor | [`Labs/Lab_26_AuditInterceptor`](Labs/Lab_26_AuditInterceptor) | ✅ |

Yirmi altı yazının yirmi altısının da çalışan bir kanıtı var. Az ya da çok demeden: kısa bir
yazının laboratuvarı da kısadır, ama vardır.

Laboratuvarlar blog sırasına göre değil **ortak altyapıya** göre yazıldı; aynı düzeneği
kuran laboratuvarlar peş peşe geldi (önce EF, sonra host, en son saf C#).

## Yapı

```text
Core/FurkanTural_Labs_Domain/            entity'ler (BaseEntity, Blog, Category, Comment)
Signature/FurkanTural_Labs_Application/
        Diagnostics/     LabReport, Expectation, IQueryCounter  ← ölçüm düzeneği
        Specifications/  ISpecification, BaseSpecification
Web/FurkanTural_Labs_Host/
        LabsWebHost      boru hattı ölçen laboratuvarların in-process Kestrel'i
        LabAuthentication başlık tabanlı en küçük kimlik şeması (Lab_03, Lab_16, Lab_20)
Infrastructure/FurkanTural_Labs_Persistence/
        Contexts/        LabsDbContext
        Configurations/  IEntityTypeConfiguration'lar
        Interceptors/    QueryCountInterceptor  ← sorgu ve satır burada sayılır
        Seed/            LabsSeeder             ← sabit tohumlu veri
        Sandbox/         DataSandbox            ← veri değiştiren laboratuvarların kabı
        Cache/           SqlCacheTable          ← dağıtık cache tablosu (Lab_11)
        Specifications/  SpecificationEvaluator
        Registration/    AddLabsPersistence / LabsHost
Labs/Lab_NN_.../                         her yazı için tek çalıştırılabilir proje
```

Katman düzeni ve isimlendirme ana monorepo ile bilerek aynıdır; burada öğrenilen şey
oraya birebir taşınsın diye. Her laboratuvara ayrı bir beş katman verilmedi — 26 × 5
proje, dersi tören altında boğardı. Paylaşılan çekirdek + laboratuvar başına tek
çalıştırılabilir proje: aynı disiplin, gereksiz tekrar yok.

### Ölçüm neden interceptor ile

Sorgu sayısı log satırı ayrıştırılarak değil, EF Core `DbCommandInterceptor` ile komut
düzeyinde sayılır. Log formatı sürüm sürüm değişir; interceptor değişmez ve `ExecuteUpdate`
gibi log'da farklı görünen çağrıları da doğru sayar.

Satır sayısı için aynı interceptor okuyucuyu saran bir katman döndürür. EF'in kendi
`ReadCount` değeri kullanılmadı: o sayı okuma *çağrısı* adedidir ve okuyucu sonuna kadar
tüketildiğinde satır sayısının bir fazlasını verir. Fark sabit olmadığı için sabit bir
sayı çıkarmak da yanlış olurdu; `Read()` sonuçları sayılıyor.

### Her laboratuvar tek birim ölçer

Sütun başlığı laboratuvara göre değişir: N+1'de "Sorgu", `IQueryable`'da "Satır",
`AsNoTracking`'de "İzlenen". Sorgu sayan bir tabloya satır karıştırılırsa okuyucu neyin
neyle kıyaslandığını kaybeder; bu kısıt aynı zamanda **her laboratuvarın tek iddia
taşımasını** zorlar.

### Tohum verisi ölümsüzdür

Laboratuvarlar aynı veri kümesini paylaşır ve kesin sayılar iddia eder: Lab_01 tabloda
tam 10.000 satır olduğunu, Lab_17 sayfa 500'ün nerede başladığını söyler. Bu yüzden veri
değiştiren laboratuvarlar (12, 17, 19) işlerini `DataSandbox` içinde yapar: değişiklikler
transaction içinde **gerçekten** uygulanır — aynı bağlantıdan görünürler, sorgu sayısı ve
süre gerçektir — sonra geri alınır. Sahte veri ya da bellek içi taklit yok.

### Beklentiler koda gömülüdür

Her senaryo bir `Expectation` ile tanımlanır (`Exactly`, `AtMost`, `Between`).
Tutmazsa `LabReport.Print()` `1` döndürür ve süreç hatayla biter. Böylece laboratuvarlar
aynı zamanda **yazıların regresyon testidir**: .NET sürümü davranışı değiştirirse ilgili
laboratuvar kırmızı yanar ve hangi yazının artık yanlış olduğunu söyler.

## Kurulum

Gereken: .NET 10 SDK. Veritabanı yalnızca veriye giden laboratuvarlar için gerekir
(01, 02, 04, 10, 11, 12, 17, 19, 22, 23, 26); boru hattını ölçenler kendi sunucusunu ayağa kaldırır ve
SQL Server istemez.

Bağlantı dizesi **repoda tutulmaz** — depo public. Tüm laboratuvarlar tek bir
user-secrets kimliğini paylaşır, yani bir kez tanımlaman yeter:

```powershell
dotnet user-secrets set "ConnectionStrings:LabsConnection" `
    "Server=<host>,<port>;Database=FurkanTural_Labs;User Id=<kullanici>;Password=<sifre>;Encrypt=True;TrustServerCertificate=True;" `
    --project Labs/Lab_04_NPlusOne
```

Alternatif: `FTLABS_ConnectionStrings__LabsConnection` ortam değişkeni (user-secrets'ı ezer).

Veritabanı ilk çalıştırmada oluşturulur ve tohumlanır; migration yoktur çünkü bu şema
tek kullanımlıktır ve migration geçmişi tutmanın öğretici değeri yoktur. Lab_11'in
kullandığı dağıtık cache tablosu da aynı veritabanında, aynı anda oluşturulur — Redis
kurmak gerekmez, çünkü kanıtlanan özellik cache'in **süreç dışında** durmasıdır.

```powershell
dotnet run --project Labs/Lab_04_NPlusOne
```

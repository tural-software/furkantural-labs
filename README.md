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
| 01 | IQueryable vs IEnumerable | `Labs/Lab_01_...` | Faz 1 |
| 02 | AsNoTracking() | `Labs/Lab_02_...` | Faz 1 |
| 03 | Middleware Pipeline | `Labs/Lab_03_...` | Faz 2 |
| 04 | N+1 Problemi | [`Labs/Lab_04_NPlusOne`](Labs/Lab_04_NPlusOne) | ✅ |
| 05 | DI Scope Hataları | `Labs/Lab_05_...` | Faz 2 |
| 06 | Global Exception Handling | `Labs/Lab_06_...` | Faz 2 |
| 07 | C# Record Types | `Labs/Lab_07_...` | Faz 4 |
| 08 | CancellationToken | `Labs/Lab_08_...` | Faz 2 |
| 09 | HttpClient / Socket Exhaustion | `Labs/Lab_09_...` | Faz 2 |
| 10 | Specification Pattern | `Labs/Lab_10_...` | Faz 1 |
| 11 | Memory vs Distributed Cache | `Labs/Lab_11_...` | Faz 3 |
| 12 | DateTime vs DateTimeOffset | `Labs/Lab_12_...` | Faz 1 |
| 13 | IOptions / Snapshot / Monitor | `Labs/Lab_13_...` | Faz 3 |
| 14 | Rate Limiting | `Labs/Lab_14_...` | Faz 3 |
| 15 | Channel&lt;T&gt; | `Labs/Lab_15_...` | Faz 4 |
| 16 | Policy-Based Authorization | `Labs/Lab_16_...` | Faz 3 |
| 17 | Skip/Take Sayfalama | `Labs/Lab_17_...` | Faz 1 |
| 18 | Structured Logging | `Labs/Lab_18_...` | Faz 2 |
| 19 | ExecuteUpdateAsync | `Labs/Lab_19_...` | Faz 1 |
| 20 | CORS | `Labs/Lab_20_...` | Faz 3 |
| 21 | Output Caching | `Labs/Lab_21_...` | Faz 3 |

Fazlar blog sırasına göre değil **ortak altyapıya** göre gruplanmıştır: aynı düzeneği
kuran laboratuvarlar peş peşe yazılır (Faz 1 EF, Faz 2–3 host, Faz 4 saf C#).

## Yapı

```
Core/FurkanTural_Labs_Domain/            entity'ler (BaseEntity, Blog, Category, Comment)
Signature/FurkanTural_Labs_Application/  sözleşmeler + ölçüm düzeneği (LabReport, IQueryCounter)
Infrastructure/FurkanTural_Labs_Persistence/
        Contexts/        LabsDbContext
        Configurations/  IEntityTypeConfiguration'lar
        Interceptors/    QueryCountInterceptor  ← sorgular burada sayılır
        Seed/            LabsSeeder             ← sabit tohumlu veri
        Registration/    AddLabsPersistence / LabsHost
Labs/Lab_NN_.../                         her yazı için tek çalıştırılabilir proje
```

Katman düzeni ve isimlendirme ana monorepo ile bilerek aynıdır; burada öğrenilen şey
oraya birebir taşınsın diye. Her laboratuvara ayrı bir beş katman verilmedi — 21 × 5
proje, dersi tören altında boğardı. Paylaşılan çekirdek + laboratuvar başına tek
çalıştırılabilir proje: aynı disiplin, gereksiz tekrar yok.

### Ölçüm neden interceptor ile

Sorgu sayısı log satırı ayrıştırılarak değil, EF Core `DbCommandInterceptor` ile komut
düzeyinde sayılır. Log formatı sürüm sürüm değişir; interceptor değişmez ve `ExecuteUpdate`
gibi log'da farklı görünen çağrıları da doğru sayar.

### Beklentiler koda gömülüdür

Her senaryo bir `Expectation` ile tanımlanır (`Exactly`, `AtMost`, `Between`).
Tutmazsa `LabReport.Print()` `1` döndürür ve süreç hatayla biter. Böylece laboratuvarlar
aynı zamanda **yazıların regresyon testidir**: .NET sürümü davranışı değiştirirse ilgili
laboratuvar kırmızı yanar ve hangi yazının artık yanlış olduğunu söyler.

## Kurulum

Gereken: .NET 10 SDK ve erişilebilir bir SQL Server.

Bağlantı dizesi **repoda tutulmaz** — depo public. Tüm laboratuvarlar tek bir
user-secrets kimliğini paylaşır, yani bir kez tanımlaman yeter:

```powershell
dotnet user-secrets set "ConnectionStrings:LabsConnection" `
    "Server=<host>,<port>;Database=FurkanTural_Labs;User Id=<kullanici>;Password=<sifre>;Encrypt=True;TrustServerCertificate=True;" `
    --project Labs/Lab_04_NPlusOne
```

Alternatif: `FTLABS_ConnectionStrings__LabsConnection` ortam değişkeni (user-secrets'ı ezer).

Veritabanı ilk çalıştırmada oluşturulur ve tohumlanır; migration yoktur çünkü bu şema
tek kullanımlıktır ve migration geçmişi tutmanın öğretici değeri yoktur.

```powershell
dotnet run --project Labs/Lab_04_NPlusOne
```

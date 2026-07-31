# Lab_19 — ExecuteUpdateAsync

İlgili yazı: [#19 — ExecuteUpdateAsync — 50.000 Satırı Güncellemek İçin Neden 50.000 Nesne Yüklüyorsunuz?](https://blog.furkantural.com/Home/Post/19)

## İddia

Aynı 1.000 satır iki yoldan güncelleniyor. Klasik yol — yükle, döngüde değiştir,
`SaveChanges` — satır sayısıyla **büyüyen** bir komut hacmi üretir. Toplu komut tek
cümlede biter ve hiçbir satır nesneye dönüşmez.

## Kanıt

```text
Senaryo                               Sorgu  Beklenen      Süre     Bellek  Sonuç
───────────────────────────────────  ──────  ─────────  ───────  ─────────  ─────
1) Yükle, döngü, SaveChanges             25  = 25        386 ms    11,6 MB  GEÇTİ
2) ExecuteUpdateAsync                     1  = 1         109 ms     179 KB  GEÇTİ
3) ExecuteUpdate, hesaplanmış değer       1  = 1          18 ms     171 KB  GEÇTİ
4) ExecuteUpdate + denetim alanı          1  = 1          15 ms     222 KB  GEÇTİ
5) ExecuteDeleteAsync                     1  = 1         125 ms     173 KB  GEÇTİ
```

Okuma notları:

- **25 sayısı ölçümden değil aritmetikten gelir:** 1 `SELECT` + `ceil(1000 / 42)` parti.
  42, SQL Server sağlayıcısının varsayılan parti boyudur. EF batch'lese de **parti boyu
  sabittir, komut sayısı satır sayısıyla büyür** — yazının söylediği tam olarak bu.
  50.000 satırda aynı hesap 1.191 komut verir.
- **11,6 MB'a karşı 179 KB.** Tek bir `bool` alanı için entity'nin bütün kolonları
  çekiliyor, her satır nesneye dönüşüyor ve Change Tracker'a snapshot yazılıyor.
- **3, klasik yolda mümkün olmayanı yapar:** yeni değeri satırın kendisinden türetmek.
  `SetProperty(b => b.ViewCount, b => b.ViewCount + 1)` artırmayı veritabanı tarafında
  yapar; okuma-yazma arasındaki yarış koşulu da böylece ortadan kalkar.
- **4'te komut sayısı değişmiyor, sorumluluk değişiyor.** Toplu komut `SaveChanges`
  üzerinden geçmez: `UpdatedAt` dolduran override'ınız, soft-delete mantığınız ve
  `SaveChanges` interceptor'larınız tetiklenmez. İkinci `SetProperty` olmasaydı
  `UpdatedAt` boş kalırdı ve kimse fark etmezdi.
- **5, `ISoftDeletable` bir entity üzerinde satırı gerçekten siler.** `IsDeleted`
  işaretlenmez. Bağımlı kayıtların akıbeti EF'in client-side cascade davranışına değil,
  veritabanındaki FK kuralına kalır — burada `ON DELETE CASCADE` tanımlı olduğu için
  komut çalışır; tanımlı olmasaydı FK ihlaliyle patlardı.

Yazının üçüncü tuzağı tabloda görünmez ama bu laboratuvarın kurulumunda saklıdır: toplu
komutlar çağrıldıkları anda veritabanına gider ve `SaveChanges`'in otomatik
transaction'ına dahil olmaz. Burada hepsi açıkça başlatılmış bir transaction içinde
çalışır — bu yüzden geri alınabiliyorlar.

## Çalıştır

```powershell
dotnet user-secrets set "ConnectionStrings:LabsConnection" "<baglanti-dizesi>" --project .
dotnet run
```

## Neden bu sayılar sabit

Hedef küme "son 1.000 yazı"dır ve tohumdaki `PublishedAt = Epoch + i saat` sıralamasından
türetilir. Beş senaryonun beşi de transaction içinde çalışıp geri alınır; laboratuvar
istediği kadar çalıştırılabilir, veri hep aynı kalır. Parti boyu bir EF ayrıntısıdır:
sağlayıcı bunu değiştirirse 1. senaryo kırmızı yanar ve yazıdaki 25 sayısının
güncellenmesi gerektiğini haber verir.

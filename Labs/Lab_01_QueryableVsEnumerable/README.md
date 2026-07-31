# Lab_01 — IQueryable vs IEnumerable

İlgili yazı: [#1 — IQueryable vs IEnumerable — EF Core'da Yanlış Seçim Nasıl Sistemi Yavaşlatır](https://blog.furkantural.com/Home/Post/1)

## İddia

Dört zincir de aynı soruyu sorar ("en çok okunan on yazı") ve dördü de **tek sorgu**
çalıştırır. Yani sorgu sayısına bakan bir ölçüm bu hatayı göremez.

Fark, veritabanından çıkan satır sayısındadır. `IQueryable`'ın `IEnumerable`'a düştüğü
noktadan sonrası artık uygulamanın işidir; o noktaya kadar elenmemiş ne varsa ağdan geçer.

## Kanıt

```text
Senaryo                                Satır  Beklenen      Süre     Bellek  Sonuç
────────────────────────────────────  ──────  ─────────  ───────  ─────────  ─────
1) Repository IEnumerable döndürüyor  10.000  = 10000     283 ms    17,7 MB  GEÇTİ
2) Zincirin ortasında ToList()         1.000  = 1000      149 ms     2,3 MB  GEÇTİ
3) Bilinçli AsEnumerable                 100  = 100        17 ms     244 KB  GEÇTİ
4) Baştan sona IQueryable                 10  = 10         12 ms     313 KB  GEÇTİ
```

Satır sayısı EF'in okuyucusunu saran bir katmanla ölçülür; log satırı ayrıştırılmaz.

Okuma notları:

- **1 ile 4 arasındaki tek fark bir tip adı.** `IEnumerable<Blog> source = db.Blogs;`
  satırı derlenir, doğru sonucu döndürür, testleri geçer. Bedeli 10 kayıt için 10.000
  satır ve 17,7 MB'lık ayırmadır — 4'ün elli katından fazlası.
- **2, hatanın küçük ama aynı cinsten hâli.** Ön filtre SQL'e gitti, `ToList()` sınırı
  erken kapattı. Zincirin geri kalanı 1.000 satır üzerinde bellekte çalıştı.
- **3 hata değil, tercih.** İfade ağacına çevrilemeyen bir kural zorunluysa geçiş
  kaçınılmazdır. Belirleyici olan geçişin *yapılıp yapılmadığı* değil, geçişten önce
  kaç satır kaldığıdır: 1.000 yerine 100.

## Çalıştır

Bağlantı dizesini bir kez tanımla (repo public; dize `appsettings.json`'a yazılmaz):

```powershell
dotnet user-secrets set "ConnectionStrings:LabsConnection" "<baglanti-dizesi>" --project .
```

Sonra:

```powershell
dotnet run
```

## Neden bu sayılar sabit

Sınırlar tohumdan türetilir, sorguyla aranmaz: `PublishedAt = Epoch + i saat` olduğu için
"son 1.000 yazı" tam olarak 1.000, "son 100 yazı" tam olarak 100 satırdır. Beklentiler
koda gömülüdür; tutmazsa süreç `1` ile biter ve bu laboratuvar yazının regresyon testi olur.

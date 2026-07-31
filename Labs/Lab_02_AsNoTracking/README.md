# Lab_02 — AsNoTracking()

İlgili yazı: [#2 — AsNoTracking() — EF Core'da Okuma Sorgularında Neden Zorunlu?](https://blog.furkantural.com/Home/Post/2)

## İddia

Aynı 10.000 satır beş farklı şekilde okunuyor. Sorgu sayısı hepsinde bir. Değişen tek
şey Change Tracker'da kaç nesnenin **orijinal değer kopyasıyla birlikte** tutulduğu.

## Kanıt

```text
Senaryo                                İzlenen  Beklenen      Süre     Bellek  Sonuç
─────────────────────────────────────  ───────  ─────────  ───────  ─────────  ─────
1) Varsayılan (tracking açık)           10.000  = 10000     504 ms    32,2 MB  GEÇTİ
2) AsNoTracking()                            0  = 0         191 ms    10,2 MB  GEÇTİ
3) Projeksiyon (tracking açık)               0  = 0          69 ms     3,9 MB  GEÇTİ
4) Aynı sorgu iki kez (tracking açık)   10.000  = 10000     479 ms    38,9 MB  GEÇTİ
5) AsNoTracking + SaveChanges                0  = 0          55 ms     402 KB  GEÇTİ
```

Okuma notları:

- **1 ile 2 arasındaki fark yazıdaki tabloyla aynı yönde ve aynı büyüklük mertebesinde:**
  süre 504 ms → 191 ms, ayrılan bellek 32,2 MB → 10,2 MB. Yazı ~%55 hız ve ~%57 bellek
  demişti; ölçülen ~%62 ve ~%68.
- **3, boş yere yazılmış `AsNoTracking()`'lerin sebebi.** Takip entity tipiyle gelir;
  projeksiyon entity döndürmediği için takip edilecek bir şey zaten yoktur.
- **4, takibin faydasını gösterir.** İkinci sorgu yeni nesne üretmez, identity map
  aynı örneği döndürür — sayı 20.000'e çıkmaz. Okuyup güncelleyeceğiniz senaryoda
  güvendiğiniz davranış budur.
- **5, bedelin karşılığıdır.** Takip edilmeyen nesnede yapılan değişikliği `SaveChanges`
  göremez: istisna da yoktur, tek bir komut bile üretilmez. Değişiklik sessizce kaybolur.

## Çalıştır

```powershell
dotnet user-secrets set "ConnectionStrings:LabsConnection" "<baglanti-dizesi>" --project .
dotnet run
```

## Neden bu sayılar sabit

İzlenen sayısı `ChangeTracker.Entries()` ile okunur ve tohumdaki satır sayısına eşittir.
Süre ile bellek makineye göre değişir; beklentiye bağlanan sayı yalnızca izlenen entity
adedidir. 5. senaryo transaction içinde çalışır ve geri alınır, tohum bozulmaz.

# Lab_10 — Specification Pattern

İlgili yazı: [#10 — Specification Pattern — Repository'yi Koşullarla Kirletmeyi Bırakın](https://blog.furkantural.com/Home/Post/10)

## İddia

Specification'ın değeri repository'yi ince tutmasında değil, koşulu **SQL'e
taşıyabilmesinde**. Koşul `Expression<Func<T, bool>>` yerine `Func<T, bool>` olarak
tutulduğunda desen aynen durur — arayüz, base class, tek `ListAsync`, hepsi yerinde —
ama sorgu artık tabloyu belleğe çeker.

Bu hata derlenir, doğru sonucu döndürür ve testleri geçer. Görülmesinin tek yolu taşınan
satırı saymaktır.

## Kanıt

```text
Senaryo                         Satır  Beklenen      Süre     Bellek  Sonuç
─────────────────────────────  ──────  ─────────  ───────  ─────────  ─────
1) Sızdıran spec (Func)        10.000  = 10000      95 ms     7,0 MB  GEÇTİ
2) Doğru spec (Expression)      1.000  = 1000      103 ms     1,3 MB  GEÇTİ
3) İki spec, And ile birleşik     101  = 101        52 ms     897 KB  GEÇTİ
4) Sayfalama spec'in içinde        20  = 20         16 ms     363 KB  GEÇTİ
```

Okuma notları:

- **1 ile 2 aynı koşulu taşır ve aynı 1.000 kaydı döndürür.** Tek fark `Where`
  çağrısının hangi sınıfa gittiğidir: koşul bir `Func` olduğu için derleyici
  `Queryable.Where`'i seçemez, `Enumerable.Where`'e düşer.
- **Süre sütununa bu ölçekte bakmayın.** 1'in 2'den hızlı görünmesi sabit maliyetlerin
  gürültüsüdür; kanıt satır sütunundadır. 10.000 satır bir ağ üzerinden değil, aynı
  makinedeki SQL Server'dan geliyor.
- **3, deseni değerli kılan yer.** Yeni bir repository metodu yazmadan koşul eklemek.
  Yazıdaki `.And(...)` kullanımının implementasyonu burada: iki ifade ağacının gövdesi
  **tek parametreye yeniden bağlanır**. Bağlanmazsa ortaya iki parametreli bir ağaç
  çıkar ve EF onu çeviremez — derlenir, çalışma anında patlar.
- **4, sayfalamanın da specification'ın parçası olduğunu gösterir.** `Skip`/`Take`
  ifade ağacına girdiği için servis katmanı sayfalamayı bellekte yapmak zorunda kalmaz.

## Çalıştır

```powershell
dotnet user-secrets set "ConnectionStrings:LabsConnection" "<baglanti-dizesi>" --project .
dotnet run
```

## Neden bu sayılar sabit

Tohumda ilk 1.000 blog yorumludur ve `PublishedAt = Epoch + i saat`'tir; "ilk 1.000 yazı"
ve "ilk 101 yazı" sınırları bu yüzden tam sayı verir. Doğru specification'lar
`Signature/FurkanTural_Labs_Application/Specifications/` altında paylaşılan koddur;
sızdıran ikizi yalnızca bu laboratuvarın içinde yaşar.

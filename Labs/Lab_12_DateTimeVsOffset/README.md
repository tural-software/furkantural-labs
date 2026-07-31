# Lab_12 — DateTime vs DateTimeOffset

İlgili yazı: [#12 — DateTime vs DateTimeOffset — Veritabanına Yazarken Zaman Dilimi Neden Kayboluyor?](https://blog.furkantural.com/Home/Post/12)

## İddia

Aynı üç an beş farklı şekilde yazılıp geri okunuyor. Sorun tarihin yanlış yazılması
değil; **hangi zaman dilimine ait olduğunun saklanmaması**. `datetime2` sütunu rakamları
saklar, `Kind` alanını saklamaz.

## Kanıt

```text
Senaryo                                    Doğru  Beklenen      Süre     Bellek  Sonuç
────────────────────────────────────────  ──────  ─────────  ───────  ─────────  ─────
1) datetime2 ← yerel duvar saati               0  = 0         294 ms     2,8 MB  GEÇTİ
2) datetime2 ← UTC + okurken SpecifyKind       3  = 3          91 ms     204 KB  GEÇTİ
3) datetimeoffset ← yerel saat + offset        3  = 3          11 ms     202 KB  GEÇTİ
4) DateTime karşılaştırması                    0  = 0           0 ms      104 B  GEÇTİ
5) DateTimeOffset karşılaştırması              3  = 3           0 ms      104 B  GEÇTİ
```

"Doğru" sütunu: üç yazımdan kaçı geri okunduğunda **aynı anı** gösteriyor.

Okuma notları:

- **1, üç saatlik kaymanın doğduğu yer.** `DateTime.Now` sütuna gider, geri
  `Unspecified` olarak döner, uygulama onu UTC sanır. Ne istisna vardır ne uyarı —
  üç yazımın üçü de sessizce yanlıştır.
- **2, değerin korunduğunu ama etiketin korunmadığını gösterir.** Rakamlar doğru gelir,
  `Kind` yine `Unspecified`'dır. `SpecifyKind` gereksiz görünen ama gerekli olan adımdır:
  onsuz `System.Text.Json` sonuna `Z` eklemez ve tarayıcı tarihi yerel saat sanar.
- **3, offset'in veritabanına kadar gittiğini gösterir.** Hem an hem de olayın yerel
  karşılığı korunur. Ama offset zaman dilimi değildir; yaz saati kuralları gerekiyorsa
  yine `TimeZoneInfo` gerekir.
- **4 ile 5 veritabanına hiç gitmez.** Aynı anı gösteren iki `DateTime` eşit sayılmaz,
  çünkü karşılaştırma `Kind`'ı yok sayıp yalnız rakamlara bakar. `DateTimeOffset` bu
  tuzağı taşımaz.

**Zaman dilimi makineden alınmıyor.** `DateTime.Now` kullanılsaydı ölçüm, testi çalıştıran
makinenin saat dilimine bağlı olurdu ve UTC'de duran bir CI sunucusunda hata hiç
görünmezdi. İstanbul'un `+03:00`'ı sabit yazılıdır.

## Çalıştır

```powershell
dotnet user-secrets set "ConnectionStrings:LabsConnection" "<baglanti-dizesi>" --project .
dotnet run
```

## Neden bu sayılar sabit

Üç an koda gömülüdür; ikisi bilerek gün sınırını aşacak şekilde seçilmiştir. Yazma
senaryoları transaction içinde çalışır ve geri alınır, tohum bozulmaz. Laboratuvarın
context'inde **bilerek global UTC `ValueConverter` yoktur**: konulsaydı 1. senaryodaki
kayıp görünmez olurdu.

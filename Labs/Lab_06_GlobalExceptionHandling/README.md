# Lab_06 — Global Exception Handling

İlgili yazı: [#6 — Global Exception Handling — Try-Catch Kaldırın, Middleware Yazın](https://blog.furkantural.com/Home/Post/6)

## İddia

Aynı beş hata iki uygulamada da fırlatılıyor: biri hatayı kimseye emanet etmiyor, diğeri
tek yerde topluyor. Try-catch'leri silmenin karşılığı daha az kod değil; **istemcinin
gördüğü durum kodunun hatanın türüyle eşleşmesi**.

## Kanıt

```text
Senaryo                       Durum  Beklenen      Süre     Bellek  Sonuç
───────────────────────────  ──────  ─────────  ───────  ─────────  ─────
1) Handler yok, NotFound        500  = 500        82 ms     362 KB  GEÇTİ
2) Handler var, NotFound        404  = 404        18 ms     201 KB  GEÇTİ
3) Handler var, Validation      400  = 400         1 ms       7 KB  GEÇTİ
4) Handler var, beklenmedik     500  = 500         2 ms      24 KB  GEÇTİ
5) Handler var, iptal           499  = 499         0 ms      11 KB  GEÇTİ
```

Okuma notları:

- **1 ile 2 aynı hatayı fırlatır.** Handler yokken iş kuralı ihlali sunucu hatası olarak
  dönüyor; istemci "kayıt yok" ile "sunucu bozuldu" arasındaki farkı göremiyor. Yeniden
  deneme mantığı da bu ikisini ayırt edemediği için yanlış karar verir.
- **3, deseni değerli kılan yer.** Yeni bir hata tipi eklemek tek bir `switch` kolu;
  uçların hiçbirine dokunulmuyor.
- **4, handler'ın işinin hataları gizlemek olmadığını gösterir.** Tanımadığı hatayı 500'e
  düşürüyor ama gövdeye özgün hata metnini koymuyor — laboratuvar bunu kontrol ediyor:
  yanıtın içinde bağlantı dizesi metni geçerse senaryo patlar.
- **5, yazı #8 ile birleştiği yer.** İptal bir hata değildir. 500 sayılırsa hata oranı
  paneliniz, kullanıcı sekmeyi kapattığı için alarma geçer. 499 standart bir HTTP kodu
  değil ama bu ayrımı yapmanın yaygın yolu.

## Çalıştır

```powershell
dotnet run
```

Veritabanı gerekmez.

## Neden bu sayılar sabit

Durum kodları istemcinin gerçekten aldığı kodlardır; iki uygulama da gerçek Kestrel
üzerinde çalışır. Handler `IExceptionHandler` arayüzünü kullanır (.NET 8+); yazıdaki
custom middleware çözümü de aynı tabloyu üretir, fark kurulum biçimindedir.

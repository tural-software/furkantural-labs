# Lab_05 — DI Scope Hataları

İlgili yazı: [#5 — Dependency Injection Scope Hataları — Singleton İçinde Scoped Servis Kullanımı](https://blog.furkantural.com/Home/Post/5)

## İddia

Aynı uygulamaya iki istek gidiyor ve her seferinde kaç **farklı** nesne üretildiği
sayılıyor. Captive dependency'nin belirtisi bir hata mesajı değil; bu sayının 1'de
kalmasıdır.

## Kanıt

```text
Senaryo                      Örnek  Beklenen      Süre     Bellek  Sonuç
──────────────────────────  ──────  ─────────  ───────  ─────────  ─────
1) Scoped servis                 2  = 2         112 ms     543 KB  GEÇTİ
2) Singleton servis              1  = 1           0 ms       8 KB  GEÇTİ
3) Singleton içinde Scoped       1  = 1           0 ms       8 KB  GEÇTİ
4) IServiceScopeFactory          2  = 2           1 ms       8 KB  GEÇTİ
5) ValidateOnBuild açık          0  = 0           4 ms     376 KB  GEÇTİ
```

Okuma notları:

- **1 ile 3'ün kaydı aynı.** İkisinde de `Probe` Scoped olarak kayıtlı. Fark, 3'te onu
  bir Singleton'ın tutması. Kayıt Scoped, davranış Singleton — ve kodun hiçbir yerinde
  yanlış görünen bir satır yok. `DbContext` olsaydı bu, bayat veri ya da
  `ObjectDisposedException` demekti.
- **4, çözümün Singleton'dan vazgeçmek olmadığını gösterir.** Servis hâlâ Singleton;
  değişen tek şey bağımlılığı **tutmuyor** olması. Her kullanımda `IServiceScopeFactory`
  ile yeni bir scope açılıyor ve sayı 2'ye dönüyor.
- **5, 3'ün neden mümkün olduğunu söyler.** Doğrulama açıkken aynı kayıtla uygulama hiç
  ayağa kalkmıyor: tek bir nesne bile üretilmiyor. Yani 3. senaryo yalnızca doğrulama
  kapalı olduğu için çalışabildi — varsayılan olarak Development'ta açık, Production'da
  kapalıdır. Hatanın üretimde ortaya çıkmasının sebebi budur.

## Çalıştır

```powershell
dotnet run
```

Veritabanı gerekmez.

## Neden bu sayılar sabit

`Probe` kimliğini kurucusunda artan bir sayaçtan alır; iki istek aynı kimliği görüyorsa
aradaki nesne aynı nesnedir. Singleton karşılaştırması ayrı bir tiple yapılır, böylece
iki senaryo aynı sayacı kirletmez.

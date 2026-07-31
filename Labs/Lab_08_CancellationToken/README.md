# Lab_08 — CancellationToken

İlgili yazı: [#8 — Cancellation Token — Neden Her Async Metoda Eklenmeli?](https://blog.furkantural.com/Home/Post/8)

## İddia

Her senaryoda 5 istek gönderilip 80 ms sonra iptal ediliyor. Sunucudaki iş 400 ms
sürüyor. Ölçülen şey, **kaçının buna rağmen sonuna kadar çalıştığı**.

## Kanıt

```text
Senaryo                       Tamamlanan  Beklenen      Süre     Bellek  Sonuç
────────────────────────────  ──────────  ─────────  ───────  ─────────  ─────
1) Jeton geçirilmiyor                  5  = 5        1070 ms     563 KB  GEÇTİ
2) Jeton geçiriliyor                   0  = 0        1067 ms     178 KB  GEÇTİ
3) Bağlı jeton + CancelAfter           0  = 0        1225 ms      64 KB  GEÇTİ
4) Jeton var, iptal yok                5  = 5        2619 ms      16 KB  GEÇTİ
```

Süre sütunu ölçümün kendisi değil: sayım yapılmadan önce sunucudaki işin bitmesi
bekleniyor, çünkü o iş istemciden bağımsız sürüyor. Zaten ölçmek istediğimiz de bu.

Okuma notları:

- **1 ile 2 arasındaki tek fark metot imzasındaki parametre.** İstemci 80 ms'de gitti;
  jetonsuz uçta beş isteğin beşi de 400 ms'lik işi sonuna kadar yaptı. Harcanan CPU ve
  tutulan bağlantı, kimsenin okumayacağı bir cevap için.
- **2'de sayı sıfır.** İş, istemciyle birlikte duruyor. `HttpContext.RequestAborted`
  jetonu uca parametre olarak geldiği için `Task.Delay` iptal edilir ve fırlatır.
- **3, iptalin her zaman istemciden gelmediğini gösterir.** Burada istemci beklemeye
  razıydı; işi 120 ms'de sunucunun kendi üst sınırı durdurdu. Bağlı jeton hem isteğin
  iptalini hem kendi süresini dinler.
- **4, jetonun yolun taşı olmadığını gösterir.** İptal yoksa beş isteğin beşi de tamamlanıyor.
  Jeton geçirmek işi kırmaz; yalnız iş gereksiz hâle geldiğinde durdurur.

## Çalıştır

```powershell
dotnet run
```

Veritabanı gerekmez. Laboratuvar bilerek yavaştır (~6 sn): iptalin ölçülebilmesi için
sunucudaki işin istemcinin vazgeçme anından belirgin biçimde uzun olması gerekir.

## Neden bu sayılar sabit

400 ms'lik iş ile 80 ms'lik vazgeçme arasındaki fark, zamanlama gürültüsünün çok üstünde.
Uçlar "sonuna kadar çalıştım" sayacını kendileri artırır; sayım, işin bitmesine izin
verildikten sonra okunur.

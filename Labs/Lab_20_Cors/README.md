# Lab_20 — CORS

İlgili yazı: [#20 — CORS — AllowAnyOrigin() Neden Hatayı Çözmez, Sadece Erteler?](https://blog.furkantural.com/Home/Post/20)

## İddia

Altı istek de sunucuya ulaşıyor ve işleniyor. Ölçülen şey, yanıtta isteğin origin'i için
`Access-Control-Allow-Origin` başlığının bulunup bulunmadığı — tarayıcının sonucu
JavaScript'e teslim edip etmeyeceğini belirleyen tek şey bu.

## Kanıt

```text
Senaryo                               Origin izni  Beklenen      Süre     Bellek  Sonuç
────────────────────────────────────  ───────────  ─────────  ───────  ─────────  ─────
1) CORS yapılandırılmamış                       0  = 0          78 ms     171 KB  GEÇTİ
2) WithOrigins, tam eşleşme                     1  = 1          15 ms     262 KB  GEÇTİ
3) WithOrigins, sonda eğik çizgi                0  = 0           4 ms     121 KB  GEÇTİ
4) AllowAnyOrigin + AllowCredentials            0  = 0           4 ms     397 KB  GEÇTİ
5) Preflight, UseCors auth'tan sonra            0  = 0           5 ms     110 KB  GEÇTİ
6) Preflight, UseCors auth'tan önce             1  = 1           1 ms       8 KB  GEÇTİ
```

> ⚠️ **4. satır yazıyla çelişiyor.** Yazı şunu söylüyor: *"Uygulama bu kodla sorunsuz
> başlar… policy `Lazy<CorsPolicy>` olarak saklanır; `Build()` ancak CORS middleware'ine
> düşen ilk istekte koşar… o ilk istekte `InvalidOperationException` fırlar ve 500
> dönersiniz."* .NET 10'da ölçüm bunu doğrulamıyor: `CorsOptions.AddPolicy` policy'yi
> **kendisi build eder** ve sonucu `Lazy` içine sarar. Bu da `CorsService` kurulurken, yani
> CORS middleware'i inşa edilirken, yani `app.StartAsync()` sırasında olur.
> **Uygulama hiç ayağa kalkmaz.** İstisnanın türü ve mesajı aynı; değişen tek şey ne zaman
> ortaya çıktığı — ve bu iyi yönde bir değişim: hata artık üretimdeki ilk çapraz kaynak
> isteğinde değil, açılışta görünüyor. Yazının bu paragrafı ile "inline overload'da
> uygulama hiç ayağa kalkmaz" nüansı güncellenmeli.

Okuma notları:

- **1, konunun tamamını özetler.** Sunucu 200 döndü, iş yapıldı, yanıt üretildi. Sunucuda
  bozulan hiçbir şey yok; eksik olan tek şey bir başlık. Aynı istek `curl` ile çalışır,
  tarayıcıda çalışmaz — CORS bir yetkilendirme katmanı değil, tarayıcının kendi kullanıcısını
  koruma kuralıdır.
- **3, saatler yakan satır.** Tek fark origin'in sonundaki eğik çizgi.
  `WithOrigins` karşılaştırmayı metin olarak yapar; `https://app.lab.local/` ile
  `https://app.lab.local` aynı değildir.
- **5 ile 6 arasındaki tek fark iki satırın yeri.** Ön kontrol isteği kimlik bilgisi
  taşımaz; `UseCors` yetkilendirmeden sonra çalışırsa ön kontrol **401** alır ve asıl istek
  tarayıcı tarafından hiç gönderilmez. Konsolda göreceğiniz mesaj yetki hatasından değil
  CORS'tan bahseder — hatayı aradığınız yer baştan yanlış olur.
- **5'in koşulu önemli.** Bu senaryonun ölçülebilmesi için ucun CORS metadata'sı taşıması
  gerekir (`RequireCors` ya da `[EnableCors]`). Yönlendirme, ön kontrol isteğini ancak bu
  metadata varsa uca eşler; yoksa eşleşme olmaz, yetkilendirme hiç çalışmaz ve CORS
  middleware'i sırası ne olursa olsun ön kontrolü cevaplar. Metadata'sız kurulumda bu tuzak
  yoktur — sorunu görmek için kurulumun bu ayrıntısını bilmek gerekiyor.

## Çalıştır

```powershell
dotnet run
```

Veritabanı gerekmez; laboratuvar beş ayrı uygulamayı rastgele portlarda ayağa kaldırır.
Dördüncü senaryodaki uygulama bilerek ayağa kalkmaz.

## Neden bu sayılar sabit

Tarayıcı taklit edilmiyor: ölçülen şey **sunucunun gönderdiği başlık**, tarayıcının o
başlıkla ne yapacağı değil. Kural spesifikasyonda yazılıdır ve tek satırdır — izin,
`Access-Control-Allow-Origin` değerinin isteğin origin'ine (credential yoksa jokere) eşit
olmasıdır. Durum kodları tabloya karıştırılmaz; senaryonun içinde ayrıca doğrulanır, sapma
olursa laboratuvar sessizce yanlış ölçmek yerine patlar.

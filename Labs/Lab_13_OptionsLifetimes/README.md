# Lab_13 — IOptions / IOptionsSnapshot / IOptionsMonitor

İlgili yazı: [#13 — IOptions vs IOptionsSnapshot vs IOptionsMonitor — Konfigürasyon Değişince Neden Uygulamayı Yeniden Başlatıyorsunuz?](https://blog.furkantural.com/Home/Post/13)

## İddia

Uygulama ayakta çalışırken dosyadaki limit 10'dan 20'ye çekiliyor. Altı tüketici de aynı
ayarı okuyor; ölçülen şey **her birinin okuduğu sayı**. Farkı yaratan seçilen arayüz ve
değerin okunduğu an.

## Kanıt

```text
Senaryo                                         Okunan  Beklenen      Süre     Bellek  Sonuç
──────────────────────────────────────────────  ──────  ─────────  ───────  ─────────  ─────
1) IOptions<T>                                      10  = 10          0 ms      112 B  GEÇTİ
2) IOptionsSnapshot<T>, yeni kapsam                 20  = 20          2 ms       5 KB  GEÇTİ
3) Singleton, CurrentValue kullanımda               20  = 20          0 ms      112 B  GEÇTİ
4) Singleton, CurrentValue ctor'da kopyalanmış      10  = 10          0 ms      112 B  GEÇTİ
5) Singleton'a IOptionsSnapshot enjekte             10  = 10          0 ms      112 B  GEÇTİ
6) reloadOnChange kapalı, IOptionsMonitor           10  = 10          0 ms      112 B  GEÇTİ
```

Altı tüketicinin yalnız ikisi yeni değeri gördü. Diğer dördü hata vermedi, uyarı üretmedi,
log'a hiçbir şey yazmadı — eski sayıyla çalışmaya devam etti.

Okuma notları:

- **3 ile 4 arasındaki fark tek satır.** İkisi de singleton, ikisi de `IOptionsMonitor`
  alıyor. Biri değeri kullanım noktasında okuyor, diğeri constructor'da bir alana
  kopyalıyor. Kopyalandığı anda "canlı" arayüz `IOptions` davranışına dönüyor.
- **5, yazının "en kötü hâlde hiçbir şey patlamaz" dediği yer.** Scoped olan
  `IOptionsSnapshot`, singleton bir servise enjekte edilmiş. Kapsam doğrulaması
  (`ValidateScopes`) varsayılan host'ta yalnız Development ortamında açıktır; burada kapalı
  olduğu için kayıt sorunsuz kuruluyor ve değer sessizce donuyor. Geliştirici makinesinde
  istisna fırlatan kod, üretimde hiçbir şey söylemeden yanlış çalışıyor.
- **6'da arayüz canlı ama kaynak sessiz.** `AddJsonFile(path)` çağrısına
  `reloadOnChange: true` verilmezse dosya izlenmez; `IOptionsMonitor` değişimi hiç duymaz.
  Doğru arayüzü seçmek tek başına yetmiyor.
- **1 bir hata değil.** Sürecin ömrü boyunca sabit kalan ayarlar — bağlantı dizeleri gibi —
  için `IOptions<T>` zaten doğru seçimdir. Kural basit: değer değişebiliyorsa
  `IOptionsMonitor`, istek boyunca sabit olmalıysa `IOptionsSnapshot`, hiç değişmiyorsa
  `IOptions`.

## Çalıştır

```powershell
dotnet run
```

Veritabanı ve sunucu gerekmez. Laboratuvar ayar dosyasını kendi çıktı klasöründe oluşturur
ve çalışırken değiştirir.

## Neden bu sayılar sabit

Tüketicilerin hepsi dosya değişmeden **önce** çözümlenir; sonra çözümlenselerdi hepsi yeni
değeri görür ve laboratuvar hiçbir şey ölçmemiş olurdu. Bu sıra, uygulamanın açılışta
ayarları okumasının karşılığıdır.

Yeniden okuma dosya sistemi bildirimiyle tetiklenir, yani eşzamansızdır. Sabit bir bekleme
koymak yerine bildirimin kendisi beklenir (`OnChange`); on beş saniyede gelmezse laboratuvar
sessizce yanlış ölçmek yerine gürültüyle durur. "Kapsam" burada bir HTTP isteğinin
karşılığıdır — `IOptionsSnapshot`'ın yeniden hesaplandığı sınır tam olarak budur.

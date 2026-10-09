# Lab_30 — BackgroundService

İlgili yazı: [#30 — BackgroundService](https://blog.furkantural.com/Home/Post/30)

## İddia

Her senaryoda aynı **10 iş** kuyrukta duruyor. İşin kendisi hiç değişmiyor: aynı döngü,
aynı sayaç. Değişen tek şey işi barındıran host'un semantiği — açılışın neyi beklediği,
`ExecuteAsync`'ten kaçan istisnaya ne yapıldığı, kapanış token'ına saygı duyulup
duyulmadığı.

Ölçülen şey tamamlanan iş sayısı.

## Kanıt

```text
Senaryo                                              Tamamlanan iş  Beklenen      Süre     Bellek  Sonuç
───────────────────────────────────────────────────  ─────────────  ─────────  ───────  ─────────  ─────
1) Döngü ilk await'ten önce, StartAsync döndüğünde               0  = 0          47 ms     291 KB  GEÇTİ
2) Döngü ilk await'ten sonra, StartAsync döndüğünde              0  = 0           4 ms     232 KB  GEÇTİ
3) Kaçan istisna, patlayan işçi                                  3  = 3         269 ms     198 KB  GEÇTİ
4) İstisna sonrası kardeş işçi, varsayılan davranış              0  = 0           5 ms     194 KB  GEÇTİ
5) İstisna sonrası kardeş işçi, Ignore                          10  = 10        508 ms     191 KB  GEÇTİ
6) İş başına try/catch                                           9  = 9         263 ms     187 KB  GEÇTİ
7) Kapanışta stoppingToken'a saygılı                             3  = 3         267 ms     212 KB  GEÇTİ
8) Kapanışta stoppingToken yok sayılıyor                        10  = 10        260 ms     187 KB  GEÇTİ
```

Okuma notları:

- **1 ve 2, yaygın bir tavsiyenin çürüdüğü yer.** "`ExecuteAsync`'e erken bir `await` koy,
  yoksa uygulamanın açılışını bloklarsın" denir. Bu doğruysa 1. senaryoda 10 görülmek
  zorundaydı: o döngü tamamen senkron ve ilk `await`'ten önce, yani host onu beklediyse
  bitmiş olmalıydı. Ölçülen 0. İki kurgu açılış açısından **ayırt edilemiyor**; tavsiye
  bu sürümde bir şey satın almıyor.
- **3, istisnanın gerçek bedeli.** Bozuk iş 4. sırada; öncesindeki 3 iş tamamlandı, sonrası
  hiç denenmedi. `ExecuteAsync`'ten çıkan istisna döngüyü olduğu yerde kesiyor ve işçi bir
  daha başlamıyor — barındırma katmanında yeniden deneme diye bir şey yok.
- **4, tablonun sebebi.** Ölçülen şey patlayan işçi değil **kardeşi**: ayrı, sağlıklı bir
  işçi patlamadan sonra tek bir iş bile tamamlayamıyor. Varsayılan
  `BackgroundServiceExceptionBehavior.StopHost`, kaçan istisnada yalnız o işçiyi değil
  bütün uygulamayı indiriyor. Bir işçinin hatası, ona hiç dokunmayan işleri de bitiriyor.
- **5, aynı istisna, tek ayar farkı.** `Ignore` ile host yaşıyor ve kardeş işçi 10 işin
  10'unu bitiriyor. Patlayan işçi yine 3'te kalıyor — yani bu ayar hatayı onarmıyor,
  yalnızca **görünmez** kılıyor: işçi sessizce ölüyor ve kimse haber vermiyor.
- **6, kararın verilmesi gereken yer.** Aynı bozuk iş, bu kez her iş kendi `try/catch`'inde.
  İstisna `ExecuteAsync`'ten hiç çıkmadığı için host davranışı devreye girmiyor: yalnız
  bozuk iş atlanıyor, kalan 9 iş tamamlanıyor. Çözüm `HostOptions`'ta değil, döngünün içinde.
- **7 ve 8, kapanışın kime ait olduğu.** Aynı noktada aynı `StopAsync`. Token okunursa kalan
  işlere hiç başlanmıyor (3); okunmazsa işçi hepsini bitiriyor (10) ve host onu beklemek
  zorunda kalıyor. Kapanış süresi en yavaş işçinin hızına düşüyor; iş `ShutdownTimeout`'u
  aşsaydı süreç ortasından kesilirdi.

## Çalıştır

```powershell
dotnet run
```

Veritabanı gerekmez. Laboratuvar kendi host'unu süreç içinde kuruyor.

## Neden bu sayılar sabit

Beklentiler ölçümden değil **barındırma sözleşmesinden** geliyor ve koda gömülü:

- `ExecuteAsync`'ten kaçan istisna döngüyü keser, yeniden denenmez → 3
- `StopHost` kapanmayı yürütür, kardeşin penceresi iptal edilir → 0
- `Ignore` yalnız o işçiyi bitirir, host yaşar → 10
- iş başına `try/catch` istisnayı dışarı çıkarmaz → 9
- `stoppingToken` okunursa kalan işe başlanmaz → 3, okunmazsa host bekler → 10

**1 ve 2 tek istisna: beklentileri yanlış bir gerekçeyle yazılmıştı.** İlk kurguda 10
bekleniyordu, çünkü `BackgroundService`'in `ExecuteAsync`'i ilk `await`'ine kadar senkron
koşturduğu ve host'un açılışı buna bağladığı varsayılmıştı. Ölçüm bunu çürüttü: düzeltilen
şey beklenti değil, **gerekçeydi**; senaryolar ölçümün gösterdiği davranışa göre yeniden
kuruldu.

Buna şu itiraz yapılabilir: "işler `await`'li olduğu için host ilk iç `await`'te serbest
kalmıştır, dolayısıyla 0 bir şey kanıtlamaz." İtiraz bu laboratuvar için geçerli değil —
1 ve 2. senaryolarda iş `Interlocked.Increment`'tir, döngüde tek bir `await` yoktur. Host
açılışı `ExecuteAsync`'e bağlasaydı o döngünün bitmiş olması zorunluydu.

Mekanizma ayrıca bu laboratuvarın düzeneğinden bağımsız, izole bir koşumla doğrulandı.
`ExecuteAsync`'i tamamen senkron olan (`Thread.Sleep(1500)` ve ardından sayaç döngüsü,
ilk `await`'ten önce) tek servisli bir host'ta:

```text
StartAsync süresi          : 6 ms
StartAsync'i çağıran thread: 2
ExecuteAsync'in thread'i   : 6      → farklı thread
StartAsync anında sayaç    : 0
ExecuteAsync'e girildi mi  : True
2,5 sn sonra sayaç         : 10
```

Yani host `ExecuteAsync`'e giriyor ama onu **başka bir thread'e gönderiyor** ve senkron ön
ekini beklemiyor. Bu kontrol denetlenen tabloda yer almıyor, çünkü tablo süre ve thread
değil iş ölçüyor; buraya gerekçenin dayanağı olarak not edilmiştir.

**4. senaryo host'u `RunAsync` ile sürüyor** ve bu bilinçli bir seçim. Elle `StartAsync` ile
sürülen bir host'ta kaçan istisna kapanmayı *ister* (`ApplicationStopping` tetiklenir) ama
kimse yürütmez; `BackgroundService`'in `stoppingToken`'ı yalnız servisin `StopAsync`'inde
iptal edildiği için kardeş işçi hiç kesilmez. O kurguda senaryo 0 değil 10 ölçüyordu —
ölçtüğü şey host'un davranışı değil, koşum düzeneğinin eksiğiydi.

Ölçüm zamana değil **açık sinyallere** bağlı: patlama anı bir `TaskCompletionSource` ile
bildiriliyor, açılış ölçümünün kapısı hiç açılmıyor, kapanış ölçümü ilk partinin bitiş
sinyalini bekliyor. Karar pencereleri yalnızca kapanış kararının yayılmasına alan bırakmak
için var ve süreç içi bir sinyal için fazlasıyla geniş. Laboratuvar üst üste dört kez
koşturuldu ve dördünde de aynı tabloyu verdi.

Süre ve bellek sütunları denetlenmez ve yazıda kullanılmaz. 1. senaryo ilk koşan senaryo
olduğu için barındırma altyapısının kurulumunu üstleniyor (47 ms, 291 KB) — bu sayı
senaryonun maliyeti değil, ölçüm sırasının. Denetlenen tek sayı tamamlanan iş adedidir.

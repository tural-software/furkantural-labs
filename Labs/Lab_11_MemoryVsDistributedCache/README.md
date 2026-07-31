# Lab_11 — Memory Cache vs Distributed Cache

İlgili yazı: [#11 — Memory Cache vs Distributed Cache — ASP.NET Core'da Doğru Cache Seçimi](https://blog.furkantural.com/Home/Post/11)

## İddia

Her senaryoda load balancer arkasında iki sunucu var ve ikisine de aynı anahtar soruluyor.
Ölçülen şey, **kaçının değeri okuyabildiği** — cache seçiminin tek gözlenebilir farkı bu.

## Kanıt

```text
Senaryo                                            Gören  Beklenen      Süre     Bellek  Sonuç
────────────────────────────────────────────────  ──────  ─────────  ───────  ─────────  ─────
1) IMemoryCache, A yazdı                               1  = 1           1 ms      25 KB  GEÇTİ
2) IDistributedCache, A yazdı                          2  = 2          21 ms      34 KB  GEÇTİ
3) IMemoryCache, A anahtarı düşürdü                    1  = 1           0 ms      472 B  GEÇTİ
4) IDistributedCache, A anahtarı düşürdü               0  = 0          16 ms      19 KB  GEÇTİ
5) IMemoryCache, iki sunucu yeniden başladı            0  = 0           1 ms      29 KB  GEÇTİ
6) IDistributedCache, iki sunucu yeniden başladı       2  = 2         122 ms     110 KB  GEÇTİ
```

Dağıtık cache **gerçekten süreç dışındadır**: kayıt SQL Server'daki bir tabloda durur.
Redis yerine SQL Server seçildi çünkü laboratuvar `dotnet run` dışında kurulum istemez ve
veritabanı zaten diğer laboratuvarların bağımlılığı. Kanıtlanan özellik sağlayıcıya bağlı
değil — cache'in **süreçlerin dışında** durması.

Okuma notları:

- **1, yazının açılış cümlesinin ölçümü.** Tek sunucuda `IMemoryCache` kusursuz görünür;
  ikinci sunucu eklendiği an aynı anahtar için iki farklı cevap ortaya çıkar.
- **3, 1'den daha sinsi.** Burada doğru sayı 0'dı: veri değişti ve yazma isteğini alan
  sunucu anahtarı düşürdü. Ama düşürme yalnız kendi belleğinde oldu; diğer sunucu bayat
  kopyayı süresi dolana kadar servis etmeye devam ediyor. Süre uzattıkça sorun büyür.
- **4, dağıtık cache'in asıl kazancıdır.** Kazanç "daha hızlı" değil, geçersizleştirmenin
  yayılmak zorunda olmaması: tek yerden silinen kayıt herkes için silinmiştir.
- **5 ile 6'yı deploy anında düşünün.** Bellek içi cache her yeniden başlatmada sıfırlanır;
  yeni sürüm ayağa kalktığında ilk isteklerin hepsi ıskalar ve yük doğrudan veritabanına
  biner. Dağıtık cache'te kayıt süreçten bağımsız yaşadığı için böyle bir çukur oluşmaz.
- **Bu tablo `IMemoryCache`'i kötülemek için değil.** 1 ms ile 21 ms arasındaki fark da
  ölçümün içinde: dağıtık cache her okumada ağ ve serileştirme öder. Tek sunucuda ya da
  gerçekten yerel veride (lookup tabloları, statik config) bellek içi cache doğru seçimdir.

## Çalıştır

```powershell
dotnet run
```

Veritabanı gerekir. Cache tablosu ilk çalıştırmada oluşturulur; `dotnet sql-cache create`
aracına gerek yok, şema laboratuvarın içinde tanımlı.

## Neden bu sayılar sabit

İki sunucu iki ayrı servis kabıdır: `IMemoryCache` kabın içinde yaşadığı için belleği de
ayrıdır, `IDistributedCache` ise ikisinde de aynı tabloya bakar. Yeniden başlatma yeni kap
kurularak taklit edilir — `IMemoryCache`'in ömrü sürecin değil kabın ömrü olduğundan sonuç
gerçek bir restart ile aynıdır. Anahtar her koşumun başında ve sonunda silinir; laboratuvar
paylaşılan veritabanında kalıntı bırakmaz.

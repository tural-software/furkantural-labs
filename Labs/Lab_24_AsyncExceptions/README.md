# Lab_24 — Async'te Kaybolan İstisnalar

İlgili yazı: [#24 — Async istisnaları](https://blog.furkantural.com/Home/Post/24)

## İddia

Her senaryoda aynı üç iş çalışıyor ve üçü de `LabException` fırlatıyor. Ölçülen şey,
çağıranın `catch (LabException)` bloğuna **kaçının ulaştığı** — "haberim oldu mu"
sorusunun sayısal karşılığı.

Değişen tek şey işlerin nasıl beklendiği. İşler, hatalar ve istisna tipi sekiz senaryoda
da aynı.

## Kanıt

```text
Senaryo                                 Görülen  Beklenen      Süre     Bellek  Sonuç
──────────────────────────────────────  ───────  ─────────  ───────  ─────────  ─────
1) await Task.WhenAll                         1  = 1          12 ms      32 KB  GEÇTİ
2) WhenAll + Exception.InnerExceptions        3  = 3           9 ms      13 KB  GEÇTİ
3) Sıralı await, tek try                      1  = 1           1 ms       2 KB  GEÇTİ
4) Sıralı await, iş başına try                3  = 3           0 ms       4 KB  GEÇTİ
5) Beklenmeyen task                           0  = 0           1 ms      11 KB  GEÇTİ
6) async void                                 0  = 0           2 ms       3 KB  GEÇTİ
7) .Result ile bloke                          0  = 0           0 ms       3 KB  GEÇTİ
8) GetAwaiter().GetResult()                   1  = 1           0 ms       3 KB  GEÇTİ
```

Sütun, çağıranın beklediği **tipteki** istisnayı sayar. `catch (Exception)` ile ölçülseydi
7. senaryo 1 görünür ve sarmalama tuzağı tabloda hiç belirmezdi.

Okuma notları:

- **1, herkesin yazdığı satır.** Üç iş de çalışır, üçü de patlar, `await` yalnız ilkini
  yeniden fırlatır. Diğer iki hata kaybolmuş değil — sorulmamış durumda. Üç servise
  paralel giden bir işlemde ikisinin neden bozuk olduğunu log'da aramanın sebebi bu satır.
- **2, aynı çağrının hatalarını sorunca.** Tek fark `Task` nesnesinin elde tutulması:
  `all.Exception.InnerExceptions` üçünü de verir. Eksik olan `await` değil, sorgulamaydı.
- **3, aynı sayı bambaşka bir sebep.** Burada da 1 görülüyor ama 1. senaryodan farklı
  olarak **2 iş hiç çalışmadı**: ilk hata döngüyü kırdı. "Bir kayıt bozuktu, kalan 4.999'u
  da işlenmedi" tablosu tam olarak budur — ve tabloda 1. senaryodan ayırt edilemez, kodu
  okumak gerekir.
- **4, tek parantezin yeri.** `try` döngünün içine alındığında üç iş de çalışıyor ve üç
  hata da görülüyor. 3 ile 4 arasındaki fark, sıralı toplu işlemede istenen davranışın
  tamamı.
- **5, hatanın hiçbir yere gitmediği hâl.** `await` atılınca çağıranın `catch`'i task'a
  bağlı değildir; üç iş de patlar, kimse görmez. .NET bu durumda uyarı da vermez —
  gözlenmemiş istisnalar varsayılan ayarda süreci etkilemez.
- **6, `async void`.** `try` bloğunun içinden çağrılmış olması hiçbir şey değiştirmiyor:
  istisna çağırana dönmez, `SynchronizationContext`'e gider. Laboratuvar en küçük bağlamı
  kurup istisnaları orada tutuyor; **bağlamı olmayan bir konsolda aynı kod süreci
  sonlandırırdı.** Sayının 0 olması "hata yok" demek değil, "çağıranın haberi yok" demek.
- **7, çalışan `catch`'in sessizce devre dışı kalması.** `.Result` istisnayı
  `AggregateException` içine sarar; `catch (LabException)` artık tutmaz. Senkron koddan
  async'e geçerken kırılan şey çoğu zaman çağrı değil, çağrının etrafındaki hata yönetimi.
- **8, aynı bloke etme, sarmalama açık.** `GetAwaiter().GetResult()` iç istisnayı olduğu
  gibi fırlatır ve `catch` yeniden tutar. Bloke etmenin diğer bedelleri (thread tutma,
  bağlamı olan ortamlarda kilitlenme) yerinde duruyor; giden yalnız kaybolan istisna.

## Çalıştır

```powershell
dotnet run
```

Veritabanı gerekmez.

## Neden bu sayılar sabit

Beklentiler ölçümden değil dilin kurallarından geliyor ve koda gömülü:

- `await` birden çok hatadan yalnız ilkini yeniden fırlatır → 1
- `Task.Exception` hepsini `AggregateException` içinde tutar → 3
- sıralı `await`'te ilk hata kalan işleri hiç başlatmaz → 1
- `try` iş başına alınırsa hepsi çalışır ve hepsi görülür → 3
- beklenmeyen task ile `async void` çağırana bir şey döndürmez → 0
- `.Result` istisnayı sarar, beklenen tip tutmaz → 0
- `GetAwaiter().GetResult()` sarmalamayı açar → 1

Sekizi de ilk koşuda tuttu.

İşler `Task.Yield()` ile başlıyor: gerçek async kod ilk `await`inde çağırana döner ve
istisna senkron yoldan değil task üzerinden ulaşır. Senkron fırlatan sahte bir iş, 5. ve
6. senaryodaki tuzakları ortadan kaldırır ve tabloyu anlamsızlaştırırdı.

`async void` senaryosu, beklenen üç istisna bağlama düşene kadar bekler; sayı bir zaman
aşımına ya da koşuya bağlı değil. 5. senaryodaki beklenmemiş task'lar ölçüm **bittikten
sonra** gözlenir — ölçülen sayıya etkisi yoktur, amaç gözlenmemiş hataların sonraki
senaryolara gürültü olarak taşınmamasıdır.

Süre ve bellek sütunları denetlenmez ve yazıda kullanılmaz: değerler KB ölçeğinde ve
koşudan koşuya oynuyor (5. senaryo 5–11 KB arasında geziniyor). Denetlenen tek sayı
görülen istisna adedidir ve dört koşuda da birebir aynı çıktı.

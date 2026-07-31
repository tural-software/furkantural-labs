# Lab_15 — Channel&lt;T&gt;

İlgili yazı: [#15 — Channel&lt;T&gt; — Arka Plan İşlerini ConcurrentQueue ile Kuyruğa Almak Neden Yanlış?](https://blog.furkantural.com/Home/Post/15)

## İddia

Dört tüketici de bir saniye çalışıyor. Ölçülen şey, **uyanıp ortada iş bulamadıkları kere
sayısı** — elle yazılmış kuyruğun asıl maliyeti bu.

## Kanıt

```text
Senaryo                             Boşa uyanma  Beklenen      Süre     Bellek  Sonuç
──────────────────────────────────  ───────────  ─────────  ───────  ─────────  ─────
1) ConcurrentQueue + Delay(500 ms)            2  2–4        1019 ms      32 KB  GEÇTİ
2) ConcurrentQueue + Delay(50 ms)            17  12–30      1008 ms       6 KB  GEÇTİ
3) Channel, hiç iş gelmedi                    0  = 0        1024 ms       4 KB  GEÇTİ
4) Channel, 20 iş / kapasite 5                0  = 0           9 ms      19 KB  GEÇTİ
```

Okuma notları:

- **1 ile 2'yi yan yana koyun: takas bu iki satırda.** Yoklama aralığı hem boşta harcanan
  uyanmayı hem de işin ne kadar bekleyeceğini belirler. Gecikmeyi on kat düşürmek için
  aralığı kısalttınız — boşa uyanma da yaklaşık on kat arttı. Bu takasın kendisi yapay bir
  problemdir; ölçülen şey de tam olarak onun bedeli.
- **3'te sayı sıfır ve bu bir tanım gereği değil.** Tüketici döngüsü bilerek
  `ReadAllAsync` yerine `WaitToReadAsync` + `TryRead` ile yazıldı; böylece "uyandım ama iş
  yoktu" durumu **sayılabilir** hâle geliyor. `ReadAllAsync` kullanılsaydı sayaç yapısı
  gereği sıfır kalırdı ve ölçüm bir şey kanıtlamazdı. Bir saniye boyunca tüketici hiç
  uyanmadı: yoklama yok, ayarlanacak süre de yok.
- **4, kapasite sınırının işi düşürmediğini gösterir.** Kapasite 5, gönderilen iş 20.
  `FullMode.Wait` ile üretici yer açılana kadar bekledi; 20 işin 20'si sırayla işlendi ve
  laboratuvar bunu ayrıca doğruluyor. Aynı senaryoda en kötü gecikmenin 50 ms'nin altında
  kaldığı da kontrol ediliyor — iş, geldiği anda işleniyor.
- **Sınırsız kanal bir kaçış yolu değil.** Yazma tarafını hiç bloklamaz, ama sorunu çözmez;
  ertelenmiş bir `OutOfMemoryException`'a çevirir. Laboratuvar bilerek `CreateBounded`
  kullanıyor: kapasite yazmak, kuyruk dolduğunda ne olacağına karar vermek demektir.

`FullMode` seçeneklerinin (`DropWrite`, `DropOldest`, `DropNewest`) karşılaştırması bu
tabloda **yok**: onları ölçmek "kaç öğe kabul edildi" birimini gerektirir ve bir laboratuvar
tek birim ölçer. Burada `Wait` davranışı 4. senaryonun içinde doğrulanıyor.

## Çalıştır

```powershell
dotnet run
```

Veritabanı ve sunucu gerekmez. Laboratuvar ~3 saniye sürer: üç senaryo bir saniyelik boş
pencereyi gerçekten bekler.

## Neden bu sayılar sabit

Yoklamalı senaryoların beklentisi tek sayı değil aralık, çünkü ölçülen şey duvar saatine
bağlıdır: bir saniyede 500 ms'lik aralıkla ~2, 50 ms'lik aralıkla ~20 uyanma olur. Kanıt
kesin sayıda değil **büyüklük mertebesinde**: 0'a karşı 2'ye karşı 17.

Channel senaryolarının beklentisi kesindir, çünkü orada zamanlama yoktur — tüketici ya
uyanmıştır ya uyanmamıştır.

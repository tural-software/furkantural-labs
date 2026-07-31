# Lab_18 — Structured Logging

İlgili yazı: [#18 — Structured Logging — Log Satırına String Interpolation Yazmak Neden Aramayı İmkânsızlaştırır?](https://blog.furkantural.com/Home/Post/18)

## İddia

Altı kayıt da konsolda okunduğunda aynı bilgiyi veriyor. Ölçülen şey, sink'e kaç
**adlandırılmış alanın doğru değeriyle** ulaştığı — sorgulanabilirliği belirleyen tek şey bu.

## Kanıt

```text
Senaryo                      Doğru alan  Beklenen      Süre     Bellek  Sonuç
───────────────────────────  ──────────  ─────────  ───────  ─────────  ─────
1) String interpolation               0  = 0           4 ms       2 KB  GEÇTİ
2) Message template                   2  = 2           1 ms       6 KB  GEÇTİ
3) Argüman sırası ters                0  = 0           0 ms       2 KB  GEÇTİ
4) BeginScope + template              3  = 3           0 ms       2 KB  GEÇTİ
5) Exception mesaja gömülü            0  = 0           0 ms       1 KB  GEÇTİ
6) Exception ayrı parametre           1  = 1           0 ms       2 KB  GEÇTİ
```

Ölçüm konsola basılan cümleye değil, sink'e ulaşan veriye bakar: kayıtları metne
çevirmeden saklayan bir `ILoggerProvider` yazıldı.

Okuma notları:

- **1 ile 2 arasındaki tek fark silinen bir `$` işareti.** Interpolation'da sink'e tek
  parça metin gidiyor; içinden `UserId`'yi geri çıkarmanın yolu yok. Şablon her çağrıda
  farklı olduğu için gruplama ve sayım da imkânsız.
- **3, 1'den daha tehlikeli.** Adlar doğru, argümanlar ters. İki alan da sink'e ulaşıyor,
  ikisi de yanlış değerde — yani log'unuz sessizce yanlış ve üstelik **sorgulanabilir**
  biçimde yanlış. Derleyici yakalamaz, çalışma zamanı hata vermez. `CA2254` yalnızca
  şablonun sabit olmadığı durumları yakalar; sıralama hatasını hiçbir analizör yakalamaz.
- **4'te üçüncü alan `BeginScope`'tan geliyor.** Aynı property'yi her satıra elle yazmak
  yerine bloğun tamamına iliştirmek. Ama scope'lar her hedefte kendiliğinden görünmez:
  Console provider'da `IncludeScopes` açık olmalıdır.
- **5 ile 6'yı karşılaştırın.** `ex.Message`'ı metne gömdüğünüzde exception nesnesi hiç
  taşınmaz: stack trace ve iç exception zinciri kaybolur. Ayrı parametre geçildiğinde
  laboratuvar iç zincirin korunduğunu ayrıca doğruluyor.

## Çalıştır

```powershell
dotnet run
```

Veritabanı ve sunucu gerekmez.

## Neden bu sayılar sabit

Beklenen ad/değer çiftleri koda gömülüdür ve "doğru alan" sayısı bu çiftlerle karşılaştırma
sonucudur. Kaydedici, kaydın kendi alanlarına ve scope alanlarına ayrı ayrı bakar; ikisi
`IEnumerable<KeyValuePair<string, object?>>` üzerinden okunur, çünkü kaydın durumu bir
liste, `BeginScope`'a verilen sözlük ise değildir — liste araması sözlüğü sessizce boş geçer.

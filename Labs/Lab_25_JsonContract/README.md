# Lab_25 — System.Text.Json Sözleşmesi

İlgili yazı: [#25 — Serileştirme sözleşmesi](https://blog.furkantural.com/Home/Post/25)

## İddia

Aynı altı alanlı kart ödemesi sekiz ayrı yoldan serileştirilip geri okunuyor. Ölçülen
şey, **kaç alanın özgün değeriyle geri geldiği**.

Nesne, alanları ve değerleri sekiz senaryoda da aynı. Değişen tek şey serileştiricinin
nesneyi hangi tip üzerinden gördüğü ve iki ucun hangi seçeneklerle çalıştığı.

## Kanıt

```text
Senaryo                             Geri gelen  Beklenen      Süre     Bellek  Sonuç
──────────────────────────────────  ──────────  ─────────  ───────  ─────────  ─────
1) Türemiş tip, uçtan uca                    6  = 6          46 ms     178 KB  GEÇTİ
2) Taban tipe atanmış referans               3  = 3           0 ms      10 KB  GEÇTİ
3) List<Odeme> içinde                        3  = 3           5 ms      22 KB  GEÇTİ
4) object olarak                             6  = 6           0 ms       4 KB  GEÇTİ
5) GetType() verilerek                       6  = 6           0 ms      632 B  GEÇTİ
6) [JsonDerivedType], tabana okuma           6  = 6           3 ms      65 KB  GEÇTİ
7) camelCase yaz, seçeneksiz oku             0  = 0           0 ms      25 KB  GEÇTİ
8) camelCase yaz, duyarsız oku               6  = 6           0 ms       2 KB  GEÇTİ
```

Sayılan alan, geri okunan nesnede **özgün değerini koruyan** alandır. Türemiş alanlar
yalnız nesne gerçekten türemiş tipe materyalize olduysa sayılır; taban tipe düşen bir
sonuçta o alanlar boş değil, **yoktur**.

Okuma notları:

- **1, ölçünün tabanı.** Bildirilen tip ile çalışma zamanı tipi aynı olduğunda kayıp yok.
  Kalan yedi satır bu satırdan sapmayı gösteriyor.
- **2, kaybı yapan satır serileştirme çağrısı değil.** `Odeme odeme = kart;` ataması
  yapıldığı anda sözleşme taban tipin sözleşmesi oluyor ve türemiş üç alan JSON'a hiç
  yazılmıyor. Servis imzası taban tip döndüren her uçta olan budur; nesne eksiksizdir,
  JSON değildir.
- **3, aynı kural koleksiyonun her elemanında.** `List<Odeme>` bildirildiği için her
  eleman taban sözleşmesiyle yazılıyor. Tek nesnede görülen kayıp listede eleman sayısı
  kadar tekrarlanır — ve liste dönen uçlarda fark etmesi daha da zordur.
- **4, kuralın tek istisnası ve en şaşırtıcı sonucu.** `object` bildirildiğinde
  System.Text.Json çalışma zamanı tipine bakar: taban tip 3 alan verirken `object` 6
  veriyor. Bildirim ne kadar belirsizse sözleşme o kadar geniş.
- **5, modele dokunmayan çözüm.** `Serialize(odeme, odeme.GetType())` çağrısı 2.
  senaryonun aynısı, tek fark tipin elle verilmesi. Yazma tarafını düzeltir.
- **6, okuma tarafını da düzelten tek seçenek.** 4 ve 5 doğru JSON üretir ama JSON'da
  tipin ne olduğu yazmaz; taban tipe okumak yine taban nesne verir.
  `[JsonDerivedType]` ayrımcıyı (`$type`) kayda koyduğu için taban tiple yazılıp taban
  tiple okunan nesne türemiş tipe materyalize oluyor.
- **7, tip doğru, JSON tam, sonuç sıfır.** Yazan taraf web varsayılanlarını
  (camelCase) kullanıyor, okuyan taraf seçeneksiz `JsonSerializer` çağırıyor ve varsayılan
  okuma büyük/küçük harfe **duyarlı**. Altı alan da JSON'da duruyor, hiçbiri eşleşmiyor;
  nesne varsayılan değerlerle doluyor. Ne istisna var ne uyarı.
- **8, aynı JSON, farklı seçenek.** Kaybı yapan JSON değildi, iki ucun farklı
  varsayılanlarla çalışmasıydı.

## Çalıştır

```powershell
dotnet run
```

Veritabanı gerekmez.

## Neden bu sayılar sabit

Beklentiler ölçümden değil System.Text.Json'ın sözleşmesinden geliyor ve koda gömülü:

- sözleşmeyi çalışma zamanı tipi değil **bildirilen** tip belirler → taban tiple 3
- `object` bildirilen tipse çalışma zamanı tipi kullanılır → 6
- tip açıkça verilirse ya da türemiş tip `[JsonDerivedType]` ile tanıtılırsa → 6
- varsayılan seçeneklerde okuma büyük/küçük harfe duyarlıdır → camelCase JSON ile 0
- `JsonSerializerDefaults.Web` duyarsızlığı açar → aynı JSON ile 6

Sekizi de ilk koşuda tuttu.

Beklenen değerlerin hiçbiri tipin varsayılanı değil: `Tutar` sıfırdan, `An`
`default(DateTime)`'dan, dizeler `null` ve boştan farklı. Bu bilerek — alan adı
eşleşmediğinde nesne varsayılan değerle dolar ve değerler varsayılana yakın seçilseydi
"geri geldi" ile "hiç okunamadı" ayırt edilemezdi. 7. senaryonun 0'ı bu seçime bağlı.

`[JsonDerivedType]` için ayrı bir tip çifti kullanıldı. Öznitelik ortak tabana konsaydı
2. ve 3. senaryolar da düzelir, yani ölçülen tuzak tabloda hiç görünmezdi; iki çiftin
alanları ve değerleri birebir aynı, tek fark tabanın türemiş tipi tanıması.

Süre ve bellek sütunları denetlenmez ve yazıda kullanılmaz: değerler koşudan koşuya
oynuyor (1. senaryo 166–178 KB arasında geziniyor, çünkü ilk çağrı serileştirici
sözleşmesini kurar). Denetlenen tek sayı geri gelen alan adedidir ve dört koşuda da
birebir aynı çıktı.

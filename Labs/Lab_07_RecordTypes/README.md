# Lab_07 — C# Record Types

İlgili yazı: [#7 — C# Record Types — Ne Zaman Class, Ne Zaman Record?](https://blog.furkantural.com/Home/Post/7)

## İddia

Her senaryoda mantıken **aynı** nesneler bir `HashSet`'e atılıyor. Ölçülen şey, kümede kaç
eleman kaldığı. Küme hem `Equals` hem `GetHashCode` kullanır; "değer eşitliği"nin pratikteki
karşılığı tam olarak budur.

## Kanıt

```text
Senaryo                             Benzersiz  Beklenen      Süre     Bellek  Sonuç
──────────────────────────────────  ─────────  ─────────  ───────  ─────────  ─────
1) class, 3 özdeş nesne                     3  = 3           2 ms       1 KB  GEÇTİ
2) record, 3 özdeş nesne                    1  = 1           3 ms      816 B  GEÇTİ
3) class + elle yazılmış eşitlik            1  = 1           1 ms      552 B  GEÇTİ
4) record, koleksiyon alanı                 2  = 2           2 ms      952 B  GEÇTİ
5) record, with { } kopyası                 1  = 1           0 ms      448 B  GEÇTİ
6) record, with { Id = 2 } kopyası          2  = 2           0 ms      448 B  GEÇTİ
```

Okuma notları:

- **1 ile 2 arasındaki tek fark bir keyword.** Aynı veriyi taşıyan üç nesne, class'ta üç
  ayrı eleman; record'da bir. Bu fark unit testlerde, `Distinct()` çağrılarında ve sözlük
  anahtarlarında sessizce sonucu değiştirir.
- **3, record'un neyi kısalttığını gösterir.** Sonuç 2 ile aynı — yani elle yazmak
  mümkündür. Fark, sınıfa yeni bir alan eklendiğinde `Equals` ve `GetHashCode`'u da
  güncellemeyi hatırlamak zorunda olmak. Unutulan alan sessiz bir hatadır: farklı nesneler
  eşit görünmeye devam eder.
- **4, değer eşitliğinin durduğu yer.** İki sepetin içeriği birebir aynı, ama liste
  nesneleri ayrı örnek. Derleyicinin ürettiği `Equals` alan alan karşılaştırır ve bir
  `List<T>` alanı için karşılaştırdığı şey listenin içeriği değil **referansı**dır.
  Koleksiyon taşıyan record'lar bu yüzden beklendiği gibi davranmaz.
- **6, `with`'in yüzeysel kopya ürettiğini gösterir.** Orijinal değişmedi — yazının
  söylediği doğru. Ama kopya ile orijinal **aynı liste nesnesini paylaşıyor**; laboratuvar
  bunu ayrıca doğruluyor. Kopyaya bir öğe eklerseniz orijinalde de görünür. "Immutable"
  garantisi record'un kendi alanları içindir, içindeki nesnelerin değil.

Yazının EF Core uyarısı bu laboratuvarda **ölçülmedi**: o soru eşitlik semantiğine değil
change tracking'e bakar ve başka bir birim gerektirir. Buradaki tablonun tek birimi
"kümede kaç eleman kaldı".

## Çalıştır

```powershell
dotnet run
```

Veritabanı ve sunucu gerekmez.

## Neden bu sayılar sabit

Ölçülen şey dilin ürettiği koddur, çalışma zamanı koşulları değil: aynı kaynak aynı
derleyiciyle her makinede aynı `Equals` ve `GetHashCode` üretir. `with`'in kopya ürettiği
ve orijinali değiştirmediği senaryonun içinde ayrıca doğrulanır; sapma olursa laboratuvar
tabloya yanlış sayı yazmak yerine patlar.

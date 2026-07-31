using FurkanTural_Labs_Application.Diagnostics;
using Lab_07_RecordTypes;

// Her senaryoda mantıken aynı olan nesneler bir kümeye atılıyor. Küme hem Equals hem
// GetHashCode kullanır; "değer eşitliği" iddiasının pratikteki karşılığı tam olarak budur.
var report = new LabReport(
    title: "Lab_07 — Değer eşitliği: aynı veriden kaç tane kalıyor",
    claim: "Her senaryoda mantıken aynı nesneler bir HashSet'e atılıyor. Ölçülen şey, " +
           "kümede kaç eleman kaldığı.",
    metric: "Benzersiz");

// ── 1. Class: referans eşitliği ──────────────────────────────────────────────
await report.MeasureAsync(
    "1) class, 3 özdeş nesne",
    Expectation.Exactly(3),
    () => Task.FromResult(new HashSet<OrderDto>(
    [
        new() { Id = 1, CustomerName = "Ali", Amount = 500 },
        new() { Id = 1, CustomerName = "Ali", Amount = 500 },
        new() { Id = 1, CustomerName = "Ali", Amount = 500 }
    ]).Count),
    note: "Üç nesne de aynı veriyi taşıyor ama üçü de ayrı sayıldı: karşılaştırılan şey referans.");

// ── 2. Record: değer eşitliği ────────────────────────────────────────────────
await report.MeasureAsync(
    "2) record, 3 özdeş nesne",
    Expectation.Exactly(1),
    () => Task.FromResult(new HashSet<OrderRecord>(
    [
        new(1, "Ali", 500),
        new(1, "Ali", 500),
        new(1, "Ali", 500)
    ]).Count),
    note: "Tek satırlık tanım. Equals ve GetHashCode derleyici tarafından üretiliyor.");

// ── 3. Aynı davranış, elle yazılmış ──────────────────────────────────────────
await report.MeasureAsync(
    "3) class + elle yazılmış eşitlik",
    Expectation.Exactly(1),
    () => Task.FromResult(new HashSet<OrderWithEquality>(
    [
        new() { Id = 1, CustomerName = "Ali", Amount = 500 },
        new() { Id = 1, CustomerName = "Ali", Amount = 500 },
        new() { Id = 1, CustomerName = "Ali", Amount = 500 }
    ]).Count),
    note: "Sonuç 2 ile aynı. Fark, alan eklendiğinde iki yeri güncellemeyi hatırlamak zorunda olmak.");

// ── 4. Değer eşitliğinin durduğu yer ─────────────────────────────────────────
await report.MeasureAsync(
    "4) record, koleksiyon alanı",
    Expectation.Exactly(2),
    () => Task.FromResult(new HashSet<Basket>(
    [
        new(1, ["elma", "armut"]),
        new(1, ["elma", "armut"])
    ]).Count),
    note: "İki sepetin içeriği aynı, listeleri ayrı örnek. Değer eşitliği burada referansa düşüyor.");

// ── 5. with: eşitliği bozmayan kopya ─────────────────────────────────────────
await report.MeasureAsync(
    "5) record, with { } kopyası",
    Expectation.Exactly(1),
    () =>
    {
        var original = new Basket(1, ["elma", "armut"]);
        var copy = original with { };

        if (ReferenceEquals(original, copy))
            throw new InvalidOperationException("with yeni nesne üretmedi; ölçüm anlamsız.");

        return Task.FromResult(new HashSet<Basket>([original, copy]).Count);
    },
    note: "Ayrı nesne, eşit değer. Kopyanın listesi de aynı örnek olduğu için eşitlik korunuyor.");

// ── 6. with: değişen alan ve yüzeysel kopya ──────────────────────────────────
await report.MeasureAsync(
    "6) record, with { Id = 2 } kopyası",
    Expectation.Exactly(2),
    () =>
    {
        var original = new Basket(1, ["elma", "armut"]);
        var updated = original with { Id = 2 };

        if (original.Id != 1)
            throw new InvalidOperationException("Orijinal değişti; with kopya üretmiyor.");

        // Yüzeysel kopya: yalnız belirtilen alan değişir, gerisi aynı referanstır.
        if (!ReferenceEquals(original.Items, updated.Items))
            throw new InvalidOperationException("Liste kopyalandı; with artık yüzeysel değil.");

        return Task.FromResult(new HashSet<Basket>([original, updated]).Count);
    },
    note: "Orijinal değişmedi ama iki nesne aynı listeyi paylaşıyor: kopyaya eklenen öğe ikisinde de görünür.");

return report.Print();

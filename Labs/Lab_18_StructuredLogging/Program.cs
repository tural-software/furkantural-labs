using FurkanTural_Labs_Application.Diagnostics;
using Lab_18_StructuredLogging;
using Microsoft.Extensions.Logging;

const int UserId = 4821;
const int ProductId = 7312;
const int OrderId = 5150;

var recorder = new RecordingLoggerProvider();
using var factory = LoggerFactory.Create(builder =>
{
    builder.AddProvider(recorder);
    builder.SetMinimumLevel(LogLevel.Trace);
});

var logger = factory.CreateLogger("Lab_18");

var report = new LabReport(
    title: "Lab_18 — Structured logging: sink'e veri mi gidiyor, cümle mi",
    claim: "Altı kayıt da konsolda okunduğunda aynı bilgiyi veriyor. Ölçülen, sink'e kaç " +
           "adlandırılmış alanın doğru değeriyle ulaştığı — sorgulanabilirliği belirleyen tek şey bu.",
    metric: "Doğru alan");

// ── 1. String interpolation ──────────────────────────────────────────────────
// Metin metot çağrılmadan önce kuruluyor: seviye kapalı olsa bile bu maliyet ödenir.
// Sink'e giden şey tek parça metindir; içinden UserId'yi geri çıkarmanın yolu yoktur.
await report.MeasureAsync(
    "1) String interpolation",
    Expectation.Exactly(0),
    () =>
    {
        recorder.Clear();
        logger.LogInformation($"Kullanıcı {UserId} {ProductId} ürününü sepete ekledi");

        return Task.FromResult(CorrectFields(recorder, (nameof(UserId), UserId), (nameof(ProductId), ProductId)));
    },
    note: "Şablon her çağrıda farklı; geriye yalnız metin içinde arama kalıyor.");

// ── 2. Message template ──────────────────────────────────────────────────────
await report.MeasureAsync(
    "2) Message template",
    Expectation.Exactly(2),
    () =>
    {
        recorder.Clear();
        logger.LogInformation("Kullanıcı {UserId} {ProductId} ürününü sepete ekledi", UserId, ProductId);

        return Task.FromResult(CorrectFields(recorder, (nameof(UserId), UserId), (nameof(ProductId), ProductId)));
    },
    note: "Tek fark silinen bir $ işareti; sink artık alan bazlı filtre ve gruplama yapabiliyor.");

// ── 3. Placeholder'lar sıraya göre eşleşir ───────────────────────────────────
// Adlar doğru, argümanlar ters. Derleyici yakalamaz, çalışma zamanı hata vermez,
// log sessizce yanlış olur. CA2254 yalnız sabit olmayan şablonu yakalar; bunu değil.
await report.MeasureAsync(
    "3) Argüman sırası ters",
    Expectation.Exactly(0),
    () =>
    {
        recorder.Clear();
        logger.LogWarning("Stok yetersiz {UserId} {ProductId}", ProductId, UserId);

        return Task.FromResult(CorrectFields(recorder, (nameof(UserId), UserId), (nameof(ProductId), ProductId)));
    },
    note: "İki alan da var, ikisi de yanlış değerde: 1)'den daha tehlikeli, çünkü sorgulanabilir.");

// ── 4. BeginScope ────────────────────────────────────────────────────────────
// Aynı property'yi her satıra elle yazmak yerine bloğun tamamına iliştirmek.
await report.MeasureAsync(
    "4) BeginScope + template",
    Expectation.Exactly(3),
    () =>
    {
        recorder.Clear();
        using (logger.BeginScope(new Dictionary<string, object?> { ["OrderId"] = OrderId }))
            logger.LogInformation("Kullanıcı {UserId} {ProductId} ürününü sepete ekledi", UserId, ProductId);

        return Task.FromResult(CorrectFields(
            recorder, (nameof(UserId), UserId), (nameof(ProductId), ProductId), (nameof(OrderId), OrderId)));
    },
    note: "Scope'lar her hedefte kendiliğinden görünmez; Console provider'da IncludeScopes açık olmalı.");

// ── 5. Exception mesajın içine gömülmüş ──────────────────────────────────────
await report.MeasureAsync(
    "5) Exception mesaja gömülü",
    Expectation.Exactly(0),
    () =>
    {
        recorder.Clear();
        var error = Failure();
        logger.LogError($"Sipariş {OrderId} işlenemedi: {error.Message}");

        if (recorder.Entries[0].Exception is not null)
            throw new InvalidOperationException("Exception yakalanmış görünüyor; ölçüm geçersiz.");

        return Task.FromResult(CorrectFields(recorder, (nameof(OrderId), OrderId)));
    },
    note: "Stack trace ve iç exception zinciri yok: elinizle sildiniz.");

// ── 6. Exception ayrı parametre ──────────────────────────────────────────────
await report.MeasureAsync(
    "6) Exception ayrı parametre",
    Expectation.Exactly(1),
    () =>
    {
        recorder.Clear();
        logger.LogError(Failure(), "Sipariş {OrderId} işlenemedi", OrderId);

        var entry = recorder.Entries[0];
        if (entry.Exception?.InnerException is null)
            throw new InvalidOperationException("İç exception zinciri korunmadı; ölçüm geçersiz.");

        return Task.FromResult(CorrectFields(recorder, (nameof(OrderId), OrderId)));
    },
    note: "Alanın yanında exception da tipiyle, stack trace'iyle ve iç zinciriyle duruyor.");

return report.Print();

// Beklenen ad/değer çiftlerinden kaçının sink'e doğru ulaştığını sayar.
static int CorrectFields(RecordingLoggerProvider recorder, params (string Name, object Value)[] expected)
{
    var entry = recorder.Entries.Single();
    return expected.Count(e => Equals(entry.Field(e.Name), e.Value));
}

// İç zinciri olan gerçekçi bir hata: 5. senaryoda neyin kaybolduğu görünsün.
static Exception Failure()
    => new InvalidOperationException("Ödeme sağlayıcısı reddetti", new TimeoutException("Bağlantı zaman aşımı"));

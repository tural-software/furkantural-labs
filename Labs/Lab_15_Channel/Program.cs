using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using FurkanTural_Labs_Application.Diagnostics;
using Lab_15_Channel;

var window = TimeSpan.FromSeconds(1);
const int Capacity = 5;
const int JobCount = 20;

var report = new LabReport(
    title: "Lab_15 — Boşta bekleyen tüketici gerçekten uyuyor mu",
    claim: $"Dört tüketici de {window.TotalSeconds:0} saniye çalışıyor. Ölçülen şey, " +
           "uyanıp ortada iş bulamadıkları kere sayısı.",
    metric: "Boşa uyanma");

// ── 1. Elle kuyruk, yarım saniyelik yoklama ──────────────────────────────────
await report.MeasureAsync(
    "1) ConcurrentQueue + Delay(500 ms)",
    Expectation.Between(2, 4),
    async () => (await Consumers.PollingAsync(new ConcurrentQueue<Job>(), window, TimeSpan.FromMilliseconds(500))).IdleWakeUps,
    note: "Ortada tek iş yok; döngü yine de saniyede iki kez uyanıp kuyruğu yokladı.");

// ── 2. Gecikmeyi düşürme denemesi ────────────────────────────────────────────
// Yazının işaret ettiği takas: Delay kısaltılınca iş daha erken görülür, ama boşa
// uyanma sayısı aynı oranda büyür. Aynı sütunda iki satır, takası doğrudan gösteriyor.
await report.MeasureAsync(
    "2) ConcurrentQueue + Delay(50 ms)",
    Expectation.Between(12, 30),
    async () => (await Consumers.PollingAsync(new ConcurrentQueue<Job>(), window, TimeSpan.FromMilliseconds(50))).IdleWakeUps,
    note: "Aynı boş pencere, on katı uyanma. Gecikmeyi düşürmenin bedeli bu sütunda görünüyor.");

// ── 3. Channel, aynı boş pencere ─────────────────────────────────────────────
await report.MeasureAsync(
    "3) Channel, hiç iş gelmedi",
    Expectation.Exactly(0),
    async () =>
    {
        var channel = CreateChannel();
        using var cts = new CancellationTokenSource(window);

        var run = await Consumers.ChannelAsync(channel.Reader, cts.Token);
        return run.IdleWakeUps;
    },
    note: "Tüketici bir saniye boyunca hiç uyanmadı. Yoklama yok, ayarlanacak süre de yok.");

// ── 4. Channel, kapasitenin dört katı iş ─────────────────────────────────────
await report.MeasureAsync(
    "4) Channel, 20 iş / kapasite 5",
    Expectation.Exactly(0),
    async () =>
    {
        var channel = CreateChannel();
        var consumer = Consumers.ChannelAsync(channel.Reader, CancellationToken.None);

        for (var id = 0; id < JobCount; id++)
            await channel.Writer.WriteAsync(new Job(id, Stopwatch.GetTimestamp()));

        channel.Writer.Complete();
        var run = await consumer;

        if (!run.Processed.SequenceEqual(Enumerable.Range(0, JobCount)))
            throw new InvalidOperationException(
                $"Sıra ya da adet bozuk: {run.Processed.Count} iş işlendi.");

        // İş geldiği anda işleniyor; yarım saniyelik gecikme diye bir şey yok.
        if (run.WorstLatencyMs > 50)
            throw new InvalidOperationException($"En kötü gecikme {run.WorstLatencyMs:0.0} ms.");

        return run.IdleWakeUps;
    },
    note: $"Kapasite {Capacity}, iş {JobCount}: üretici yer açılana kadar bekledi, hiçbir iş kaybolmadı.");

return report.Print();

// Bounded: kapasite sınırı kararı sizden ister. Unbounded bir kaçış yolu değil,
// ertelenmiş bir OutOfMemoryException'dır.
static Channel<Job> CreateChannel()
    => Channel.CreateBounded<Job>(new BoundedChannelOptions(Capacity)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true
    });

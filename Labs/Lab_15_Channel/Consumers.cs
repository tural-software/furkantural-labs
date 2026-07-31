using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

namespace Lab_15_Channel;

/// <summary>Kuyruğa giren iş. Yazılma anı, gecikmeyi ölçebilmek için taşınıyor.</summary>
/// <param name="Id">Sıra numarası; işlenme sırasının korunduğu bununla doğrulanır.</param>
/// <param name="WrittenAt">Yazma anının zaman damgası.</param>
public readonly record struct Job(int Id, long WrittenAt);

/// <summary>Bir tüketicinin bir pencere boyunca ne yaptığı.</summary>
/// <param name="IdleWakeUps">Uyanıp ortada iş bulamadığı kere sayısı — tablodaki sütun.</param>
/// <param name="Processed">İşlenen işlerin sıra numaraları.</param>
/// <param name="WorstLatencyMs">Yazılma ile işlenme arasındaki en uzun süre.</param>
public sealed record ConsumerRun(int IdleWakeUps, IReadOnlyList<int> Processed, double WorstLatencyMs);

public static class Consumers
{
    /// <summary>
    /// Sahada en sık görülen arka plan döngüsü: kuyruğu yokla, boşsa bir süre uyu.
    /// <para>
    /// Kusur gizli değil, tam ortada duruyor: <paramref name="pollInterval"/> hem boşta
    /// harcanan uyanmayı hem de işin ne kadar bekleyeceğini belirler. Birini iyileştirmek
    /// diğerini bozar; bu takas yapay bir problemdir.
    /// </para>
    /// </summary>
    public static async Task<ConsumerRun> PollingAsync(
        ConcurrentQueue<Job> queue,
        TimeSpan window,
        TimeSpan pollInterval)
    {
        var idleWakeUps = 0;
        var processed = new List<int>();
        var worstLatency = 0d;

        using var cts = new CancellationTokenSource(window);

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                if (queue.TryDequeue(out var job))
                {
                    processed.Add(job.Id);
                    worstLatency = Math.Max(worstLatency, ElapsedMs(job.WrittenAt));
                    continue;
                }

                idleWakeUps++;
                await Task.Delay(pollInterval, cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Pencere kapandı; ölçüm bitti.
        }

        return new ConsumerRun(idleWakeUps, processed, worstLatency);
    }

    /// <summary>
    /// Aynı iş, <see cref="Channel{T}"/> ile.
    /// <para>
    /// Döngü bilerek <c>ReadAllAsync</c> yerine <c>WaitToReadAsync</c> + <c>TryRead</c>
    /// ile yazıldı: böylece "uyandım ama iş yoktu" durumu <b>sayılabilir</b> hâle geliyor.
    /// <c>ReadAllAsync</c> kullanılsaydı sayaç yapısı gereği sıfır kalırdı ve ölçüm
    /// bir şey kanıtlamazdı.
    /// </para>
    /// </summary>
    public static async Task<ConsumerRun> ChannelAsync(ChannelReader<Job> reader, CancellationToken ct)
    {
        var idleWakeUps = 0;
        var processed = new List<int>();
        var worstLatency = 0d;

        try
        {
            while (await reader.WaitToReadAsync(ct))
            {
                if (!reader.TryRead(out var job))
                {
                    idleWakeUps++;
                    continue;
                }

                processed.Add(job.Id);
                worstLatency = Math.Max(worstLatency, ElapsedMs(job.WrittenAt));
            }
        }
        catch (OperationCanceledException)
        {
            // Pencere kapandı ya da kapanış istendi.
        }

        return new ConsumerRun(idleWakeUps, processed, worstLatency);
    }

    private static double ElapsedMs(long writtenAt)
        => Stopwatch.GetElapsedTime(writtenAt).TotalMilliseconds;
}

using Microsoft.Extensions.Hosting;

namespace Lab_30_BackgroundService;

/// <summary>
/// Ölçülen tek şey: kaç iş tamamlandı. Sayaç <see cref="Interlocked"/> ile artar çünkü
/// senaryoların bir kısmında iki işçi aynı anda çalışıyor.
/// </summary>
public sealed class IsDefteri
{
    private int _tamamlanan;

    public int Tamamlanan => Volatile.Read(ref _tamamlanan);

    public void Tamamla() => Interlocked.Increment(ref _tamamlanan);
}

/// <summary>
/// <c>StartAsync</c> döndüğünde ne kadar işin bitmiş olduğunu ölçer. İki kurgu arasındaki
/// tek fark, işin ilk <c>await</c>'ten önce mi sonra mı yapıldığıdır — yani yaygın
/// tavsiyenin tam olarak değiştirmeyi önerdiği şey.
/// </summary>
/// <param name="defter">Tamamlanan iş sayacı.</param>
/// <param name="kapi">Hiç tamamlanmayan bir bekleme: yield noktası görevi görür.</param>
/// <param name="isSayisi">Kuyrukta bekleyen iş adedi.</param>
/// <param name="senkronOnce">true ise döngü <c>await</c>'ten önce, kesintisiz koşar.</param>
public sealed class BaslangicIscisi(IsDefteri defter, Task kapi, int isSayisi, bool senkronOnce)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (senkronOnce)
        {
            for (var i = 0; i < isSayisi; i++) defter.Tamamla();
            await kapi;
            return;
        }

        await kapi;
        for (var i = 0; i < isSayisi; i++) defter.Tamamla();
    }
}

/// <summary>
/// Sırası <paramref name="bozukSira"/> olan işte istisna fırlatır. Fırlatmadan hemen önce
/// <paramref name="patlamaSinyali"/>'ni kurar; ölçüm zamana değil bu sinyale bağlanır.
/// </summary>
public sealed class PatlayanIsci(
    IsDefteri defter,
    int isSayisi,
    int bozukSira,
    TaskCompletionSource patlamaSinyali) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        for (var sira = 1; sira <= isSayisi; sira++)
        {
            if (sira == bozukSira)
            {
                patlamaSinyali.TrySetResult();
                throw new InvalidOperationException($"{sira}. iş bozuk.");
            }

            defter.Tamamla();
        }
    }
}

/// <summary>
/// Aynı bozuk işi taşır ama her işi kendi <c>try/catch</c>'ine alır. İstisna
/// <c>ExecuteAsync</c>'ten dışarı çıkmadığı için host'un davranışı hiç devreye girmez.
/// </summary>
public sealed class KorumaliIsci(IsDefteri defter, int isSayisi, int bozukSira) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        for (var sira = 1; sira <= isSayisi; sira++)
        {
            try
            {
                if (sira == bozukSira) throw new InvalidOperationException($"{sira}. iş bozuk.");
                defter.Tamamla();
            }
            catch (InvalidOperationException)
            {
            }
        }
    }
}

/// <summary>
/// Kardeş işçi patladıktan sonra host'un hâlâ ayakta olup olmadığını ölçer. Patlamayı
/// bekler, sonra bir karar penceresi boyunca durur: o pencere <c>stoppingToken</c> ile
/// iptal edilirse host düşmüş demektir ve hiçbir iş tamamlanmaz. Pencere sağ salim
/// biterse host ayaktadır ve işlerin tamamı tamamlanır.
/// </summary>
public sealed class KardesIsci(IsDefteri defter, Task patlama, TimeSpan pencere, int isSayisi)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await patlama;

        try
        {
            await Task.Delay(pencere, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        for (var i = 0; i < isSayisi; i++) defter.Tamamla();
    }
}

/// <summary>
/// Kapanışta <c>stoppingToken</c>'a saygı duymanın ne değiştirdiğini ölçer. İlk partiyi
/// bitirip <paramref name="ilkPartiBitti"/>'yi kurar; ana akış o sinyali görünce host'u
/// durdurur. Kalan işler ancak token yok sayılırsa tamamlanır.
/// </summary>
public sealed class KapanisIscisi(
    IsDefteri defter,
    int isSayisi,
    int ilkParti,
    bool tokenaSaygi,
    TimeSpan pencere,
    TaskCompletionSource ilkPartiBitti) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        for (var sira = 1; sira <= ilkParti; sira++) defter.Tamamla();

        ilkPartiBitti.TrySetResult();

        // Ana akışın StopAsync'i çağırmasına alan bırakılıyor. Bu bekleme bilerek
        // iptal edilemez: ölçülen şey kapanış isteğinden SONRAKİ davranış.
        await Task.Delay(pencere, CancellationToken.None);

        for (var sira = ilkParti + 1; sira <= isSayisi; sira++)
        {
            if (tokenaSaygi && stoppingToken.IsCancellationRequested) return;
            defter.Tamamla();
        }
    }
}

namespace Lab_24_AsyncExceptions;

/// <summary>
/// İşlerin fırlattığı istisna. Ayrı bir tip olması şart: laboratuvarın ölçtüğü şey
/// "bir istisna yakalandı mı" değil, <b>çağıranın beklediği tipteki</b> istisnanın
/// yakalanıp yakalanmadığı. <c>catch (Exception)</c> ile ölçseydik bloke eden
/// senaryolardaki sarmalama görünmez olurdu.
/// </summary>
public sealed class LabException(string message) : Exception(message);

/// <summary>
/// Ölçülen iş kümesi. Her senaryoda aynı üç iş çalışır ve üçü de hata fırlatır;
/// değişen tek şey çağıranın onları nasıl beklediği.
/// </summary>
public static class Work
{
    /// <summary>Her senaryoda fırlatılan istisna sayısı. Tablonun paydası budur.</summary>
    public const int FailingJobs = 3;

    /// <summary>
    /// Hata fırlatan iş. <c>Task.Yield()</c> bilerek var: gerçek async kod ilk
    /// await'inde geri döner, istisna çağırana senkron olarak değil task üzerinden
    /// ulaşır. Senkron fırlatan bir sahte iş, ölçülen tuzakların çoğunu ortadan kaldırırdı.
    /// </summary>
    /// <param name="index">İşin sırası; istisna mesajında görünür.</param>
    public static async Task<int> FailAsync(int index)
    {
        await Task.Yield();
        throw new LabException($"{index}. iş başarısız.");
    }

    /// <summary>Üç işi de başlatır ve task'ları döndürür; hiçbiri beklenmez.</summary>
    public static Task<int>[] StartAll()
        => [.. Enumerable.Range(1, FailingJobs).Select(FailAsync)];

    /// <summary>
    /// <c>async void</c> hâli. Dönüş tipi dışında yukarıdakinin aynısı — ve ölçümdeki
    /// farkın tamamı o dönüş tipinden geliyor.
    /// </summary>
    /// <param name="index">İşin sırası; istisna mesajında görünür.</param>
    public static async void FailVoid(int index)
    {
        await Task.Yield();
        throw new LabException($"{index}. iş başarısız.");
    }

    /// <summary>
    /// Ölçüm bittikten sonra çağrılır: beklenmemiş task'ların hatalarını gözler.
    /// Ölçülen sayıya etkisi yoktur (o zaten hesaplanmıştır); amacı, gözlenmemiş
    /// istisnaların sonraki senaryolara gürültü olarak taşınmamasıdır.
    /// </summary>
    /// <param name="tasks">Beklenmemiş task'lar.</param>
    public static async Task ObserveAsync(IEnumerable<Task> tasks)
    {
        foreach (var task in tasks)
        {
            try { await task; }
            catch (LabException) { }
        }
    }
}

/// <summary>
/// <c>async void</c>'in istisnasını yakalayan en küçük eşzamanlama bağlamı.
/// <para>
/// <c>async void</c> bir metot hata fırlattığında istisna çağırana dönmez;
/// <see cref="SynchronizationContext"/> üzerine gönderilir. Konsol uygulamasında
/// bağlam yoktur, istisna doğrudan thread havuzuna düşer ve <b>süreci öldürür</b> —
/// laboratuvar o hâlde ölçüm yapamazdı. Buradaki bağlam, ASP.NET'in ya da WinForms'un
/// yaptığı işi en yalın hâliyle yaparak istisnayı tutar ve sayar.
/// </para>
/// <para>
/// Sayılan şey ölçümün kendisi değildir: tablodaki sütun çağıranın <c>catch</c>
/// bloğuna ulaşan istisnaları sayar. Bu sınıf yalnız sürecin ayakta kalmasını sağlar
/// ve istisnanın nereye gittiğini gösterir.
/// </para>
/// </summary>
/// <param name="expected">Beklenen istisna sayısı; hepsi gelince <see cref="DrainAsync"/> serbest kalır.</param>
public sealed class CapturingSynchronizationContext(int expected) : SynchronizationContext
{
    private readonly TaskCompletionSource _drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _captured;

    /// <summary>Bağlama düşen istisna sayısı.</summary>
    public int Captured => Volatile.Read(ref _captured);

    public override void Post(SendOrPostCallback d, object? state)
    {
        // Geri çağrım yerinde çalıştırılıyor: kuyruk ve pompa kurmak bu ölçüm için
        // gereksiz, sırayı da belirsizleştirirdi.
        try
        {
            d(state);
        }
        catch (LabException)
        {
            if (Interlocked.Increment(ref _captured) == expected)
                _drained.TrySetResult();
        }
    }

    /// <summary>Beklenen istisnaların hepsi bağlama düşene kadar bekler.</summary>
    public Task DrainAsync() => _drained.Task;
}

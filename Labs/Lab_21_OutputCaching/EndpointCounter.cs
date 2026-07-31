namespace Lab_21_OutputCaching;

/// <summary>
/// Ölçülen tek şey: uç kaç kez gerçekten çalıştı.
/// <para>
/// Yanıtın nereden geldiğini istemci tarafından anlamak güvenilir değildir — gövde
/// aynıdır, durum kodu aynıdır. Sayaç ucun kendi içinde artar; "sunucu yükü düştü mü"
/// sorusunun tek dürüst karşılığı budur.
/// </para>
/// </summary>
public sealed class EndpointCounter
{
    private int _hits;

    public int Hits => Volatile.Read(ref _hits);

    public void Record() => Interlocked.Increment(ref _hits);

    public void Reset() => Interlocked.Exchange(ref _hits, 0);
}

namespace Lab_29_RetryIdempotency;

/// <summary>
/// İsteğe yeni bir idempotency anahtarı basar. Laboratuvarda iki kez kullanılıyor ve
/// kodu iki seferde de aynı; değişen tek şey pipeline'da nereye kaydedildiği.
/// <para>
/// Yeniden deneme handler'ından <b>önce</b> kaydedilirse dışarıda kalır: her mantıksal
/// istek için bir kez çalışır, bütün denemeler aynı anahtarı taşır. <b>Sonra</b>
/// kaydedilirse içeride kalır: her denemede yeniden çalışır ve her deneme sunucuya
/// yeni bir sipariş gibi görünür.
/// </para>
/// </summary>
public sealed class AnahtarDamgasi : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Yeniden deneme aynı HttpRequestMessage'ı tekrar gönderir; eski değer silinmezse
        // başlık iki değer taşır ve sunucu ilkini okuyarak hatayı gizler.
        request.Headers.Remove(SiparisDefteri.AnahtarBasligi);
        request.Headers.Add(SiparisDefteri.AnahtarBasligi, Guid.NewGuid().ToString("N"));

        return base.SendAsync(request, cancellationToken);
    }
}

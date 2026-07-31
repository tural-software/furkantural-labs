using FurkanTural_Labs_Host;
using Microsoft.AspNetCore.Http;

namespace Lab_14_RateLimiting;

/// <summary>Bir yığın isteğin sonucu: kaçı geçti, reddedilenler hangi kodu aldı.</summary>
/// <param name="Passed">200 alan istek sayısı — tablodaki "Geçen" sütunu.</param>
/// <param name="RejectedCodes">Reddedilen isteklerin durum kodları.</param>
/// <param name="RetryAfterSeen">Reddedilen yanıtların herhangi birinde <c>Retry-After</c> var mıydı.</param>
public sealed record BurstResult(int Passed, IReadOnlyList<int> RejectedCodes, bool RetryAfterSeen)
{
    /// <summary>Reddedilenlerin hepsi beklenen kodu mu döndürdü.</summary>
    public bool AllRejectedWith(int statusCode)
        => RejectedCodes.Count > 0 && RejectedCodes.All(code => code == statusCode);
}

public static class Burst
{
    /// <summary>
    /// Aynı anda <paramref name="count"/> istek gönderir.
    /// <para>
    /// Eşzamanlılık şart: elle yazılmış sayacın kusuru yarış koşuludur, istekler sıra ile
    /// gönderilirse o kusur hiç görünmez ve iki uygulama aynı sayıyı verir.
    /// </para>
    /// </summary>
    public static async Task<BurstResult> SendAsync(LabApp app, string path, string client, int count)
    {
        var responses = await Task.WhenAll(Enumerable.Range(0, count).Select(async _ =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add(NaiveRateLimiter.ClientHeader, client);

            using var response = await app.Client.SendAsync(request);
            return (Status: (int)response.StatusCode, RetryAfter: response.Headers.RetryAfter is not null);
        }));

        return new BurstResult(
            Passed: responses.Count(r => r.Status == StatusCodes.Status200OK),
            RejectedCodes: [.. responses.Where(r => r.Status != StatusCodes.Status200OK).Select(r => r.Status)],
            RetryAfterSeen: responses.Any(r => r.Status != StatusCodes.Status200OK && r.RetryAfter));
    }
}

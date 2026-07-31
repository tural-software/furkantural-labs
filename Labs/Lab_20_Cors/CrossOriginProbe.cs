using FurkanTural_Labs_Host;

namespace Lab_20_Cors;

/// <summary>Tek bir çapraz kaynak isteğinin sonucu.</summary>
/// <param name="OriginAllowed">
/// Tarayıcının aradığı izin döndü mü: <c>Access-Control-Allow-Origin</c> ya isteğin
/// origin'ine ya da jokere eşit olmalı. Tablodaki sütun bu değerdir.
/// </param>
/// <param name="Status">Sunucunun döndürdüğü durum kodu.</param>
public sealed record ProbeResult(int OriginAllowed, int Status);

public static class CrossOriginProbe
{
    public const string AllowedOrigin = "https://app.lab.local";
    private const string AllowOriginHeader = "Access-Control-Allow-Origin";

    /// <summary>Tarayıcıdan gelmiş gibi bir istek: tek fark <c>Origin</c> başlığı.</summary>
    public static Task<ProbeResult> RequestAsync(LabApp app, HttpMethod method, string path)
        => SendAsync(app, method, path, preflightFor: null);

    /// <summary>
    /// Ön kontrol (preflight): tarayıcı, "basit" olmayan isteklerin önüne bunu koyar.
    /// Kimlik bilgisi taşımaz — yazının sıralama uyarısı tam olarak bundan doğar.
    /// </summary>
    public static Task<ProbeResult> PreflightAsync(LabApp app, string path, HttpMethod actualMethod)
        => SendAsync(app, HttpMethod.Options, path, preflightFor: actualMethod);

    private static async Task<ProbeResult> SendAsync(
        LabApp app,
        HttpMethod method,
        string path,
        HttpMethod? preflightFor)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Origin", AllowedOrigin);

        if (preflightFor is not null)
        {
            request.Headers.Add("Access-Control-Request-Method", preflightFor.Method);
            request.Headers.Add("Access-Control-Request-Headers", "content-type");
        }

        using var response = await app.Client.SendAsync(request);

        var allowed = response.Headers.TryGetValues(AllowOriginHeader, out var values)
                      && values.Any(value => value == AllowedOrigin || value == "*");

        return new ProbeResult(allowed ? 1 : 0, (int)response.StatusCode);
    }
}

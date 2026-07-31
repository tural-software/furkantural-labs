using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace FurkanTural_Labs_Host;

/// <summary>
/// Boru hattı davranışını ölçen laboratuvarların açılışı.
/// <para>
/// Gerçek bir Kestrel ayağa kalkar ve gerçek HTTP konuşulur. <c>TestServer</c> kullanılmadı:
/// bellekte taklit edilen bir sunucu, ölçmek istediğimiz şeylerin bir kısmını (bağlantı
/// havuzu, istemci portu, istemcinin bağlantıyı koparması) hiç göstermez.
/// </para>
/// <para>
/// Port <c>0</c> ile alınır; sabit port seçmek aynı anda çalışan iki laboratuvarı
/// birbirine düşürür.
/// </para>
/// </summary>
public static class LabsWebHost
{
    /// <summary>Minimal bir uygulamayı başlatır ve ona bağlı bir istemciyle birlikte döndürür.</summary>
    /// <param name="configure">Boru hattını ve uçları kuran işlem.</param>
    /// <param name="configureServices">Servis kaydı; gerekmiyorsa <c>null</c>.</param>
    /// <param name="ct">İptal jetonu.</param>
    public static async Task<LabApp> StartAsync(
        Action<WebApplication> configure,
        Action<WebApplicationBuilder>? configureServices = null,
        CancellationToken ct = default)
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseUrls("http://127.0.0.1:0");

        // Framework log'ları tabloyu boğar. Lab_18 kendi sağlayıcısını ekleyerek bunu ezer.
        builder.Logging.ClearProviders();

        configureServices?.Invoke(builder);

        var app = builder.Build();
        configure(app);

        await app.StartAsync(ct);

        return new LabApp(app);
    }
}

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace FurkanTural_Labs_Host;

/// <summary>Çalışan bir laboratuvar uygulaması ve ona bağlı istemci.</summary>
public sealed class LabApp : IAsyncDisposable
{
    private readonly WebApplication _application;
    private HttpClient? _client;

    internal LabApp(WebApplication application)
    {
        _application = application;

        // Bağlanılacak adres başlatmadan önce bilinmez: port 0 istendiği için gerçek
        // portu ancak sunucu bağlandıktan sonra sunucunun kendisi söyleyebilir.
        var addresses = application.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("Sunucu adres bilgisi vermedi.");

        BaseAddress = new Uri(addresses.Addresses.First());
    }

    /// <summary>Uygulamanın dinlediği adres.</summary>
    public Uri BaseAddress { get; }

    /// <summary>Uygulamaya bağlı, tekrar kullanılan istemci.</summary>
    /// <remarks>
    /// Bağlantı havuzunu ölçen laboratuvar bunu kullanmaz; kendi istemcilerini kurar.
    /// </remarks>
    public HttpClient Client => _client ??= new HttpClient { BaseAddress = BaseAddress };

    /// <summary>Servis sağlayıcı; laboratuvarın kaydettiği kayıtçılara buradan ulaşılır.</summary>
    public IServiceProvider Services => _application.Services;

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        await _application.StopAsync();
        await _application.DisposeAsync();
    }
}

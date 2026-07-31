using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Lab_14_RateLimiting;

/// <summary>
/// Sahada en sık görülen elle yazılmış sayaç: <c>IMemoryCache</c> üzerinde oku-karşılaştır-yaz.
/// <para>
/// Yazıdaki kodun karşılığı, tek farkla: yazı <c>count &gt; Limit</c> yazıyor ve bu limitin
/// bir fazlasına izin verir. Burada <c>&gt;=</c> kullanıldı ki iki uygulama <b>aynı</b> izin
/// sayısına sahip olsun; ölçülen fark yalnızca eşzamanlılıktan gelsin.
/// </para>
/// <para>
/// Kusur okumakla görülmez: <c>Get</c> ile <c>Set</c> arasında kilit yoktur. Eşzamanlı
/// isteklerin hepsi aynı eski sayıyı okur, hepsi kendini limitin altında sanır.
/// </para>
/// </summary>
public static class NaiveRateLimiter
{
    public const string ClientHeader = "X-Lab-Client";
    public const int PermitLimit = 5;

    public static readonly TimeSpan Window = TimeSpan.FromMilliseconds(500);

    public static void UseNaiveRateLimiter(this WebApplication app)
        => app.Use(async (context, next) =>
        {
            var cache = context.RequestServices.GetRequiredService<IMemoryCache>();
            var key = $"rl_{context.Request.Headers[ClientHeader]}";

            var count = cache.Get<int>(key);

            if (count >= PermitLimit)
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                return;
            }

            cache.Set(key, count + 1, Window);
            await next(context);
        });
}

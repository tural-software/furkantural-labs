using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FurkanTural_Labs_Host;

/// <summary>
/// Ölçüm için yeterli en küçük kimlik doğrulama şeması: <c>X-Lab-User</c> başlığı varsa
/// istek kimliklidir, <c>X-Lab-Roles</c> varsa rolleri de vardır.
/// <para>
/// JWT kurmak, yetkilendirme sorularının hiçbirine bir şey katmaz; yalnız gürültü ekler.
/// Kimliğin nasıl kurulduğu değil, kurulduktan sonra ne olduğu ölçülüyor.
/// </para>
/// </summary>
public sealed class LabAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "LabHeader";
    public const string UserHeader = "X-Lab-User";
    public const string RolesHeader = "X-Lab-Roles";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserHeader, out var user) || string.IsNullOrWhiteSpace(user))
            return Task.FromResult(AuthenticateResult.NoResult());

        // Ad ve kimlik aynı değer: kaynağa bağlı kuralların karşılaştırdığı şey NameIdentifier'dır.
        List<Claim> claims =
        [
            new(ClaimTypes.Name, user!),
            new(ClaimTypes.NameIdentifier, user!)
        ];

        if (Request.Headers.TryGetValue(RolesHeader, out var roles))
        {
            claims.AddRange(roles.ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(role => new Claim(ClaimTypes.Role, role)));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public static class LabAuthenticationExtensions
{
    /// <summary>Başlık tabanlı şemayı varsayılan şema olarak kaydeder.</summary>
    public static IServiceCollection AddLabAuthentication(this IServiceCollection services)
    {
        services
            .AddAuthentication(LabAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, LabAuthenticationHandler>(
                LabAuthenticationHandler.SchemeName, _ => { });

        return services;
    }
}

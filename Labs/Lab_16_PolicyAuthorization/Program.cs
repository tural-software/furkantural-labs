using System.Security.Claims;
using FurkanTural_Labs_Application.Diagnostics;
using FurkanTural_Labs_Host;
using Lab_16_PolicyAuthorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

// ── Rol adına bakan uygulama: [Authorize(Roles = "Admin,Editor")] ────────────
await using var roleOnly = await LabsWebHost.StartAsync(
    app => app.MapPut("/yazi/{id:int}", (int id) => Results.Ok(id))
              .RequireAuthorization(policy => policy.RequireRole(Posts.AdminRole, Posts.EditorRole)),
    builder =>
    {
        builder.Services.AddLabAuthentication();
        builder.Services.AddAuthorization();
    });

// ── Policy tabanlı uygulama ──────────────────────────────────────────────────
await using var policyBased = await LabsWebHost.StartAsync(
    app =>
    {
        // Kaynağa bağlı karar attribute ile verilemez: pipeline'da o nesne henüz yüklü değil.
        // Kararı uç, kaydı yükledikten sonra IAuthorizationService üzerinden tetikliyor.
        app.MapPut("/yazi/{id:int}", async (
            int id,
            ClaimsPrincipal user,
            IAuthorizationService authorization) =>
        {
            var post = Posts.Sample with { Id = id };
            var result = await authorization.AuthorizeAsync(user, post, Policies.PostOwner);

            return result.Succeeded ? Results.Ok(id) : Results.Forbid();
        }).RequireAuthorization();

        app.MapGet("/cakisan", DecideAsync(Policies.Conflicting)).RequireAuthorization();
        app.MapGet("/kararsiz", DecideAsync(Policies.Undecided)).RequireAuthorization();
    },
    builder =>
    {
        builder.Services.AddLabAuthentication();

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(Policies.PostOwner, p => p.Requirements.Add(new PostOwnerRequirement()));
            options.AddPolicy(Policies.Conflicting, p => p.Requirements.Add(new ConflictingRequirement()));
            options.AddPolicy(Policies.Undecided, p => p.Requirements.Add(new UndecidedRequirement()));
        });

        // Scoped kayıt: handler içinde DbContext gibi scoped bir bağımlılık kullanılabilsin.
        // Singleton kaydedip DbContext enjekte etmek captive dependency üretir.
        builder.Services.AddScoped<IAuthorizationHandler, PostOwnerHandler>();
        builder.Services.AddScoped<IAuthorizationHandler, GrantingHandler>();
        builder.Services.AddScoped<IAuthorizationHandler, VetoingHandler>();
        builder.Services.AddScoped<IAuthorizationHandler, SilentHandler>();
    });

var report = new LabReport(
    title: "Lab_16 — Rol adı mı, kural mı: aynı düzenleme isteği altı kez",
    claim: $"Her senaryoda {Posts.Owner} adlı kullanıcının {Posts.Sample.Id} numaralı yazısı " +
           "düzenlenmeye çalışılıyor. Ölçülen şey, isteği gönderenin aldığı durum kodu.",
    metric: "Durum");

// ── 1. Rol yeterli sanılıyor ─────────────────────────────────────────────────
// Yazının merkez iddiası: rol adına bakmak yetki kontrolü değildir.
await report.MeasureAsync(
    "1) Rol attribute, başkasının yazısı",
    Expectation.Exactly(StatusCodes.Status200OK),
    () => EditAsync(roleOnly, Posts.Other, Posts.EditorRole),
    note: $"{Posts.Other} Editor rolünde, ama yazının sahibi değil. Rol doğru olduğu için geçti.");

// ── 2. Aynı istek, kaynağa bakan policy ──────────────────────────────────────
await report.MeasureAsync(
    "2) Policy, başkasının yazısı",
    Expectation.Exactly(StatusCodes.Status403Forbidden),
    () => EditAsync(policyBased, Posts.Other, Posts.EditorRole),
    note: "Aynı kullanıcı, aynı rol, aynı istek. Fark: kural artık kaynağa bakıyor.");

// ── 3. Yazının sahibi ────────────────────────────────────────────────────────
await report.MeasureAsync(
    "3) Policy, yazının sahibi",
    Expectation.Exactly(StatusCodes.Status200OK),
    () => EditAsync(policyBased, Posts.Owner, Posts.EditorRole),
    note: "Policy sahibi tanıyor: rol aynı, sonuç farklı.");

// ── 4. Admin, başkasının yazısı ──────────────────────────────────────────────
await report.MeasureAsync(
    "4) Policy, admin",
    Expectation.Exactly(StatusCodes.Status200OK),
    () => EditAsync(policyBased, Posts.Administrator, Posts.AdminRole),
    note: "İstisna da kuralın içinde; controller'da ayrı bir if olarak durmuyor.");

// ── 5. İki handler, biri Succeed biri Fail ───────────────────────────────────
await report.MeasureAsync(
    "5) Succeed + Fail aynı requirement",
    Expectation.Exactly(StatusCodes.Status403Forbidden),
    () => StatusAsync(policyBased, "/cakisan", Posts.Administrator, Posts.AdminRole),
    note: "Aynı requirement'ın handler'ları VEYA gibi davranır, ama Fail() hepsini ezer.");

// ── 6. Handler karar vermiyor ────────────────────────────────────────────────
await report.MeasureAsync(
    "6) Handler hiçbir şey çağırmıyor",
    Expectation.Exactly(StatusCodes.Status403Forbidden),
    () => StatusAsync(policyBased, "/kararsiz", Posts.Administrator, Posts.AdminRole),
    note: "Sessizce dönmek izin vermek değil; kimse karşılamazsa requirement reddedilir.");

return report.Print();

// Ortak uç gövdesi: kaynağı olmayan policy'ler için karar doğrudan servise sorulur.
static Func<ClaimsPrincipal, IAuthorizationService, Task<IResult>> DecideAsync(string policy)
    => async (user, authorization) =>
    {
        var result = await authorization.AuthorizeAsync(user, policy);
        return result.Succeeded ? Results.Ok(policy) : Results.Forbid();
    };

static Task<int> EditAsync(LabApp app, string user, string role)
    => SendAsync(app, HttpMethod.Put, $"/yazi/{Posts.Sample.Id}", user, role);

static Task<int> StatusAsync(LabApp app, string path, string user, string role)
    => SendAsync(app, HttpMethod.Get, path, user, role);

static async Task<int> SendAsync(LabApp app, HttpMethod method, string path, string user, string role)
{
    using var request = new HttpRequestMessage(method, path);
    request.Headers.Add(LabAuthenticationHandler.UserHeader, user);
    request.Headers.Add(LabAuthenticationHandler.RolesHeader, role);

    using var response = await app.Client.SendAsync(request);
    return (int)response.StatusCode;
}

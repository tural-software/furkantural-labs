using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Lab_16_PolicyAuthorization;

/// <summary>Yetki kararının bakacağı kaynak. Kural "bu kişi kim" değil, "bu kayda ne yapabilir".</summary>
public sealed record Post(int Id, string AuthorId);

public static class Posts
{
    public const string Owner = "furkan";
    public const string Other = "ayse";
    public const string Administrator = "admin";

    public const string EditorRole = "Editor";
    public const string AdminRole = "Admin";

    /// <summary>Sahibi <see cref="Owner"/> olan yazı; ölçümlerde hep bu düzenlenmeye çalışılıyor.</summary>
    public static readonly Post Sample = new(7, Owner);
}

public static class Policies
{
    public const string PostOwner = "PostOwner";
    public const string Conflicting = "Cakisan";
    public const string Undecided = "Kararsiz";
}

public sealed class PostOwnerRequirement : IAuthorizationRequirement;

/// <summary>
/// Kural artık kopyalanabilir bir <c>if</c> değil, adı olan bir nesne. Doğrudan unit test
/// edilebilir ve MVC ile Web API tarafında tekrar yazılmadan kullanılabilir.
/// </summary>
public sealed class PostOwnerHandler : AuthorizationHandler<PostOwnerRequirement, Post>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PostOwnerRequirement requirement,
        Post resource)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (context.User.IsInRole(Posts.AdminRole) || resource.AuthorId == userId)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}

public sealed class ConflictingRequirement : IAuthorizationRequirement;

/// <summary>Aynı requirement için ikinci bir handler; tek başına yeterdi.</summary>
public sealed class GrantingHandler : AuthorizationHandler<ConflictingRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ConflictingRequirement requirement)
    {
        context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

/// <summary><c>Fail()</c> kesin yasaktır: başka handler <c>Succeed</c> dese bile sonuç değişmez.</summary>
public sealed class VetoingHandler : AuthorizationHandler<ConflictingRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ConflictingRequirement requirement)
    {
        context.Fail();
        return Task.CompletedTask;
    }
}

public sealed class UndecidedRequirement : IAuthorizationRequirement;

/// <summary>
/// Hiçbir şey çağırmayan handler. Sık yapılan okuma hatası: "reddetmedim, demek ki izin verdim".
/// Sessizce dönmek yalnızca "karar veremedim" demektir; kimse karşılamazsa sonuç rettir.
/// </summary>
public sealed class SilentHandler : AuthorizationHandler<UndecidedRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        UndecidedRequirement requirement)
        => Task.CompletedTask;
}

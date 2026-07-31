using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Lab_06_GlobalExceptionHandling;

/// <summary>İş kuralı ihlali: kayıt yok.</summary>
/// <param name="message">Hata metni.</param>
public sealed class NotFoundException(string message) : Exception(message);

/// <summary>İş kuralı ihlali: girdi geçersiz.</summary>
/// <param name="message">Hata metni.</param>
public sealed class ValidationException(string message) : Exception(message);

/// <summary>
/// .NET 8 ile gelen <see cref="IExceptionHandler"/> karşılığı: tek yerde eşleme, her uçta
/// aynı gövde. Controller'lardaki try-catch yığınının yerini alan şey budur.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    /// <summary>İstemcinin bağlantıyı kapattığını anlatan standart dışı ama yaygın kod.</summary>
    public const int ClientClosedRequest = 499;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        // İptal bir hata değildir: 500 sayılırsa gösterge panosu, istemci sekmeyi
        // kapattığı için alarma geçer.
        if (exception is OperationCanceledException)
        {
            context.Response.StatusCode = ClientClosedRequest;
            return true;
        }

        var status = exception switch
        {
            NotFoundException => StatusCodes.Status404NotFound,
            ValidationException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };

        context.Response.StatusCode = status;

        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            // Beklenmedik hatanın metni istemciye taşınmaz: iç detay sızdırmanın en kolay yolu.
            Title = status == StatusCodes.Status500InternalServerError
                ? "Beklenmedik bir hata oluştu"
                : exception.Message,
            Instance = context.Request.Path
        }, cancellationToken);

        return true;
    }
}

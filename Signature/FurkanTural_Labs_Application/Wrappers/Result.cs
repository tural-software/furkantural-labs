using System.Text.Json.Serialization;

namespace FurkanTural_Labs_Application.Wrappers;

/// <summary>
/// Monorepo'daki <c>FurkanTural_Application.Wrappers.Result</c> sözleşmesinin kopyası.
/// Servis katmanı gerektiren laboratuvarlar (Lab_10 Specification, Lab_06 exception handling)
/// gerçek projedeki dönüş tipini kullansın diye buraya taşındı.
/// <para>
/// <see cref="InternalMessage"/> geliştirici teşhisidir ve <see cref="JsonIgnoreAttribute"/> ile
/// süreç içinde kalır; istemciye çıkan tek hata metni <see cref="Errors"/>'tır. Bu, Lab_06'nın
/// anlattığı şeyin ta kendisidir: o lab, mesajında bağlantı dizesi geçen bir istisna atıp
/// istemciye hiçbir iç ayrıntının sızmadığını gösterir. Zarf kendi başına sızdırırsa ders çürür.
/// </para>
/// <para>
/// Tek bilinen ayrışma <see cref="ErrorCode"/>'dur: monorepo bu alanı kaldırdı, buradaki kopya
/// henüz taşıyor. Kaldırılması gerekiyorsa <c>Fail</c> aşırı yüklemelerinin imzaları da değişir.
/// </para>
/// </summary>
public class Result
{
    public bool Success { get; protected init; }
    public bool IsFailure => !Success;
    public string Message { get; protected init; } = string.Empty;

    [JsonIgnore]
    public string InternalMessage { get; protected init; } = string.Empty;

    public string ErrorCode { get; protected init; } = string.Empty;
    public int StatusCode { get; protected init; } = 200;
    public List<string> Errors { get; protected init; } = [];

    protected Result() { }

    public static Result Ok(string message = "") =>
        new() { Success = true, Message = message, StatusCode = 200 };

    public static Result Fail(string error, string internalMessage = "", string errorCode = "", int statusCode = 400) =>
        new() { Success = false, Errors = [error], InternalMessage = internalMessage, ErrorCode = errorCode, StatusCode = statusCode };

    public static Result Fail(List<string> errors, string internalMessage = "", string errorCode = "", int statusCode = 400) =>
        new() { Success = false, Errors = errors, InternalMessage = internalMessage, ErrorCode = errorCode, StatusCode = statusCode };
}

public class Result<T> : Result
{
    public T? Data { get; protected init; }

    protected Result() { }

    public static Result<T> Ok(T data, string message = "") =>
        new() { Success = true, Data = data, Message = message, StatusCode = 200 };

    public new static Result<T> Fail(string error, string internalMessage = "", string errorCode = "", int statusCode = 400) =>
        new() { Success = false, Errors = [error], InternalMessage = internalMessage, ErrorCode = errorCode, StatusCode = statusCode };

    public new static Result<T> Fail(List<string> errors, string internalMessage = "", string errorCode = "", int statusCode = 400) =>
        new() { Success = false, Errors = errors, InternalMessage = internalMessage, ErrorCode = errorCode, StatusCode = statusCode };
}

public class PagedResult<T> : Result<IEnumerable<T>>
{
    public int TotalCount { get; private init; }
    public int PageNumber { get; private init; }
    public int PageSize { get; private init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    private PagedResult() { }

    public static PagedResult<T> Ok(IEnumerable<T> data, int totalCount, int pageNumber, int pageSize, string message = "") =>
        new() { Success = true, Data = data, Message = message, StatusCode = 200, TotalCount = totalCount, PageNumber = pageNumber, PageSize = pageSize };

    public new static PagedResult<T> Fail(string error, string internalMessage = "", string errorCode = "", int statusCode = 400) =>
        new() { Success = false, Errors = [error], InternalMessage = internalMessage, ErrorCode = errorCode, StatusCode = statusCode };
}

namespace DGates.Identity.Jwt2Fa.Services;

/// <summary>The HTTP outcome a <see cref="Jwt2FaResult{T}"/> should map to.</summary>
public enum Jwt2FaResultKind
{
    /// <summary>200 OK, with <see cref="Jwt2FaResult{T}.Value"/> as the body.</summary>
    Ok,

    /// <summary>400 Bad Request, with <see cref="Jwt2FaResult{T}.Error"/> as the body.</summary>
    BadRequest,

    /// <summary>404 Not Found, with <see cref="Jwt2FaResult{T}.Error"/> as the body.</summary>
    NotFound,

    /// <summary>401 Unauthorized, with <see cref="Jwt2FaResult{T}.Value"/> as the body.</summary>
    Unauthorized
}

/// <summary>
/// Wraps a service method's result with the HTTP outcome it should map to, so the
/// service layer can signal "this should be a 404" without taking a dependency on
/// ASP.NET Core's <c>IResult</c>/<c>Results</c> types — keeps services testable without
/// a hosting context. Endpoint mapping converts this to an actual <c>IResult</c> via
/// <see cref="Jwt2FaResultExtensions.ToIResult{T}"/>.
/// </summary>
public readonly struct Jwt2FaResult<T>
{
    /// <summary>The outcome this result maps to.</summary>
    public Jwt2FaResultKind Kind { get; }

    /// <summary>The success (or unauthorized-but-still-has-a-body) payload.</summary>
    public T? Value { get; }

    /// <summary>The error payload for <see cref="Jwt2FaResultKind.BadRequest"/>/<see cref="Jwt2FaResultKind.NotFound"/> — a message string or a validation error collection.</summary>
    public object? Error { get; }

    private Jwt2FaResult(Jwt2FaResultKind kind, T? value, object? error)
    {
        Kind = kind;
        Value = value;
        Error = error;
    }

    /// <summary>A successful result.</summary>
    public static Jwt2FaResult<T> Ok(T value) => new(Jwt2FaResultKind.Ok, value, null);

    /// <summary>A 400 Bad Request, e.g. a failed self-or-admin authorization check or Identity validation errors.</summary>
    public static Jwt2FaResult<T> BadRequest(object error) => new(Jwt2FaResultKind.BadRequest, default, error);

    /// <summary>A 404 Not Found.</summary>
    public static Jwt2FaResult<T> NotFound(object error) => new(Jwt2FaResultKind.NotFound, default, error);

    /// <summary>A 401 Unauthorized that still carries a body (e.g. an error-message DTO).</summary>
    public static Jwt2FaResult<T> Unauthorized(T value) => new(Jwt2FaResultKind.Unauthorized, value, null);
}

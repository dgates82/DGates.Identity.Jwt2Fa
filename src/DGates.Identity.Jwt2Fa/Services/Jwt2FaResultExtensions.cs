using Microsoft.AspNetCore.Http;

namespace DGates.Identity.Jwt2Fa.Services;

/// <summary>Converts a <see cref="Jwt2FaResult{T}"/> to an ASP.NET Core <see cref="IResult"/>.</summary>
public static class Jwt2FaResultExtensions
{
    /// <summary>Maps <paramref name="result"/>'s <see cref="Jwt2FaResult{T}.Kind"/> to the matching <see cref="IResult"/>.</summary>
    public static IResult ToIResult<T>(this Jwt2FaResult<T> result) => result.Kind switch
    {
        Jwt2FaResultKind.Ok => Results.Ok(result.Value),
        Jwt2FaResultKind.BadRequest => Results.BadRequest(result.Error),
        Jwt2FaResultKind.NotFound => Results.NotFound(result.Error),
        Jwt2FaResultKind.Unauthorized => Results.Json(result.Value, statusCode: StatusCodes.Status401Unauthorized),
        _ => throw new InvalidOperationException($"Unhandled {nameof(Jwt2FaResultKind)}: {result.Kind}")
    };
}

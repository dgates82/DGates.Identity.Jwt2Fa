using DGates.Identity.Jwt2Fa.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace DGates.Identity.Jwt2Fa.Tests.Services;

public class Jwt2FaResultExtensionsTests
{
    [Fact]
    public async Task ToIResult_Ok_ProducesStatus200()
    {
        var httpContext = await ExecuteAsync(Jwt2FaResult<string>.Ok("value"));

        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task ToIResult_BadRequest_ProducesStatus400()
    {
        var httpContext = await ExecuteAsync(Jwt2FaResult<string>.BadRequest("bad"));

        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task ToIResult_NotFound_ProducesStatus404()
    {
        var httpContext = await ExecuteAsync(Jwt2FaResult<string>.NotFound("missing"));

        Assert.Equal(StatusCodes.Status404NotFound, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task ToIResult_Unauthorized_ProducesStatus401()
    {
        var httpContext = await ExecuteAsync(Jwt2FaResult<string>.Unauthorized("value"));

        Assert.Equal(StatusCodes.Status401Unauthorized, httpContext.Response.StatusCode);
    }

    private static async Task<DefaultHttpContext> ExecuteAsync<T>(Jwt2FaResult<T> result)
    {
        var services = new ServiceCollection().AddOptions().AddLogging().BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services,
            Response = { Body = new MemoryStream() }
        };
        await result.ToIResult().ExecuteAsync(httpContext);
        return httpContext;
    }
}

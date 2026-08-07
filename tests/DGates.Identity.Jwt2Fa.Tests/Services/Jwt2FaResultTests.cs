using DGates.Identity.Jwt2Fa.Services;

namespace DGates.Identity.Jwt2Fa.Tests.Services;

public class Jwt2FaResultTests
{
    [Fact]
    public void Ok_SetsKindAndValue()
    {
        var result = Jwt2FaResult<string>.Ok("value");

        Assert.Equal(Jwt2FaResultKind.Ok, result.Kind);
        Assert.Equal("value", result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void BadRequest_SetsKindAndError()
    {
        var result = Jwt2FaResult<string>.BadRequest("bad");

        Assert.Equal(Jwt2FaResultKind.BadRequest, result.Kind);
        Assert.Equal("bad", result.Error);
        Assert.Null(result.Value);
    }

    [Fact]
    public void NotFound_SetsKindAndError()
    {
        var result = Jwt2FaResult<string>.NotFound("missing");

        Assert.Equal(Jwt2FaResultKind.NotFound, result.Kind);
        Assert.Equal("missing", result.Error);
        Assert.Null(result.Value);
    }

    [Fact]
    public void Unauthorized_SetsKindAndValue()
    {
        var result = Jwt2FaResult<string>.Unauthorized("value");

        Assert.Equal(Jwt2FaResultKind.Unauthorized, result.Kind);
        Assert.Equal("value", result.Value);
        Assert.Null(result.Error);
    }
}

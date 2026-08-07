using DGates.Identity.Jwt2Fa.Dtos;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DGates.Identity.Jwt2Fa.Tests.Integration;

/// <summary>
/// Covers the exception-handling and request-validation endpoint filters applied to
/// every module's route group — a baseline safety net the source app had per-action
/// (catch-all + generic 500 message, automatic 400 on invalid input) with no equivalent
/// anywhere in this package until now.
/// </summary>
public class EndpointHardeningFlowTests : IntegrationTestBase
{
    [Fact]
    public async Task Register_WithMissingRequiredField_ReturnsBadRequest()
    {
        // A required C# member missing from the JSON body entirely fails during model
        // binding itself, before this reaches the validation filter - System.Text.Json
        // (and the minimal API binding layer) already reject it with a 400, just not
        // in the filter's ValidationProblem shape. Malformed-but-present values (empty
        // string, bad email format) are what the filter itself covers - see below.
        var response = await Client.PostAsJsonAsync("/auth/register", new { Email = "user@example.com" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithEmptyEmail_ReturnsValidationProblem()
    {
        var response = await Client.PostAsJsonAsync("/auth/register", new RegisterRequestDto { Email = "", Password = "P@ssw0rd" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").TryGetProperty("Email", out _));
    }

    [Fact]
    public async Task Register_WithMalformedEmail_ReturnsValidationProblem()
    {
        var response = await Client.PostAsJsonAsync("/auth/register", new RegisterRequestDto { Email = "not-an-email", Password = "P@ssw0rd" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").TryGetProperty("Email", out _));
    }

    [Fact]
    public async Task Register_WithWellFormedRequest_IsNotRejectedByValidation()
    {
        var response = await Client.PostAsJsonAsync("/auth/register", new RegisterRequestDto
        {
            Email = "valid-request@example.com",
            Password = "P@ssw0rd"
        });

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Register_WhenAnUnhandledExceptionOccurs_ReturnsGenericMessageNotExceptionDetails()
    {
        EmailSender.ThrowOnNextSend = true;

        var response = await Client.PostAsJsonAsync("/auth/register", new RegisterRequestDto
        {
            Email = "exception-test@example.com",
            Password = "P@ssw0rd"
        });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("An unexpected error occurred.", body);
        Assert.DoesNotContain("Simulated email delivery failure", body);
        Assert.DoesNotContain("InvalidOperationException", body);
    }
}

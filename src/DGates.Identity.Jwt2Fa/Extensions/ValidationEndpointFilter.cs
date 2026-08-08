using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace DGates.Identity.Jwt2Fa.Extensions;

/// <summary>
/// Applied once per module at <c>MapGroup</c> level, alongside
/// <see cref="ExceptionHandlingEndpointFilter"/>. Runs plain
/// <see cref="System.ComponentModel.DataAnnotations"/> validation against every bound
/// argument that carries validation attributes — request DTOs on POST/PUT endpoints;
/// route/query parameters (plain strings/ints) have nothing to validate and are skipped
/// harmlessly. Mirrors <c>[ApiController]</c>'s automatic 400-on-invalid-ModelState,
/// which minimal APIs don't provide on their own.
/// </summary>
internal sealed class ValidationEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        foreach (var argument in context.Arguments)
        {
            if (argument is null)
            {
                continue;
            }

            var validationResults = new List<ValidationResult>();
            var isValid = Validator.TryValidateObject(
                argument, new ValidationContext(argument), validationResults, validateAllProperties: true);

            if (!isValid)
            {
                var errors = validationResults
                    .SelectMany(result => result.MemberNames.DefaultIfEmpty(string.Empty)
                        .Select(member => (Member: member, result.ErrorMessage)))
                    .GroupBy(x => x.Member)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(x => x.ErrorMessage ?? "Invalid value.").ToArray());

                return Results.ValidationProblem(errors);
            }
        }

        return await next(context);
    }
}

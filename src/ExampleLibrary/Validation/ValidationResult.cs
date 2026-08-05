using System.Collections.Generic;

namespace ExampleLibrary.Validation;

/// <summary>
/// Represents the outcome of validating a <see cref="Note"/>.
/// </summary>
public class ValidationResult
{
    /// <summary>Whether the validated note passed all checks.</summary>
    public bool IsValid { get; }

    /// <summary>The validation error messages, if any.</summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationResult"/> class.
    /// </summary>
    /// <param name="isValid">Whether the validated note passed all checks.</param>
    /// <param name="errors">The validation error messages, if any.</param>
    public ValidationResult(bool isValid, IReadOnlyList<string> errors)
    {
        IsValid = isValid;
        Errors = errors;
    }
}
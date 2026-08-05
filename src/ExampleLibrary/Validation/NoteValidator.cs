using System.Collections.Generic;
using ExampleLibrary.Models;

namespace ExampleLibrary.Validation;

/// <summary>
/// Validates <see cref="Note"/> instances against required fields and length constraints.
/// </summary>
public static class NoteValidator
{
    internal const int MaxTitleLength = 200;
    internal const int MaxBodyLength = 10000;

    /// <summary>
    /// Validates the given note.
    /// </summary>
    /// <param name="note">The note to validate.</param>
    /// <returns>A <see cref="ValidationResult"/> describing whether the note is valid and any errors found.</returns>
    public static ValidationResult Validate(Note note)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(note.Id))
        {
            errors.Add("Id is required.");
        }
        
        if (string.IsNullOrWhiteSpace(note.Title))
        {
            errors.Add("Title is required.");
        }
        else if (note.Title.Length > MaxTitleLength)
        {
            errors.Add($"Title must be {MaxTitleLength} characters or fewer.");
        }
        
        if (string.IsNullOrWhiteSpace(note.Body))
        {
            errors.Add("Body is required.");
        }
        else if (note.Body.Length > MaxBodyLength)
        {
            errors.Add($"Body must be {MaxBodyLength} characters or fewer.");
        }
        
        return new ValidationResult(errors.Count == 0, errors);
    }
    
}
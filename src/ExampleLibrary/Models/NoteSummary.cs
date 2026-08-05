using System;

namespace ExampleLibrary.Models;

/// <summary>
/// Represents a lightweight view of a <see cref="Note"/>, omitting the body content.
/// </summary>
public class NoteSummary
{
    /// <summary>The unique identifier of the note.</summary>
    public string Id { get; set; }

    /// <summary>The note's title.</summary>
    public string Title { get; set; }

    /// <summary>The date and time the note was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NoteSummary"/> class.
    /// </summary>
    /// <param name="id">The unique identifier of the note.</param>
    /// <param name="title">The note's title.</param>
    /// <param name="createdAt">The date and time the note was created.</param>
    public NoteSummary(string id, string title, DateTimeOffset createdAt)
    {
        Id = id;
        Title = title;
        CreatedAt = createdAt;
    }
}

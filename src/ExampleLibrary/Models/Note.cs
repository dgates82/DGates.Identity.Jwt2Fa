using System;

namespace ExampleLibrary.Models;

/// <summary>
/// Represents a single note, including its full body content.
/// </summary>
public class Note
{
    /// <summary>The unique identifier of the note.</summary>
    public string Id { get; set; }

    /// <summary>The note's title.</summary>
    public string Title { get; set; }

    /// <summary>The note's full text content.</summary>
    public string Body { get; set; }

    /// <summary>The date and time the note was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Note"/> class.
    /// </summary>
    /// <param name="id">The unique identifier of the note.</param>
    /// <param name="title">The note's title.</param>
    /// <param name="body">The note's full text content.</param>
    /// <param name="createdAt">The date and time the note was created.</param>
    public Note(string id, string title, string body, DateTimeOffset createdAt)
    {
        Id = id;
        Title = title;
        Body = body;
        CreatedAt = createdAt;
    }
}

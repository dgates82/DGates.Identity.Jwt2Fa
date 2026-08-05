using ExampleLibrary.Models;

namespace ExampleLibrary.Storage;

/// <summary>
/// Provides persistence operations for <see cref="Note"/> instances.
/// </summary>
public interface INoteStore
{

    /// <summary>
    /// Creates or overwrites the given note.
    /// </summary>
    /// <param name="note">The note to store.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task PutNoteAsync(Note note, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a note by its identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the note.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The matching note, or <c>null</c> if no note with the given id exists.</returns>
    Task<Note?> GetNoteAsync(string id,  CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists lightweight summaries of all stored notes.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<IEnumerable<NoteSummary>> ListNotesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a note by its identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the note.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task DeleteNoteAsync(string id, CancellationToken cancellationToken = default);

}
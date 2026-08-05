using ExampleLibrary.Models;
using ExampleLibrary.Storage;

namespace ExampleLibrary.Tests.Storage;

[Trait("Category", "Integration")]
public class S3NoteStoreTests : IClassFixture<LocalStackFixture>
{
    private readonly S3NoteStore _store;

    public S3NoteStoreTests(LocalStackFixture fixture)
    {
        _store = new S3NoteStore(fixture.S3Client, LocalStackFixture.BucketName);
    }

    [Fact]
    public async Task GetNoteAsync_WithSeededNote_ReturnsExpectedNote()
    {
        var note = await _store.GetNoteAsync("seed-note-1");
        
        Assert.NotNull(note);
        Assert.Equal("seed-note-1", note!.Id);
        Assert.Equal("Welcome to ExampleLibrary", note.Title);
    }
    
    [Fact]
    public async Task GetNoteAsync_WithNonExistentId_ReturnsNull()
    {
        var note = await _store.GetNoteAsync("does-not-exist");

        Assert.Null(note);
    }

    [Fact]
    public async Task PutNoteAsync_ThenGetNoteAsync_ReturnsSameNote()
    {
        var note = new Note(
            id: $"test-{Guid.NewGuid()}",
            title: "Integration test note",
            body: "Created and read back by S3NoteStoreTests.",
            createdAt: DateTimeOffset.UtcNow);

        try
        {
            await _store.PutNoteAsync(note);

            var retrieved = await _store.GetNoteAsync(note.Id);

            Assert.NotNull(retrieved);
            Assert.Equal(note.Id, retrieved!.Id);
            Assert.Equal(note.Title, retrieved.Title);
            Assert.Equal(note.Body, retrieved.Body);
        }
        finally
        {
            await _store.DeleteNoteAsync(note.Id);
        }
    }

    [Fact]
    public async Task DeleteNoteAsync_RemovesNote()
    {
        var note = new Note(
            id: $"test-{Guid.NewGuid()}",
            title: "To be deleted",
            body: "This note should not exist after deletion.",
            createdAt: DateTimeOffset.UtcNow);

        await _store.PutNoteAsync(note);
        await _store.DeleteNoteAsync(note.Id);

        var retrieved = await _store.GetNoteAsync(note.Id);

        Assert.Null(retrieved);
    }

    [Fact]
    public async Task ListNotesAsync_IncludesSeededNote()
    {
        var summaries = await _store.ListNotesAsync();

        Assert.Contains(summaries, s => s.Id == "seed-note-1" && s.Title == "Welcome to ExampleLibrary");
    }

    [Fact]
    public async Task ListNotesAsync_AfterPut_IncludesNewNote()
    {
        var note = new Note(
            id: $"test-{Guid.NewGuid()}",
            title: "Should appear in list",
            body: "Body content, not returned by ListNotesAsync.",
            createdAt: DateTimeOffset.UtcNow);

        try
        {
            await _store.PutNoteAsync(note);

            var summaries = await _store.ListNotesAsync();

            Assert.Contains(summaries, s => s.Id == note.Id && s.Title == note.Title);
        }
        finally
        {
            await _store.DeleteNoteAsync(note.Id);
        }
    }

    [Fact]
    public async Task PutNoteAsync_WithNullOrEmptyId_ThrowsArgumentException()
    {
        var note = new Note(id: "", title: "Invalid", body: "Missing id.", createdAt: DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<ArgumentException>(() => _store.PutNoteAsync(note));
    }
}
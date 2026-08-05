using ExampleLibrary.Models;
using ExampleLibrary.Validation;

namespace ExampleLibrary.Tests.Validation;

public class NoteValidatorTests
{
    private static Note CreateNote(string? id, string? title, string? body) =>
        new Note(id: id!, title: title!, body: body!, createdAt: DateTimeOffset.UtcNow);

    [Fact]
    public void Validate_WithValidNote_ReturnsValid()
    {
        var note = CreateNote("test-id", "A valid title", "A valid body");

        var result = NoteValidator.Validate(note);
        
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithMissingId_ReturnsInvalid(string? id)
    {
        var note = CreateNote(id, "A valid title", "A valid body.");

        var result = NoteValidator.Validate(note);

        Assert.False(result.IsValid);
        Assert.Contains("Id is required.", result.Errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithMissingTitle_ReturnsInvalid(string? title)
    {
        var note = CreateNote("test-id", title, "A valid body.");

        var result = NoteValidator.Validate(note);

        Assert.False(result.IsValid);
        Assert.Contains("Title is required.", result.Errors);
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("  ")]
    public void Validate_WithMissingBody_ReturnsInvalid(string? body)
    {
        var note = CreateNote("test-id", "A valid title", body);
        
        var result = NoteValidator.Validate(note);
        
        Assert.False(result.IsValid);
        Assert.Contains("Body is required.", result.Errors);
    }
    
    [Fact]
    public void Validate_WithTitleOverMaxLength_ReturnsInvalid()
    {
        var note = CreateNote("test-id", new string('a', NoteValidator.MaxTitleLength + 1), "A valid body.");

        var result = NoteValidator.Validate(note);

        Assert.False(result.IsValid);
        Assert.Contains($"Title must be {NoteValidator.MaxTitleLength} characters or fewer.", result.Errors);
    }

    [Fact]
    public void Validate_WithTitleAtMaxLength_ReturnsValid()
    {
        var note = CreateNote("test-id", new string('a', NoteValidator.MaxTitleLength), "A valid body.");

        var result = NoteValidator.Validate(note);

        Assert.True(result.IsValid);
    }
    
    [Fact]
    public void Validate_WithBodyOverMaxLength_ReturnsInvalid()
    {
        var note = CreateNote("test-id", "A valid title", new string('a', NoteValidator.MaxBodyLength + 1));

        var result = NoteValidator.Validate(note);

        Assert.False(result.IsValid);
        Assert.Contains($"Body must be {NoteValidator.MaxBodyLength} characters or fewer.", result.Errors);
    }

    [Fact]
    public void Validate_WithBodyAtMaxLength_ReturnsValid()
    {
        var note = CreateNote("test-id", "A valid title", new string('a', NoteValidator.MaxBodyLength));

        var result = NoteValidator.Validate(note);

        Assert.True(result.IsValid);
    }
    
    [Fact]
    public void Validate_WithMissingIdTitleAndBody_ReturnsAllErrors()
    {
        var note = CreateNote("", "", "");

        var result = NoteValidator.Validate(note);

        Assert.False(result.IsValid);
        Assert.Equal(3, result.Errors.Count);
    }
    
}
using System.Globalization;
using System.Text;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using ExampleLibrary.Models;

namespace ExampleLibrary.Storage;

/// <summary>
/// An <see cref="INoteStore"/> implementation backed by an S3 bucket. Each note is stored as a
/// JSON object under a common key prefix, with title and creation time duplicated into object
/// metadata so <see cref="ListNotesAsync"/> can build summaries without fetching object bodies.
/// </summary>
public class S3NoteStore : INoteStore
{

    private const string KeyPrefix = "notes/";
    private const string TitleMetadataKey = "title";
    private const string CreatedAtMetadataKey = "created-at";

    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="S3NoteStore"/> class.
    /// </summary>
    /// <param name="s3Client">The S3 client used to access the bucket.</param>
    /// <param name="bucketName">The name of the bucket notes are stored in.</param>
    /// <exception cref="ArgumentNullException"><paramref name="s3Client"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="bucketName"/> is null or empty.</exception>
    public S3NoteStore(IAmazonS3 s3Client, string bucketName)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));

        if (string.IsNullOrEmpty(bucketName))
        {
            throw new ArgumentException("Bucket name must no be null or empty", nameof(bucketName));
        }
        
        _bucketName = bucketName;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="note"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="note"/>'s <c>Id</c> is null or empty.</exception>
    public async Task PutNoteAsync(Note note, CancellationToken cancellationToken = default)
    {
        if (note is null)
        {
            throw new ArgumentNullException(nameof(note));
        }
        
        if (string.IsNullOrWhiteSpace(note.Id))
        {
            throw new ArgumentException("Note Id must not be null or empty", nameof(note));
        }
        
        var json = JsonSerializer.Serialize(note, JsonOptions);

        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = BuildKey(note.Id),
            ContentBody = json
        };
        
        request.Metadata.Add(TitleMetadataKey, note.Title);
        request.Metadata.Add(CreatedAtMetadataKey, note.CreatedAt.ToString("o"));
        
        await _s3Client.PutObjectAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="id"/> is null or empty.</exception>
    public async Task<Note?> GetNoteAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Id must not be null or empty", nameof(id));
        }

        try
        {
            var response = await _s3Client.GetObjectAsync(_bucketName, BuildKey(id), cancellationToken);
            
            using var reader = new StreamReader(response.ResponseStream, Encoding.UTF8);
            var json = await  reader.ReadToEndAsync();
            
            return JsonSerializer.Deserialize<Note>(json, JsonOptions);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<NoteSummary>> ListNotesAsync(CancellationToken cancellationToken = default)
    {
        var summaries = new List<NoteSummary>();
        
        var listRequest = new ListObjectsV2Request
        {
            BucketName = _bucketName,
            Prefix = KeyPrefix
        };
        
        ListObjectsV2Response listResponse;

        do
        {
            listResponse = await _s3Client.ListObjectsV2Async(listRequest, cancellationToken);

            foreach (var obj in listResponse.S3Objects)
            {
                var headResponse = await _s3Client.GetObjectMetadataAsync(
                    new GetObjectMetadataRequest
                    {
                        BucketName = _bucketName,
                        Key = obj.Key
                    }, cancellationToken);

                summaries.Add(MapToSummary(obj.Key, headResponse.Metadata));
            }

            listRequest.ContinuationToken = listResponse.NextContinuationToken;
        } while (listResponse.IsTruncated == true);

        return summaries;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="id"/> is null or empty.</exception>
    public async Task DeleteNoteAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Id must not be null or empty", nameof(id));
        }
        
        await _s3Client.DeleteObjectAsync(_bucketName, BuildKey(id), cancellationToken);
    }
    
    /// <summary>Builds the S3 object key for a note id.</summary>
    private static string BuildKey(string id) => $"{KeyPrefix}{id}.json";

    /// <summary>Recovers a note id from an S3 object key produced by <see cref="BuildKey"/>.</summary>
    private static string ExtractIdFromKey(string key) =>
        key.Substring(KeyPrefix.Length, key.Length - KeyPrefix.Length - ".json".Length);

    /// <summary>Builds a <see cref="NoteSummary"/> from an object key and its S3 metadata.</summary>
    private static NoteSummary MapToSummary(string key, MetadataCollection metadata)
    {
        var id = ExtractIdFromKey(key);
        var title = metadata[TitleMetadataKey] ?? string.Empty;
        var createdAt = DateTimeOffset.Parse(
            metadata[CreatedAtMetadataKey],
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);

        return new NoteSummary(id, title, createdAt);
    }
    
}

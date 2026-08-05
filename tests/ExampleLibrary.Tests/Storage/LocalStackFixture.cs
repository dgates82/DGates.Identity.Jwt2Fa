using Amazon.S3;

namespace ExampleLibrary.Tests.Storage;

public class LocalStackFixture
{
    public const string BucketName = "notes-bucket";
    public IAmazonS3 S3Client { get; }

    public LocalStackFixture()
    {
        var endpoint = Environment.GetEnvironmentVariable("LOCALSTACK_ENDPOINT") ??  "http://localhost:4566";
        
        S3Client = new AmazonS3Client("test", 
            "test", 
            new AmazonS3Config
            {
                ServiceURL = endpoint,
                ForcePathStyle = true,
                AuthenticationRegion = "us-west-2"
            });
    }
}

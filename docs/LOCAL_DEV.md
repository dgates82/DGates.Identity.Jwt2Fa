# dotnet-nuget-release-template — Local Development

This repo's example library and tests can run entirely without a real AWS account using
[LocalStack](https://www.localstack.cloud/).

## Setup

1. Start LocalStack — the notes bucket is created and a starting note is seeded automatically
   once the container is healthy:
```sh
   docker compose up -d --wait
```

2. Tests point `S3NoteStore` at LocalStack via `LocalStackFixture`
   (`tests/ExampleLibrary.Tests/Storage/LocalStackFixture.cs`):
```csharp
   var endpoint = Environment.GetEnvironmentVariable("LOCALSTACK_ENDPOINT") ?? "http://localhost:4566";

   var s3Client = new AmazonS3Client(
       "test",
       "test",
       new AmazonS3Config
       {
           ServiceURL = endpoint,
           ForcePathStyle = true,
           AuthenticationRegion = "us-west-2"
       });

   var store = new S3NoteStore(s3Client, bucketName: "notes-bucket");
```
Works with no setup against the default `docker compose` port mapping. Only set
`LOCALSTACK_ENDPOINT` if you're pointing at a non-default address.

## Running tests

```sh
# Unit tests only (no Docker required)
dotnet test --filter "Category!=Integration"

# Integration tests (requires LocalStack running + seeded, see above)
dotnet test --filter "Category=Integration"

# Everything
dotnet test
```

## Tearing down

```sh
docker compose down -v
```

## Building a local NuGet package

Use this to test `dotnet pack`/versioning locally before relying on `release.yml`, or to
sanity-check the packed output before tagging a real release.

```sh
dotnet pack --configuration Release /p:Version=0.1.0 --output ./nupkg
```

To reference the local package from another project, add a local NuGet source:

```sh
dotnet nuget add source /path/to/this/repo/nupkg --name LocalTemplateTest
```

Then reference it normally in the consuming project's `.csproj`:

```xml
<PackageReference Include="ExampleLibrary" Version="0.1.0" />
```

## Developing without Docker

`ExampleLibrary` has no local-fallback mode — unlike some libraries, `S3NoteStore` doesn't
ship a non-AWS code path to fall back to, since the point of this example is specifically to
demonstrate integration testing against a real external dependency in CI.

- **Unit tests still run** — `NoteValidatorTests` doesn't depend on Docker or LocalStack at
  all.
- **Integration tests (`Category=Integration`) require LocalStack and can't be skipped or
  faked.** They exist specifically to verify the real S3 code path; there's no fallback branch
  to substitute.
- If you can't run Docker locally (e.g. a nested VM without virtualization passthrough), the
  more faithful option is pointing `S3NoteStore` at a real, disposable S3 bucket with a narrow
  IAM policy, rather than trying to stub it out.

This is different from the `LocalJsonFallbackPath` pattern used in
[`DGates.AwsSecretsManager`](https://github.com/dgates82/DGates.AwsSecretsManager) — that
fallback exists for *consumers* who want to develop their own app without any AWS dependency
at all. `ExampleLibrary` doesn't need that, since it's a disposable demonstration, not a
product meant to support Docker-free consumer development.
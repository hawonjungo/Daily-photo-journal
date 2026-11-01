using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Options;
using Relifes.Api.Storage;

namespace Relifes.Api.Tests;

public class R2ImageStorageTests
{
    private static R2ImageStorage Storage(string serviceUrl)
    {
        var s3 = new AmazonS3Client(
            new BasicAWSCredentials("key", "secret"),
            new AmazonS3Config { ServiceURL = serviceUrl, AuthenticationRegion = "auto", ForcePathStyle = true });

        return new R2ImageStorage(s3, Options.Create(new R2Options { ServiceUrl = serviceUrl, BucketName = "photos" }));
    }

    [Theory]
    [InlineData("https://account.r2.cloudflarestorage.com", "https://account.r2.cloudflarestorage.com/photos/a/full.webp?")]
    [InlineData("http://localhost:19000", "http://localhost:19000/photos/a/full.webp?")]
    public async Task Signed_url_uses_the_scheme_of_the_service_url(string serviceUrl, string expectedStart)
    {
        var url = await Storage(serviceUrl).GetDownloadUrlAsync("a/full.webp", TimeSpan.FromHours(6));

        Assert.StartsWith(expectedStart, url);
        Assert.Contains("X-Amz-Signature=", url);
        Assert.Contains("X-Amz-Algorithm=AWS4-HMAC-SHA256", url);
    }
}

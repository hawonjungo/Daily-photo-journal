using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace Relifes.Api.Storage;

public class R2ImageStorage(IAmazonS3 s3, IOptions<R2Options> options) : IImageStorage
{
    private readonly string _bucket = options.Value.BucketName;

    // R2 is always https. Plain http is only for a local S3 server during development or demos.
    private readonly bool _useHttps = options.Value.ServiceUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    public async Task UploadAsync(string key, byte[] content, string contentType, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(content);
        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = stream,
            ContentType = contentType,
            // R2 does not support the SDK's default streaming payload signature and trailing checksums.
            // The SDK only allows an unsigned payload over https.
            DisablePayloadSigning = _useHttps,
            DisableDefaultChecksumValidation = true,
        }, cancellationToken);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        s3.DeleteObjectAsync(_bucket, key, cancellationToken);

    public Task<string> GetDownloadUrlAsync(string key, TimeSpan lifetime) =>
        s3.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Protocol = _useHttps ? Protocol.HTTPS : Protocol.HTTP,
            Expires = DateTime.UtcNow.Add(lifetime),
        });
}

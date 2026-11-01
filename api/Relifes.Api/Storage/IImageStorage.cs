namespace Relifes.Api.Storage;

public interface IImageStorage
{
    Task UploadAsync(string key, byte[] content, string contentType, CancellationToken cancellationToken);
    Task DeleteAsync(string key, CancellationToken cancellationToken);

    /// <summary>Signed, expiring URL. The bucket is private and &lt;img&gt; tags cannot send an API key.</summary>
    Task<string> GetDownloadUrlAsync(string key, TimeSpan lifetime);
}

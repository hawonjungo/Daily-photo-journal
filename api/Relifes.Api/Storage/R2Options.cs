using System.ComponentModel.DataAnnotations;

namespace Relifes.Api.Storage;

public class R2Options
{
    public const string SectionName = "R2";

    /// <summary>S3 endpoint, e.g. https://&lt;account id&gt;.r2.cloudflarestorage.com</summary>
    [Required, Url]
    public string ServiceUrl { get; set; } = "";

    [Required]
    public string AccessKeyId { get; set; } = "";

    [Required]
    public string SecretAccessKey { get; set; } = "";

    [Required]
    public string BucketName { get; set; } = "";
}

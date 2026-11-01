using Amazon.Runtime;
using Amazon.S3;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Relifes.Api.Auth;
using Relifes.Api.Data;
using Relifes.Api.Entries;
using Relifes.Api.Places;
using Relifes.Api.Storage;
using Relifes.Api.Summaries;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' is not configured. See README.md.");

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();
builder.Services.AddOpenApi();

builder.Services.AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, null);
builder.Services.AddAuthorization();

builder.Services.AddOptions<R2Options>()
    .Bind(builder.Configuration.GetSection(R2Options.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<IAmazonS3>(services =>
{

    var r2 = services.GetRequiredService<IOptions<R2Options>>().Value;
    return new AmazonS3Client(
        new BasicAWSCredentials(r2.AccessKeyId, r2.SecretAccessKey),
        new AmazonS3Config
        {
            ServiceURL = r2.ServiceUrl,
            AuthenticationRegion = "auto",
            ForcePathStyle = true,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        });
});
builder.Services.AddSingleton<IImageStorage, R2ImageStorage>();

builder.Services.Configure<PlacesOptions>(builder.Configuration.GetSection(PlacesOptions.SectionName));
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<IPlacesClient, GooglePlacesClient>(http => http.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddScoped<NearbyPlacesService>();

var app = builder.Build();

if (args.FirstOrDefault() == CreateUserCommand.Name)
{
    return await CreateUserCommand.RunAsync(app.Services, args);
}

if (args.FirstOrDefault() == "migrate")
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    return 0;
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapEntryEndpoints();
app.MapPlaceEndpoints();
app.MapSummaryEndpoints();

app.Run();
return 0;

public partial class Program;

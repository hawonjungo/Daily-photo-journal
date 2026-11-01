using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Relifes.Api.Data;

namespace Relifes.Api.Auth;

public class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AppDbContext db)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var key = Request.Headers[HeaderName].ToString();
        if (string.IsNullOrEmpty(key))
        {
            return AuthenticateResult.NoResult();
        }

        var hash = ApiKey.Hash(key);
        var userId = await db.Users
            .Where(u => u.ApiKeyHash == hash)
            .Select(u => (Guid?)u.Id)
            .SingleOrDefaultAsync(Context.RequestAborted);

        if (userId is null)
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())], SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}

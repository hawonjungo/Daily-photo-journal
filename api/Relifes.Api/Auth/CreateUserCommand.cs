using Microsoft.EntityFrameworkCore;
using Relifes.Api.Data;
using Relifes.Api.Entities;

namespace Relifes.Api.Auth;

/// <summary>Usage: dotnet run --project api/Relifes.Api -- create-user &lt;email&gt; &lt;display name&gt;</summary>
public static class CreateUserCommand
{
    public const string Name = "create-user";

    public static async Task<int> RunAsync(IServiceProvider services, string[] args)
    {
        if (args.Length != 3)
        {
            Console.Error.WriteLine($"Usage: {Name} <email> <display name>");
            return 1;
        }

        var email = args[1].Trim();
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (await db.Users.AnyAsync(u => u.Email == email))
        {
            Console.Error.WriteLine($"User {email} already exists.");
            return 1;
        }

        var key = ApiKey.Generate();
        db.Users.Add(new User
        {
            Email = email,
            DisplayName = args[2].Trim(),
            ApiKeyHash = ApiKey.Hash(key),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        Console.WriteLine($"Created {email}. API key (shown only once):");
        Console.WriteLine(key);
        return 0;
    }
}

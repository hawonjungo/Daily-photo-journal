using Microsoft.EntityFrameworkCore;
using Relifes.Api.Entities;

namespace Relifes.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Place> Places => Set<Place>();
    public DbSet<Entry> Entries => Set<Entry>();
    public DbSet<GoogleApiUsage> GoogleApiUsage => Set<GoogleApiUsage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(user =>
        {
            user.Property(u => u.Id).ValueGeneratedNever();
            user.Property(u => u.Email).HasMaxLength(320);
            user.Property(u => u.DisplayName).HasMaxLength(100);
            user.Property(u => u.ApiKeyHash).HasMaxLength(200);

            user.HasIndex(u => u.Email).IsUnique();
            user.HasIndex(u => u.ApiKeyHash).IsUnique();
        });

        modelBuilder.Entity<GoogleApiUsage>(usage =>
        {
            usage.HasKey(u => u.Date);
            usage.ToTable("GoogleApiUsage");
        });

        modelBuilder.Entity<Place>(place =>
        {
            place.Property(p => p.Id).ValueGeneratedNever();
            place.Property(p => p.GooglePlaceId).HasMaxLength(300);
            place.Property(p => p.Name).HasMaxLength(200);
            place.Property(p => p.Address).HasMaxLength(500);

            place.HasOne(p => p.CreatedByUser)
                .WithMany()
                .HasForeignKey(p => p.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            place.HasIndex(p => p.GooglePlaceId)
                .IsUnique()
                .HasFilter("\"GooglePlaceId\" IS NOT NULL");
            place.HasIndex(p => new { p.Lat, p.Lng });
        });

        modelBuilder.Entity<Entry>(entry =>
        {
            entry.Property(e => e.Id).ValueGeneratedNever();
            entry.Property(e => e.TimeZone).HasMaxLength(64);
            entry.Property(e => e.ImageKey).HasMaxLength(300);
            entry.Property(e => e.ThumbKey).HasMaxLength(300);
            entry.Property(e => e.Price).HasColumnType("numeric(18,2)");
            entry.Property(e => e.Currency).HasColumnType("char(3)");
            entry.Property(e => e.Category).HasMaxLength(50);

            entry.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entry.HasOne(e => e.Place)
                .WithMany()
                .HasForeignKey(e => e.PlaceId)
                .OnDelete(DeleteBehavior.SetNull);

            entry.HasIndex(e => new { e.UserId, e.TakenAt });

            entry.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Entries_Rating", "\"Rating\" BETWEEN 1 AND 5");
                table.HasCheckConstraint("CK_Entries_PriceHasCurrency", "\"Price\" IS NULL OR \"Currency\" IS NOT NULL");
            });
        });
    }
}

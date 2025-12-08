using LrmCloud.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace LrmCloud.Api.Data;

/// <summary>
/// Entity Framework Core database context for LRM Cloud.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // Users & Auth
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();
    public DbSet<OrganizationInvitation> OrganizationInvitations => Set<OrganizationInvitation>();

    // Projects & Resources
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ResourceKey> ResourceKeys => Set<ResourceKey>();
    public DbSet<Translation> Translations => Set<Translation>();

    // API Keys
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<UserApiKey> UserApiKeys => Set<UserApiKey>();
    public DbSet<OrganizationApiKey> OrganizationApiKeys => Set<OrganizationApiKey>();
    public DbSet<ProjectApiKey> ProjectApiKeys => Set<ProjectApiKey>();

    // Sync & Audit
    public DbSet<SyncHistory> SyncHistory => Set<SyncHistory>();
    public DbSet<SyncConflict> SyncConflicts => Set<SyncConflict>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // =====================================================================
        // Users
        // =====================================================================
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.GitHubId).IsUnique();
            entity.HasIndex(e => e.Username);

            // Soft delete filter
            entity.HasQueryFilter(e => e.DeletedAt == null);
        });

        // =====================================================================
        // Refresh Tokens
        // =====================================================================
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.ExpiresAt);
            entity.HasIndex(e => e.TokenHash);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Match parent's soft-delete filter
            entity.HasQueryFilter(e => e.User!.DeletedAt == null);
        });

        // =====================================================================
        // Organizations
        // =====================================================================
        modelBuilder.Entity<Organization>(entity =>
        {
            entity.HasIndex(e => e.Slug).IsUnique();

            entity.HasOne(e => e.Owner)
                .WithMany()
                .HasForeignKey(e => e.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Soft delete filter
            entity.HasQueryFilter(e => e.DeletedAt == null);
        });

        // =====================================================================
        // Organization Members
        // =====================================================================
        modelBuilder.Entity<OrganizationMember>(entity =>
        {
            entity.HasIndex(e => new { e.OrganizationId, e.UserId }).IsUnique();
            entity.HasIndex(e => e.OrganizationId);
            entity.HasIndex(e => e.UserId);

            // Configure the User relationship (the member)
            entity.HasOne(e => e.User)
                .WithMany(u => u.OrganizationMemberships)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure the InvitedBy relationship (who invited them)
            entity.HasOne(e => e.InvitedBy)
                .WithMany()
                .HasForeignKey(e => e.InvitedById)
                .OnDelete(DeleteBehavior.SetNull);

            // Match parent's soft-delete filters (both org and user)
            entity.HasQueryFilter(e => e.Organization!.DeletedAt == null && e.User!.DeletedAt == null);
        });

        // =====================================================================
        // Organization Invitations
        // =====================================================================
        modelBuilder.Entity<OrganizationInvitation>(entity =>
        {
            entity.HasIndex(e => new { e.OrganizationId, e.Email }).IsUnique();
            entity.HasIndex(e => e.Email);
            entity.HasIndex(e => e.TokenHash);
            entity.HasIndex(e => e.ExpiresAt);

            entity.HasOne(e => e.Organization)
                .WithMany()
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Inviter)
                .WithMany()
                .HasForeignKey(e => e.InvitedBy)
                .OnDelete(DeleteBehavior.Cascade);

            // Match parent's soft-delete filter
            entity.HasQueryFilter(e => e.Organization!.DeletedAt == null);
        });

        // =====================================================================
        // Projects
        // =====================================================================
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.OrganizationId);
            entity.HasIndex(e => e.GitHubRepo);
            entity.HasIndex(e => e.ConfigUpdatedBy);

            // Configure ConfigUpdater relationship (who last updated config)
            // Note: User relationship is already configured via [ForeignKey] attribute
            entity.HasOne(e => e.ConfigUpdater)
                .WithMany()
                .HasForeignKey(e => e.ConfigUpdatedBy)
                .OnDelete(DeleteBehavior.SetNull);

            // Check constraint: must have either user_id or organization_id
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_projects_owner",
                "(user_id IS NOT NULL AND organization_id IS NULL) OR (user_id IS NULL AND organization_id IS NOT NULL)"
            ));
        });

        // =====================================================================
        // Resource Keys
        // =====================================================================
        modelBuilder.Entity<ResourceKey>(entity =>
        {
            entity.HasIndex(e => new { e.ProjectId, e.KeyName }).IsUnique();
            entity.HasIndex(e => e.ProjectId);
        });

        // =====================================================================
        // Translations
        // =====================================================================
        modelBuilder.Entity<Translation>(entity =>
        {
            entity.HasIndex(e => new { e.ResourceKeyId, e.LanguageCode, e.PluralForm }).IsUnique();
            entity.HasIndex(e => e.ResourceKeyId);
            entity.HasIndex(e => e.LanguageCode);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.UpdatedAt);
        });

        // =====================================================================
        // API Keys
        // =====================================================================
        modelBuilder.Entity<ApiKey>(entity =>
        {
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.KeyPrefix);
            entity.HasIndex(e => e.ExpiresAt);

            // Match parent's soft-delete filter
            entity.HasQueryFilter(e => e.User!.DeletedAt == null);
        });

        // =====================================================================
        // Translation API Keys (hierarchical)
        // =====================================================================
        modelBuilder.Entity<UserApiKey>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.Provider }).IsUnique();

            // Match parent's soft-delete filter
            entity.HasQueryFilter(e => e.User!.DeletedAt == null);
        });

        modelBuilder.Entity<OrganizationApiKey>(entity =>
        {
            entity.HasIndex(e => new { e.OrganizationId, e.Provider }).IsUnique();

            // Match parent's soft-delete filter
            entity.HasQueryFilter(e => e.Organization!.DeletedAt == null);
        });

        modelBuilder.Entity<ProjectApiKey>(entity =>
        {
            entity.HasIndex(e => new { e.ProjectId, e.Provider }).IsUnique();
        });

        // =====================================================================
        // Sync History
        // =====================================================================
        modelBuilder.Entity<SyncHistory>(entity =>
        {
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<SyncConflict>(entity =>
        {
            entity.HasIndex(e => e.ProjectId);
        });

        // =====================================================================
        // Audit Log
        // =====================================================================
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Action);
        });
    }

    /// <summary>
    /// Override SaveChanges to automatically set UpdatedAt timestamps.
    /// </summary>
    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            if (entry.Entity is User user)
                user.UpdatedAt = DateTime.UtcNow;
            else if (entry.Entity is Organization org)
                org.UpdatedAt = DateTime.UtcNow;
            else if (entry.Entity is Project project)
                project.UpdatedAt = DateTime.UtcNow;
            else if (entry.Entity is ResourceKey key)
                key.UpdatedAt = DateTime.UtcNow;
            else if (entry.Entity is Translation translation)
                translation.UpdatedAt = DateTime.UtcNow;
        }
    }
}

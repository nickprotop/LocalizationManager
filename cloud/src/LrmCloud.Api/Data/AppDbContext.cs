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
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<GitHubSyncState> GitHubSyncStates => Set<GitHubSyncState>();
    public DbSet<PendingConflict> PendingConflicts => Set<PendingConflict>();

    // Snapshots
    public DbSet<Snapshot> Snapshots => Set<Snapshot>();

    // Translation Usage History
    public DbSet<TranslationUsageHistory> TranslationUsageHistory => Set<TranslationUsageHistory>();

    // Usage Events (detailed per-translation tracking)
    public DbSet<UsageEvent> UsageEvents => Set<UsageEvent>();

    // Translation Memory
    public DbSet<TranslationMemory> TranslationMemories => Set<TranslationMemory>();

    // Glossary
    public DbSet<GlossaryTerm> GlossaryTerms => Set<GlossaryTerm>();
    public DbSet<GlossaryTranslation> GlossaryTranslations => Set<GlossaryTranslation>();
    public DbSet<GlossaryProviderSync> GlossaryProviderSyncs => Set<GlossaryProviderSync>();

    // Review Workflow
    public DbSet<ProjectReviewer> ProjectReviewers => Set<ProjectReviewer>();
    public DbSet<OrganizationReviewer> OrganizationReviewers => Set<OrganizationReviewer>();

    // Billing & Webhooks
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();

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

            // Configure GitHubConnectedByUser relationship (who connected GitHub)
            entity.HasOne(e => e.GitHubConnectedByUser)
                .WithMany()
                .HasForeignKey(e => e.GitHubConnectedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

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

            // Unique slug per user (for personal projects)
            entity.HasIndex(e => new { e.UserId, e.Slug })
                .HasFilter("user_id IS NOT NULL")
                .IsUnique();

            // Unique slug per organization (for org projects)
            entity.HasIndex(e => new { e.OrganizationId, e.Slug })
                .HasFilter("organization_id IS NOT NULL")
                .IsUnique();

            // Configure ConfigUpdater relationship (who last updated config)
            // Note: User relationship is already configured via [ForeignKey] attribute
            entity.HasOne(e => e.ConfigUpdater)
                .WithMany()
                .HasForeignKey(e => e.ConfigUpdatedBy)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure GitHubConnectedByUser relationship (who connected GitHub)
            entity.HasOne(e => e.GitHubConnectedByUser)
                .WithMany()
                .HasForeignKey(e => e.GitHubConnectedByUserId)
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

            // Configure optimistic locking for concurrent updates
            entity.Property(e => e.Version).IsConcurrencyToken();
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
            entity.HasIndex(e => new { e.ProjectId, e.HistoryId }).IsUnique();
            entity.HasIndex(e => e.UserId);

            entity.HasOne(e => e.Project)
                .WithMany(p => p.SyncHistory)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.RevertedFrom)
                .WithMany()
                .HasForeignKey(e => e.RevertedFromId)
                .OnDelete(DeleteBehavior.SetNull);
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

        // =====================================================================
        // GitHub Sync State (for three-way merge between Cloud and GitHub)
        // =====================================================================
        modelBuilder.Entity<GitHubSyncState>(entity =>
        {
            // Unique per project + key + language + plural form
            entity.HasIndex(e => new { e.ProjectId, e.KeyName, e.LanguageCode, e.PluralForm }).IsUnique();
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.SyncedAt);

            entity.HasOne(e => e.Project)
                .WithMany(p => p.GitHubSyncStates)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure optimistic locking for concurrent sync operations
            entity.Property(e => e.Version).IsConcurrencyToken();
        });

        // =====================================================================
        // Pending Conflicts (for GitHub pull conflict resolution)
        // =====================================================================
        modelBuilder.Entity<PendingConflict>(entity =>
        {
            // Unique per project + key + language + plural form
            entity.HasIndex(e => new { e.ProjectId, e.KeyName, e.LanguageCode, e.PluralForm }).IsUnique();
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.CreatedAt);

            entity.HasOne(e => e.Project)
                .WithMany()
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // =====================================================================
        // Snapshots
        // =====================================================================
        modelBuilder.Entity<Snapshot>(entity =>
        {
            entity.HasIndex(e => new { e.ProjectId, e.SnapshotId }).IsUnique();
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.SnapshotType);

            entity.HasOne(e => e.Project)
                .WithMany(p => p.Snapshots)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.CreatedBy)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // =====================================================================
        // Translation Usage History
        // =====================================================================
        modelBuilder.Entity<TranslationUsageHistory>(entity =>
        {
            // Unique constraint: one record per user+provider+period
            entity.HasIndex(e => new { e.UserId, e.ProviderName, e.PeriodStart }).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.ProviderName);
            entity.HasIndex(e => e.PeriodStart);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Match parent's soft-delete filter
            entity.HasQueryFilter(e => e.User!.DeletedAt == null);
        });

        // =====================================================================
        // Usage Events (detailed per-translation tracking)
        // =====================================================================
        modelBuilder.Entity<UsageEvent>(entity =>
        {
            entity.HasIndex(e => e.ActingUserId);
            entity.HasIndex(e => e.BilledUserId);
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.OrganizationId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.OrganizationId, e.CreatedAt });
            entity.HasIndex(e => new { e.ProjectId, e.CreatedAt });

            entity.HasOne(e => e.ActingUser)
                .WithMany()
                .HasForeignKey(e => e.ActingUserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.BilledUser)
                .WithMany()
                .HasForeignKey(e => e.BilledUserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Project)
                .WithMany()
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Organization)
                .WithMany()
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // =====================================================================
        // Translation Memory
        // =====================================================================
        modelBuilder.Entity<TranslationMemory>(entity =>
        {
            // Unique: user + source lang + target lang + source hash
            entity.HasIndex(e => new { e.UserId, e.SourceLanguage, e.TargetLanguage, e.SourceHash }).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.OrganizationId);
            entity.HasIndex(e => e.SourceLanguage);
            entity.HasIndex(e => e.TargetLanguage);
            entity.HasIndex(e => e.UpdatedAt);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Organization)
                .WithMany()
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.SetNull);

            // Match parent's soft-delete filter
            entity.HasQueryFilter(e => e.User!.DeletedAt == null);
        });

        // =====================================================================
        // Glossary Terms
        // =====================================================================
        modelBuilder.Entity<GlossaryTerm>(entity =>
        {
            // Unique per project (filtered index)
            entity.HasIndex(e => new { e.ProjectId, e.SourceTerm, e.SourceLanguage })
                .HasFilter("project_id IS NOT NULL")
                .IsUnique();

            // Unique per organization (filtered index)
            entity.HasIndex(e => new { e.OrganizationId, e.SourceTerm, e.SourceLanguage })
                .HasFilter("organization_id IS NOT NULL")
                .IsUnique();

            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.OrganizationId);

            entity.HasOne(e => e.Project)
                .WithMany()
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Organization)
                .WithMany()
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Creator)
                .WithMany()
                .HasForeignKey(e => e.CreatedBy)
                .OnDelete(DeleteBehavior.SetNull);

            // Check constraint: must have exactly one owner (project or organization)
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_glossary_terms_owner",
                "(project_id IS NOT NULL AND organization_id IS NULL) OR (project_id IS NULL AND organization_id IS NOT NULL)"
            ));
        });

        // =====================================================================
        // Glossary Translations
        // =====================================================================
        modelBuilder.Entity<GlossaryTranslation>(entity =>
        {
            entity.HasIndex(e => new { e.TermId, e.TargetLanguage }).IsUnique();
            entity.HasIndex(e => e.TermId);

            entity.HasOne(e => e.Term)
                .WithMany(t => t.Translations)
                .HasForeignKey(e => e.TermId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // =====================================================================
        // Glossary Provider Sync
        // =====================================================================
        modelBuilder.Entity<GlossaryProviderSync>(entity =>
        {
            // Unique per project (filtered index)
            entity.HasIndex(e => new { e.ProjectId, e.ProviderName, e.SourceLanguage, e.TargetLanguage })
                .HasFilter("project_id IS NOT NULL")
                .IsUnique();

            // Unique per organization (filtered index)
            entity.HasIndex(e => new { e.OrganizationId, e.ProviderName, e.SourceLanguage, e.TargetLanguage })
                .HasFilter("organization_id IS NOT NULL")
                .IsUnique();

            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.OrganizationId);

            entity.HasOne(e => e.Project)
                .WithMany()
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Organization)
                .WithMany()
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            // Check constraint: must have exactly one owner (project or organization)
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_glossary_provider_sync_owner",
                "(project_id IS NOT NULL AND organization_id IS NULL) OR (project_id IS NULL AND organization_id IS NOT NULL)"
            ));
        });

        // =====================================================================
        // Project Reviewers
        // =====================================================================
        modelBuilder.Entity<ProjectReviewer>(entity =>
        {
            // Unique: one user can only have one role per project
            entity.HasIndex(e => new { e.ProjectId, e.UserId }).IsUnique();
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.UserId);

            entity.HasOne(e => e.Project)
                .WithMany(p => p.Reviewers)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.AddedBy)
                .WithMany()
                .HasForeignKey(e => e.AddedById)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // =====================================================================
        // Organization Reviewers
        // =====================================================================
        modelBuilder.Entity<OrganizationReviewer>(entity =>
        {
            // Unique: one user can only have one role per organization
            entity.HasIndex(e => new { e.OrganizationId, e.UserId }).IsUnique();
            entity.HasIndex(e => e.OrganizationId);
            entity.HasIndex(e => e.UserId);

            entity.HasOne(e => e.Organization)
                .WithMany(o => o.Reviewers)
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.AddedBy)
                .WithMany()
                .HasForeignKey(e => e.AddedById)
                .OnDelete(DeleteBehavior.SetNull);

            // Match parent's soft-delete filter
            entity.HasQueryFilter(e => e.Organization!.DeletedAt == null);
        });

        // =====================================================================
        // Webhook Events (Idempotency)
        // =====================================================================
        modelBuilder.Entity<WebhookEvent>(entity =>
        {
            // Unique constraint to prevent duplicate processing
            entity.HasIndex(e => new { e.ProviderEventId, e.ProviderName }).IsUnique();
            entity.HasIndex(e => e.ProcessedAt);
            entity.HasIndex(e => e.UserId);
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
            else if (entry.Entity is TranslationMemory tm)
                tm.UpdatedAt = DateTime.UtcNow;
            else if (entry.Entity is GlossaryTerm glossaryTerm)
                glossaryTerm.UpdatedAt = DateTime.UtcNow;
            else if (entry.Entity is GlossaryTranslation glossaryTranslation)
                glossaryTranslation.UpdatedAt = DateTime.UtcNow;
            else if (entry.Entity is GlossaryProviderSync glossarySync)
                glossarySync.UpdatedAt = DateTime.UtcNow;
        }
    }
}

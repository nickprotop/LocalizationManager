using LrmCloud.Api.Data;
using LrmCloud.Shared.DTOs.Projects;
using LrmCloud.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace LrmCloud.Api.Services;

/// <summary>
/// Service for creating sample/demo projects for user onboarding.
/// </summary>
public class SampleProjectService
{
    private readonly AppDbContext _db;
    private readonly IProjectService _projectService;
    private readonly ILogger<SampleProjectService> _logger;

    public SampleProjectService(
        AppDbContext db,
        IProjectService projectService,
        ILogger<SampleProjectService> logger)
    {
        _db = db;
        _projectService = projectService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a sample project for the user if they haven't received one before.
    /// Returns existing sample project if already created.
    /// </summary>
    public async Task<ProjectDto?> CreateSampleProjectAsync(int userId)
    {
        try
        {
            // Check if user already has a sample project
            var existingProject = await _db.Projects
                .Include(p => p.ResourceKeys)
                    .ThenInclude(k => k.Translations)
                .FirstOrDefaultAsync(p => p.UserId == userId && p.IsSampleProject);

            if (existingProject != null)
            {
                _logger.LogInformation("User {UserId} already has sample project {ProjectId}", userId, existingProject.Id);
                return await _projectService.GetProjectAsync(existingProject.Id, userId);
            }

            // Get user to update the flag
            var user = await _db.Users.FindAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found when creating sample project", userId);
                return null;
            }

            // Create sample project
            var project = new Project
            {
                UserId = userId,
                Slug = $"sample-app-demo",
                Name = "Sample App (Demo)",
                Description = "A sample project demonstrating LRM Cloud features. Feel free to explore and delete it when ready!",
                DefaultLanguage = "en",
                IsSampleProject = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Projects.Add(project);
            await _db.SaveChangesAsync();

            // Populate sample data
            await PopulateSampleDataAsync(project);

            // Mark user as having received sample project
            user.HasReceivedSampleProject = true;
            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            _logger.LogInformation("Created sample project {ProjectId} for user {UserId}", project.Id, userId);

            return await _projectService.GetProjectAsync(project.Id, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating sample project for user {UserId}", userId);
            return null;
        }
    }

    /// <summary>
    /// Checks if the user should see a sample project creation option.
    /// Returns true if user has no projects and hasn't received a sample before.
    /// </summary>
    public async Task<(bool ShouldAutoCreate, bool CanCreateSample)> GetSampleProjectStatusAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return (false, false);

        var projectCount = await _db.Projects.CountAsync(p => p.UserId == userId);

        // Check organization projects too
        var orgIds = await _db.OrganizationMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.OrganizationId)
            .ToListAsync();

        var orgProjectCount = await _db.Projects
            .CountAsync(p => p.OrganizationId != null && orgIds.Contains(p.OrganizationId.Value));

        var hasNoProjects = projectCount == 0 && orgProjectCount == 0;

        // Auto-create only for first-time users (no projects + never received sample)
        var shouldAutoCreate = hasNoProjects && !user.HasReceivedSampleProject;

        // Can manually create sample if user has no projects (even if received before)
        var canCreateSample = hasNoProjects;

        return (shouldAutoCreate, canCreateSample);
    }

    private async Task PopulateSampleDataAsync(Project project)
    {
        // Sample keys for a realistic mobile app
        var sampleData = new List<(string Key, Dictionary<string, string> Translations, string? Comment)>
        {
            ("welcome_title", new Dictionary<string, string>
            {
                { "en", "Welcome!" },
                { "es", "¡Bienvenido!" },
                { "fr", "Bienvenue!" },
                { "de", "Willkommen!" }
            }, "Shown on the welcome screen"),

            ("welcome_subtitle", new Dictionary<string, string>
            {
                { "en", "Let's get started" },
                { "es", "Empecemos" },
                { "fr", "Commençons" }
            }, null),

            ("login_button", new Dictionary<string, string>
            {
                { "en", "Log In" },
                { "es", "Iniciar sesión" },
                { "fr", "Connexion" }
            }, "Primary login button"),

            ("signup_button", new Dictionary<string, string>
            {
                { "en", "Sign Up" },
                { "es", "Registrarse" },
                { "fr", "S'inscrire" }
            }, null),

            ("email_placeholder", new Dictionary<string, string>
            {
                { "en", "Enter your email" },
                { "es", "Ingresa tu email" }
            }, "Email field placeholder"),

            ("password_placeholder", new Dictionary<string, string>
            {
                { "en", "Enter password" },
                { "es", "Ingresa contraseña" }
            }, null),

            ("settings_title", new Dictionary<string, string>
            {
                { "en", "Settings" },
                { "es", "Configuración" },
                { "fr", "Paramètres" },
                { "de", "Einstellungen" }
            }, "Settings page header"),

            ("profile_name", new Dictionary<string, string>
            {
                { "en", "Name" },
                { "es", "Nombre" },
                { "fr", "Nom" },
                { "de", "Name" }
            }, null),

            ("error_network", new Dictionary<string, string>
            {
                { "en", "Network error. Please check your connection." },
                { "es", "Error de red. Por favor verifica tu conexión." },
                { "fr", "Erreur réseau. Veuillez vérifier votre connexion." }
            }, "Generic network error message"),

            ("error_required", new Dictionary<string, string>
            {
                { "en", "This field is required" }
            }, "Form validation message"),

            ("success_saved", new Dictionary<string, string>
            {
                { "en", "Changes saved successfully!" },
                { "es", "¡Cambios guardados exitosamente!" }
            }, "Success notification"),

            ("button_cancel", new Dictionary<string, string>
            {
                { "en", "Cancel" },
                { "es", "Cancelar" },
                { "fr", "Annuler" },
                { "de", "Abbrechen" }
            }, null)
        };

        foreach (var (keyName, translations, comment) in sampleData)
        {
            var resourceKey = new ResourceKey
            {
                ProjectId = project.Id,
                KeyName = keyName,
                Comment = comment,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.ResourceKeys.Add(resourceKey);
            await _db.SaveChangesAsync();

            foreach (var (lang, value) in translations)
            {
                var translation = new Shared.Entities.Translation
                {
                    ResourceKeyId = resourceKey.Id,
                    LanguageCode = lang,
                    Value = value,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _db.Translations.Add(translation);
            }
        }

        await _db.SaveChangesAsync();
    }
}

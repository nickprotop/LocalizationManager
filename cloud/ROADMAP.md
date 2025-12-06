# LRM Cloud - SaaS Roadmap

## Overview

Git-native localization management platform. Users connect repos, edit translations in web UI, sync back via PRs or CLI.

**Hosting:** DigitalOcean (manual Linux management, minimal cost)
**Sync:** Both GitHub App + CLI sync in parallel
**License:** Open source (MIT)

---

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    DigitalOcean                         │
│  ┌─────────────────────────────────────────────────┐   │
│  │              Nginx (reverse proxy)               │   │
│  │              + Let's Encrypt SSL                 │   │
│  └─────────────────────────────────────────────────┘   │
│                         │                               │
│  ┌─────────────────────────────────────────────────┐   │
│  │           ASP.NET Core API (Docker)              │   │
│  │  • Auth (GitHub OAuth + API keys)                │   │
│  │  • Project management                            │   │
│  │  • Translation API                               │   │
│  │  • GitHub webhook handler                        │   │
│  └─────────────────────────────────────────────────┘   │
│                         │                               │
│  ┌──────────────┐  ┌──────────────┐  ┌─────────────┐   │
│  │  PostgreSQL  │  │    Redis     │  │ File Store  │   │
│  │  (users,     │  │  (sessions,  │  │ (projects,  │   │
│  │   projects)  │  │   cache)     │  │  temp files)│   │
│  └──────────────┘  └──────────────┘  └─────────────┘   │
└─────────────────────────────────────────────────────────┘
```

---

## Infrastructure (DigitalOcean)

### Minimal Setup (~$20-30/mo)
- **1x Droplet** $12/mo (2GB RAM, 1 vCPU) - API + Nginx
- **Managed PostgreSQL** $15/mo (or self-hosted on same droplet)
- **Spaces** (S3-compatible) for file storage if needed

### Growth Setup (~$50-70/mo)
- **1x Droplet** $24/mo (4GB RAM, 2 vCPU)
- **Managed PostgreSQL** $15/mo
- **Managed Redis** $15/mo (or self-hosted)
- **DO Spaces** $5/mo

### Stack
- Ubuntu 24.04 LTS
- Docker + Docker Compose
- Nginx reverse proxy
- Certbot for SSL
- systemd for service management

---

## Database Schema (Complete)

```sql
-- =====================
-- USERS & AUTH
-- =====================

CREATE TABLE users (
    id SERIAL PRIMARY KEY,

    -- Authentication (multiple methods)
    auth_type VARCHAR(50) NOT NULL,        -- 'email', 'github', 'google'
    email VARCHAR(255) UNIQUE,
    email_verified BOOLEAN DEFAULT false,
    password_hash VARCHAR(255),            -- bcrypt hash (NULL for OAuth users)

    -- GitHub OAuth (optional)
    github_id BIGINT UNIQUE,
    github_access_token_encrypted TEXT,
    github_refresh_token_encrypted TEXT,
    github_token_expires_at TIMESTAMP,

    -- Profile
    username VARCHAR(255) NOT NULL,
    display_name VARCHAR(255),
    avatar_url TEXT,

    -- Subscription
    plan VARCHAR(50) DEFAULT 'free',       -- free, pro, team, enterprise
    stripe_customer_id VARCHAR(255),
    stripe_subscription_id VARCHAR(255),
    translation_chars_used INT DEFAULT 0,
    translation_chars_limit INT DEFAULT 10000,
    translation_chars_reset_at TIMESTAMP,
    projects_limit INT DEFAULT 1,

    -- Security
    password_reset_token VARCHAR(255),
    password_reset_expires TIMESTAMP,
    email_verification_token VARCHAR(255),
    last_login_at TIMESTAMP,
    failed_login_attempts INT DEFAULT 0,
    locked_until TIMESTAMP,

    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW()
);

CREATE INDEX idx_users_email ON users(email);
CREATE INDEX idx_users_github_id ON users(github_id);
CREATE INDEX idx_users_stripe_customer ON users(stripe_customer_id);

-- =====================
-- PROJECTS
-- =====================

CREATE TABLE projects (
    id SERIAL PRIMARY KEY,
    user_id INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    name VARCHAR(255) NOT NULL,
    description TEXT,

    -- GitHub integration (NULL if CLI-only)
    github_repo VARCHAR(255),              -- owner/repo
    github_installation_id BIGINT,
    github_default_branch VARCHAR(100) DEFAULT 'main',
    github_webhook_secret VARCHAR(255),

    -- Localization settings
    localization_path VARCHAR(500) DEFAULT '.',  -- relative path in repo
    format VARCHAR(50) NOT NULL,           -- resx, json, i18next
    default_language VARCHAR(10) DEFAULT 'en',

    -- Sync settings
    sync_mode VARCHAR(50) DEFAULT 'manual', -- github_app, cli, manual
    auto_translate BOOLEAN DEFAULT false,
    auto_create_pr BOOLEAN DEFAULT true,

    -- State
    last_synced_at TIMESTAMP,
    last_synced_commit VARCHAR(40),
    sync_status VARCHAR(50) DEFAULT 'pending', -- pending, syncing, synced, error
    sync_error TEXT,

    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW(),

    UNIQUE(user_id, name)
);

CREATE INDEX idx_projects_user ON projects(user_id);
CREATE INDEX idx_projects_github_repo ON projects(github_repo);
CREATE INDEX idx_projects_installation ON projects(github_installation_id);

-- =====================
-- PROJECT LANGUAGES
-- =====================

CREATE TABLE project_languages (
    id SERIAL PRIMARY KEY,
    project_id INT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
    language_code VARCHAR(10) NOT NULL,    -- en, fr, de, etc.
    is_default BOOLEAN DEFAULT false,
    total_keys INT DEFAULT 0,
    translated_keys INT DEFAULT 0,
    created_at TIMESTAMP DEFAULT NOW(),

    UNIQUE(project_id, language_code)
);

CREATE INDEX idx_project_languages_project ON project_languages(project_id);

-- =====================
-- RESOURCE KEYS (cached from files)
-- =====================

CREATE TABLE resource_keys (
    id SERIAL PRIMARY KEY,
    project_id INT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
    key_name VARCHAR(500) NOT NULL,
    key_path VARCHAR(500),                 -- for nested keys: "navigation.home"
    is_plural BOOLEAN DEFAULT false,
    comment TEXT,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW(),

    UNIQUE(project_id, key_name)
);

CREATE INDEX idx_resource_keys_project ON resource_keys(project_id);
CREATE INDEX idx_resource_keys_name ON resource_keys(key_name);

-- =====================
-- TRANSLATIONS
-- =====================

CREATE TABLE translations (
    id SERIAL PRIMARY KEY,
    resource_key_id INT NOT NULL REFERENCES resource_keys(id) ON DELETE CASCADE,
    language_code VARCHAR(10) NOT NULL,
    value TEXT,
    plural_form VARCHAR(20),               -- one, other, few, many, etc.
    status VARCHAR(50) DEFAULT 'pending',  -- pending, translated, reviewed, approved
    translated_by VARCHAR(50),             -- user, machine:google, machine:deepl
    reviewed_by INT REFERENCES users(id),
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW(),

    UNIQUE(resource_key_id, language_code, plural_form)
);

CREATE INDEX idx_translations_key ON translations(resource_key_id);
CREATE INDEX idx_translations_language ON translations(language_code);
CREATE INDEX idx_translations_status ON translations(status);

-- =====================
-- API KEYS (for CLI sync)
-- =====================

CREATE TABLE api_keys (
    id SERIAL PRIMARY KEY,
    user_id INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    project_id INT REFERENCES projects(id) ON DELETE CASCADE,  -- NULL = all projects
    key_prefix VARCHAR(10) NOT NULL,       -- first 8 chars for identification
    key_hash VARCHAR(255) NOT NULL,        -- bcrypt hash
    name VARCHAR(255),
    scopes VARCHAR(255) DEFAULT 'read,write', -- read, write, translate
    last_used_at TIMESTAMP,
    expires_at TIMESTAMP,
    created_at TIMESTAMP DEFAULT NOW()
);

CREATE INDEX idx_api_keys_user ON api_keys(user_id);
CREATE INDEX idx_api_keys_prefix ON api_keys(key_prefix);

-- =====================
-- GITHUB APP INSTALLATIONS
-- =====================

CREATE TABLE github_installations (
    id SERIAL PRIMARY KEY,
    installation_id BIGINT UNIQUE NOT NULL,
    account_login VARCHAR(255) NOT NULL,   -- user or org name
    account_type VARCHAR(50),              -- User or Organization
    access_token_encrypted TEXT,
    token_expires_at TIMESTAMP,
    repositories JSONB,                    -- list of repos with access
    suspended_at TIMESTAMP,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW()
);

CREATE INDEX idx_github_installations_id ON github_installations(installation_id);

-- =====================
-- SYNC HISTORY
-- =====================

CREATE TABLE sync_history (
    id SERIAL PRIMARY KEY,
    project_id INT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
    sync_type VARCHAR(50) NOT NULL,        -- push, pull, github_webhook
    direction VARCHAR(20),                 -- inbound, outbound
    commit_sha VARCHAR(40),
    pr_number INT,
    pr_url TEXT,
    keys_added INT DEFAULT 0,
    keys_updated INT DEFAULT 0,
    keys_deleted INT DEFAULT 0,
    status VARCHAR(50),                    -- success, failed, partial
    error_message TEXT,
    created_at TIMESTAMP DEFAULT NOW()
);

CREATE INDEX idx_sync_history_project ON sync_history(project_id);
CREATE INDEX idx_sync_history_created ON sync_history(created_at);

-- =====================
-- TRANSLATION USAGE
-- =====================

CREATE TABLE translation_usage (
    id SERIAL PRIMARY KEY,
    user_id INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    project_id INT REFERENCES projects(id) ON DELETE SET NULL,
    provider VARCHAR(50) NOT NULL,         -- google, deepl, openai, etc.
    source_language VARCHAR(10),
    target_language VARCHAR(10),
    chars_translated INT NOT NULL,
    cost_estimate DECIMAL(10, 4),          -- estimated cost in USD
    created_at TIMESTAMP DEFAULT NOW()
);

CREATE INDEX idx_translation_usage_user ON translation_usage(user_id);
CREATE INDEX idx_translation_usage_created ON translation_usage(created_at);

-- =====================
-- AUDIT LOG
-- =====================

CREATE TABLE audit_log (
    id SERIAL PRIMARY KEY,
    user_id INT REFERENCES users(id),
    project_id INT REFERENCES projects(id),
    action VARCHAR(100) NOT NULL,          -- key.created, key.translated, project.synced
    entity_type VARCHAR(50),
    entity_id INT,
    old_value JSONB,
    new_value JSONB,
    ip_address INET,
    user_agent TEXT,
    created_at TIMESTAMP DEFAULT NOW()
);

CREATE INDEX idx_audit_log_user ON audit_log(user_id);
CREATE INDEX idx_audit_log_project ON audit_log(project_id);
CREATE INDEX idx_audit_log_created ON audit_log(created_at);
```

---

## API Endpoints

### Auth
```
GET  /auth/github              # Redirect to GitHub OAuth
GET  /auth/github/callback     # OAuth callback
POST /auth/logout
GET  /api/me                   # Current user info
```

### Projects
```
GET    /api/projects                    # List user's projects
POST   /api/projects                    # Create project
GET    /api/projects/:id                # Get project details
DELETE /api/projects/:id                # Delete project
POST   /api/projects/:id/sync           # Trigger sync from GitHub
```

### Resources (per project)
```
GET    /api/projects/:id/keys           # List all keys
GET    /api/projects/:id/keys/:key      # Get key details
PUT    /api/projects/:id/keys/:key      # Update key
POST   /api/projects/:id/keys           # Add key
DELETE /api/projects/:id/keys/:key      # Delete key
POST   /api/projects/:id/translate      # Translate missing keys
GET    /api/projects/:id/stats          # Translation stats
GET    /api/projects/:id/validate       # Validate resources
```

### CLI Sync
```
POST /api/sync/push           # Upload local files (API key auth)
GET  /api/sync/pull           # Download current state
POST /api/sync/commit         # Create PR/commit with changes
```

### GitHub Webhooks
```
POST /webhooks/github         # Handle push events, PR events
```

---

## CLI Commands (new)

```bash
# Link project to cloud
lrm cloud login                    # GitHub OAuth via browser
lrm cloud init                     # Create project, get API key
lrm cloud link <project-id>        # Link existing local folder

# Sync
lrm push                           # Upload local changes
lrm pull                           # Download cloud changes
lrm sync                           # Bidirectional sync

# Cloud operations
lrm cloud translate --to fr,de     # Translate via cloud (uses quota)
lrm cloud status                   # Show sync status
```

---

## Authentication (Multiple Methods)

### Supported Auth Methods
- **Email/Password** - Standalone registration (no GitHub required)
- **GitHub OAuth** - One-click login for developers
- **Google OAuth** - (Future) Broader audience
- **Account Linking** - Connect GitHub to existing email account

### 1. Email/Password Registration

**API Endpoints:**
```
POST /auth/register          # Create account
POST /auth/login             # Email/password login
POST /auth/logout            # End session
POST /auth/forgot-password   # Request reset email
POST /auth/reset-password    # Set new password
POST /auth/verify-email      # Confirm email address
```

**Flow:**
```
User fills registration form (email, password, username)
    ↓
POST /auth/register
    → Validate email unique, password strength
    → Hash password (bcrypt, cost 12)
    → Create user with email_verified = false
    → Generate verification token
    → Send verification email
    ↓
User clicks link in email
    ↓
POST /auth/verify-email?token=xxx
    → Set email_verified = true
    ↓
User can now login
```

**Code (AuthController.cs):**
```csharp
[HttpPost("register")]
public async Task<IActionResult> Register([FromBody] RegisterRequest request)
{
    // Validate
    if (await _db.Users.AnyAsync(u => u.Email == request.Email))
        return BadRequest("Email already registered");

    if (!IsValidPassword(request.Password))
        return BadRequest("Password must be 8+ chars with number and symbol");

    // Create user
    var user = new User
    {
        AuthType = "email",
        Email = request.Email,
        Username = request.Username ?? request.Email.Split('@')[0],
        PasswordHash = BCrypt.HashPassword(request.Password, 12),
        EmailVerified = false,
        EmailVerificationToken = GenerateToken()
    };

    _db.Users.Add(user);
    await _db.SaveChangesAsync();

    // Send verification email
    await _emailService.SendVerificationEmail(user.Email, user.EmailVerificationToken);

    return Ok(new { message = "Please check your email to verify your account" });
}

[HttpPost("login")]
public async Task<IActionResult> Login([FromBody] LoginRequest request)
{
    var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

    if (user == null || !BCrypt.Verify(request.Password, user.PasswordHash))
    {
        // Track failed attempts
        if (user != null)
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= 5)
                user.LockedUntil = DateTime.UtcNow.AddMinutes(15);
            await _db.SaveChangesAsync();
        }
        return Unauthorized("Invalid email or password");
    }

    if (user.LockedUntil > DateTime.UtcNow)
        return Unauthorized("Account locked. Try again later.");

    if (!user.EmailVerified)
        return Unauthorized("Please verify your email first");

    // Reset failed attempts, update last login
    user.FailedLoginAttempts = 0;
    user.LastLoginAt = DateTime.UtcNow;
    await _db.SaveChangesAsync();

    // Set auth cookie
    await HttpContext.SignInAsync(user.ToClaims());

    return Ok(new { user = user.ToDto() });
}
```

### 2. GitHub OAuth (Optional)

**Setup:**
- [ ] Create OAuth App at github.com/settings/developers
- [ ] Set callback URL: `https://lrm.cloud/auth/github/callback`
- [ ] Store Client ID and Client Secret in env vars

**Flow:**
```
User clicks "Login with GitHub"
    ↓
Redirect to: github.com/login/oauth/authorize
    ?client_id=xxx
    &redirect_uri=https://lrm.cloud/auth/github/callback
    &scope=user:email
    ↓
GitHub redirects back with ?code=xxx
    ↓
POST github.com/login/oauth/access_token
    → Get access_token
    ↓
GET api.github.com/user
    → Get user info (id, login, email, avatar)
    ↓
Create/update user in DB, set session cookie
```

**Code (AuthController.cs):**
```csharp
[HttpGet("github")]
public IActionResult GitHubLogin([FromQuery] string? returnUrl)
{
    var state = GenerateState(returnUrl);  // CSRF protection
    var url = $"https://github.com/login/oauth/authorize" +
              $"?client_id={_config.GitHubClientId}" +
              $"&redirect_uri={_config.GitHubCallbackUrl}" +
              $"&scope=user:email" +
              $"&state={state}";
    return Redirect(url);
}

[HttpGet("github/callback")]
public async Task<IActionResult> GitHubCallback(string code, string state)
{
    // Verify state (CSRF protection)
    var returnUrl = ValidateState(state);

    // Exchange code for token
    var token = await _github.ExchangeCodeForToken(code);
    var ghUser = await _github.GetUser(token);

    // Check if GitHub account is linked to existing user
    var existingUser = await _db.Users.FirstOrDefaultAsync(u => u.GitHubId == ghUser.Id);

    if (existingUser != null)
    {
        // Update token, login
        existingUser.GitHubAccessTokenEncrypted = Encrypt(token);
        await _db.SaveChangesAsync();
        await HttpContext.SignInAsync(existingUser.ToClaims());
    }
    else
    {
        // Check if email already registered
        var emailUser = await _db.Users.FirstOrDefaultAsync(u => u.Email == ghUser.Email);
        if (emailUser != null)
        {
            // Link GitHub to existing account
            emailUser.GitHubId = ghUser.Id;
            emailUser.GitHubAccessTokenEncrypted = Encrypt(token);
            await _db.SaveChangesAsync();
            await HttpContext.SignInAsync(emailUser.ToClaims());
        }
        else
        {
            // Create new user
            var user = new User
            {
                AuthType = "github",
                GitHubId = ghUser.Id,
                Email = ghUser.Email,
                EmailVerified = true,  // GitHub verified
                Username = ghUser.Login,
                AvatarUrl = ghUser.AvatarUrl,
                GitHubAccessTokenEncrypted = Encrypt(token)
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            await HttpContext.SignInAsync(user.ToClaims());
        }
    }

    return Redirect(returnUrl ?? "/dashboard");
}
```

### 3. Account Linking

Users can link GitHub to their email account for repo access:

```csharp
[HttpPost("link-github")]
[Authorize]
public async Task<IActionResult> LinkGitHub()
{
    // Redirect to GitHub OAuth with "link" state
    var state = GenerateState("link");
    return Redirect($"https://github.com/login/oauth/authorize?...&state={state}");
}

// In callback, check if state indicates linking:
if (stateData.Action == "link")
{
    var currentUser = await GetCurrentUser();
    currentUser.GitHubId = ghUser.Id;
    currentUser.GitHubAccessTokenEncrypted = Encrypt(token);
    await _db.SaveChangesAsync();
    return Redirect("/account/settings?linked=github");
}
```

---

## GitHub App (Repo Access)

**Setup:**
- [ ] Create GitHub App at github.com/settings/apps/new
- [ ] App name: "LRM Cloud"
- [ ] Homepage: https://lrm.cloud
- [ ] Webhook URL: https://lrm.cloud/webhooks/github
- [ ] Webhook secret: Generate and store securely
- [ ] Generate private key (.pem file)

**Permissions:**
| Permission | Access | Reason |
|------------|--------|--------|
| Contents | Read & Write | Read localization files, create commits |
| Pull requests | Read & Write | Create PRs with translation changes |
| Metadata | Read | Get repo info |

**Webhook Events:**
| Event | Handler |
|-------|---------|
| `installation` | Save installation ID, list of repos |
| `installation_repositories` | Update repo list |
| `push` | Sync localization files on push |
| `pull_request` | (Future) Add translation status check |

**Installation Flow:**
```
User clicks "Connect GitHub Repo"
    ↓
Redirect to: github.com/apps/lrm-cloud/installations/new
    ↓
User selects repos to grant access
    ↓
GitHub sends `installation` webhook
    ↓
We store installation_id + repos in github_installations table
    ↓
Redirect to: /dashboard with ?installation_id=xxx
    ↓
User creates project, selects from available repos
```

**Webhook Handler (WebhooksController.cs):**
```csharp
[HttpPost("github")]
public async Task<IActionResult> GitHubWebhook()
{
    // Verify signature
    var signature = Request.Headers["X-Hub-Signature-256"];
    var payload = await ReadBodyAsync();
    if (!VerifySignature(payload, signature))
        return Unauthorized();

    var eventType = Request.Headers["X-GitHub-Event"];
    var delivery = Request.Headers["X-GitHub-Delivery"];

    switch (eventType)
    {
        case "installation":
            await HandleInstallation(payload);
            break;
        case "push":
            await HandlePush(payload);
            break;
    }

    return Ok();
}

private async Task HandlePush(string payload)
{
    var push = JsonSerializer.Deserialize<PushEvent>(payload);

    // Find project for this repo
    var project = await _db.Projects
        .FirstOrDefaultAsync(p => p.GitHubRepo == push.Repository.FullName);

    if (project == null) return;

    // Check if localization files changed
    var locFiles = push.Commits
        .SelectMany(c => c.Added.Concat(c.Modified))
        .Where(f => IsLocalizationFile(f, project.LocalizationPath))
        .ToList();

    if (locFiles.Any())
    {
        await _syncService.SyncFromGitHub(project, push.After);
    }
}
```

### 3. GitHub API Operations

**Get Installation Token:**
```csharp
public async Task<string> GetInstallationToken(long installationId)
{
    // Create JWT from App private key
    var jwt = CreateAppJwt();

    // Exchange for installation token
    var response = await _http.PostAsync(
        $"https://api.github.com/app/installations/{installationId}/access_tokens",
        null,
        new AuthenticationHeaderValue("Bearer", jwt)
    );

    var token = JsonSerializer.Deserialize<InstallationToken>(response);
    return token.Token;  // Valid for 1 hour
}
```

**Read Localization Files:**
```csharp
public async Task<List<LocalizationFile>> GetLocalizationFiles(
    string repo, string path, string token)
{
    // Get directory contents
    var contents = await _http.GetAsync<List<GitHubContent>>(
        $"https://api.github.com/repos/{repo}/contents/{path}",
        new AuthenticationHeaderValue("token", token)
    );

    var files = new List<LocalizationFile>();
    foreach (var item in contents.Where(c => c.Type == "file"))
    {
        if (IsLocalizationFile(item.Name))
        {
            var content = await GetFileContent(repo, item.Path, token);
            files.Add(new LocalizationFile(item.Name, content));
        }
    }
    return files;
}
```

**Create PR with Changes:**
```csharp
public async Task<string> CreateTranslationPR(
    Project project,
    Dictionary<string, string> fileChanges,
    string token)
{
    var repo = project.GitHubRepo;
    var baseBranch = project.GitHubDefaultBranch;
    var newBranch = $"lrm/translations-{DateTime.UtcNow:yyyyMMdd-HHmmss}";

    // 1. Get base branch SHA
    var baseRef = await GetRef(repo, baseBranch, token);

    // 2. Create new branch
    await CreateRef(repo, newBranch, baseRef.Sha, token);

    // 3. Create commits for each file
    foreach (var (path, content) in fileChanges)
    {
        await CreateOrUpdateFile(repo, path, content, newBranch, token);
    }

    // 4. Create PR
    var pr = await CreatePullRequest(repo, new
    {
        title = "Update translations",
        body = "Translations updated via LRM Cloud",
        head = newBranch,
        @base = baseBranch
    }, token);

    return pr.HtmlUrl;
}

---

## Blazor Frontend (Detailed)

### Tech Stack
- **Blazor Server** (real-time updates, C# reuse)
- **MudBlazor** (Material Design component library)
- **SignalR** (real-time sync notifications)

### Project Structure
```
LrmCloud.Web/
├── Program.cs
├── App.razor
├── _Imports.razor
├── wwwroot/
│   ├── css/
│   └── images/
├── Components/
│   ├── Layout/
│   │   ├── MainLayout.razor
│   │   ├── NavMenu.razor
│   │   └── LoginDisplay.razor
│   ├── Shared/
│   │   ├── LoadingSpinner.razor
│   │   ├── ErrorAlert.razor
│   │   └── ConfirmDialog.razor
│   └── Translation/
│       ├── TranslationEditor.razor
│       ├── KeyList.razor
│       ├── LanguageColumn.razor
│       └── TranslationCell.razor
├── Pages/
│   ├── Index.razor              # Landing page
│   ├── Login.razor              # GitHub OAuth
│   ├── Dashboard.razor          # Project list
│   ├── Projects/
│   │   ├── ProjectDetail.razor
│   │   ├── ProjectCreate.razor
│   │   ├── ProjectSettings.razor
│   │   └── ProjectEditor.razor
│   └── Account/
│       ├── AccountSettings.razor
│       ├── ApiKeys.razor
│       └── Billing.razor
└── Services/
    ├── AuthStateProvider.cs
    ├── ProjectService.cs
    └── TranslationService.cs
```

### Page Layouts

#### Dashboard.razor
```razor
@page "/dashboard"
@attribute [Authorize]

<MudContainer MaxWidth="MaxWidth.Large" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">My Projects</MudText>

    <MudGrid>
        <!-- Stats Cards -->
        <MudItem xs="12" sm="4">
            <MudCard>
                <MudCardContent>
                    <MudText Typo="Typo.h3">@_stats.TotalProjects</MudText>
                    <MudText Typo="Typo.body2">Projects</MudText>
                </MudCardContent>
            </MudCard>
        </MudItem>
        <MudItem xs="12" sm="4">
            <MudCard>
                <MudCardContent>
                    <MudText Typo="Typo.h3">@_stats.TranslationProgress%</MudText>
                    <MudText Typo="Typo.body2">Translated</MudText>
                </MudCardContent>
            </MudCard>
        </MudItem>
        <MudItem xs="12" sm="4">
            <MudCard>
                <MudCardContent>
                    <MudText Typo="Typo.h3">@_stats.CharsUsed / @_stats.CharsLimit</MudText>
                    <MudText Typo="Typo.body2">Translation Chars</MudText>
                </MudCardContent>
            </MudCard>
        </MudItem>

        <!-- Project List -->
        <MudItem xs="12">
            <MudTable Items="@_projects" Hover="true" Striped="true">
                <HeaderContent>
                    <MudTh>Name</MudTh>
                    <MudTh>Format</MudTh>
                    <MudTh>Languages</MudTh>
                    <MudTh>Progress</MudTh>
                    <MudTh>Last Sync</MudTh>
                    <MudTh></MudTh>
                </HeaderContent>
                <RowTemplate>
                    <MudTd>@context.Name</MudTd>
                    <MudTd><MudChip Size="Size.Small">@context.Format</MudChip></MudTd>
                    <MudTd>@context.LanguageCount languages</MudTd>
                    <MudTd>
                        <MudProgressLinear Value="@context.TranslationProgress" Color="Color.Primary" />
                    </MudTd>
                    <MudTd>@context.LastSyncedAt?.Humanize()</MudTd>
                    <MudTd>
                        <MudIconButton Icon="@Icons.Material.Filled.Edit"
                                       Href="@($"/projects/{context.Id}/edit")" />
                    </MudTd>
                </RowTemplate>
            </MudTable>
        </MudItem>

        <!-- Create Project Button -->
        <MudItem xs="12">
            <MudButton Variant="Variant.Filled" Color="Color.Primary"
                       Href="/projects/create">
                Create Project
            </MudButton>
        </MudItem>
    </MudGrid>
</MudContainer>
```

#### ProjectEditor.razor (Translation Editor)
```razor
@page "/projects/{ProjectId:int}/edit"
@attribute [Authorize]

<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="mt-4">
    <!-- Toolbar -->
    <MudToolBar Dense="true" Class="mb-4">
        <MudTextField @bind-Value="_searchText" Placeholder="Search keys..."
                      Adornment="Adornment.Start" AdornmentIcon="@Icons.Material.Filled.Search"
                      Immediate="true" />
        <MudSpacer />
        <MudSelect T="string" Label="Filter" @bind-Value="_statusFilter">
            <MudSelectItem Value="@("all")">All</MudSelectItem>
            <MudSelectItem Value="@("missing")">Missing</MudSelectItem>
            <MudSelectItem Value="@("translated")">Translated</MudSelectItem>
        </MudSelect>
        <MudButton Variant="Variant.Filled" Color="Color.Primary"
                   OnClick="TranslateMissing" Disabled="@_isTranslating">
            @if (_isTranslating)
            {
                <MudProgressCircular Size="Size.Small" Indeterminate="true" />
            }
            Translate Missing
        </MudButton>
        <MudButton Variant="Variant.Filled" Color="Color.Success"
                   OnClick="CreatePR" Disabled="@(!_hasChanges)">
            Create PR
        </MudButton>
    </MudToolBar>

    <!-- Translation Grid -->
    <MudTable Items="@_filteredKeys" Virtualize="true" Height="calc(100vh - 200px)"
              FixedHeader="true" Dense="true" Hover="true">
        <HeaderContent>
            <MudTh Style="width: 250px">Key</MudTh>
            @foreach (var lang in _languages)
            {
                <MudTh Style="min-width: 200px">
                    <MudStack Row="true" AlignItems="AlignItems.Center">
                        <MudText>@lang.Code</MudText>
                        @if (lang.IsDefault)
                        {
                            <MudChip Size="Size.Small" Color="Color.Info">Default</MudChip>
                        }
                    </MudStack>
                </MudTh>
            }
        </HeaderContent>
        <RowTemplate>
            <MudTd>
                <MudText Typo="Typo.body2" Style="font-family: monospace">
                    @context.KeyName
                </MudText>
            </MudTd>
            @foreach (var lang in _languages)
            {
                <MudTd>
                    <TranslationCell Key="@context" Language="@lang.Code"
                                     OnChanged="@(() => _hasChanges = true)" />
                </MudTd>
            }
        </RowTemplate>
    </MudTable>
</MudContainer>

@code {
    [Parameter] public int ProjectId { get; set; }

    private List<ResourceKey> _keys = new();
    private List<ResourceKey> _filteredKeys => FilterKeys();
    private List<Language> _languages = new();
    private string _searchText = "";
    private string _statusFilter = "all";
    private bool _hasChanges = false;
    private bool _isTranslating = false;

    protected override async Task OnInitializedAsync()
    {
        var project = await ProjectService.GetAsync(ProjectId);
        _keys = await ProjectService.GetKeysAsync(ProjectId);
        _languages = project.Languages;
    }

    private async Task TranslateMissing()
    {
        _isTranslating = true;
        await ProjectService.TranslateMissingAsync(ProjectId);
        _keys = await ProjectService.GetKeysAsync(ProjectId);
        _isTranslating = false;
    }

    private async Task CreatePR()
    {
        var prUrl = await ProjectService.CreatePRAsync(ProjectId);
        NavigationManager.NavigateTo(prUrl);
    }
}
```

### Components

#### TranslationCell.razor
```razor
<div class="translation-cell @GetStatusClass()">
    @if (_isEditing)
    {
        <MudTextField @bind-Value="_value" Lines="2" Immediate="true"
                      OnBlur="SaveChanges" Variant="Variant.Outlined" />
    }
    else
    {
        <div @onclick="StartEditing" class="cell-content">
            @if (string.IsNullOrEmpty(_value))
            {
                <MudText Typo="Typo.body2" Color="Color.Warning">
                    <em>Missing</em>
                </MudText>
            }
            else
            {
                <MudText Typo="Typo.body2">@_value</MudText>
            }
        </div>
    }
</div>

@code {
    [Parameter] public ResourceKey Key { get; set; }
    [Parameter] public string Language { get; set; }
    [Parameter] public EventCallback OnChanged { get; set; }

    private string _value;
    private bool _isEditing = false;

    protected override void OnParametersSet()
    {
        _value = Key.Translations.GetValueOrDefault(Language)?.Value;
    }

    private void StartEditing() => _isEditing = true;

    private async Task SaveChanges()
    {
        _isEditing = false;
        if (_value != Key.Translations.GetValueOrDefault(Language)?.Value)
        {
            await TranslationService.UpdateAsync(Key.Id, Language, _value);
            await OnChanged.InvokeAsync();
        }
    }

    private string GetStatusClass() => string.IsNullOrEmpty(_value) ? "missing" : "translated";
}
```

### Authentication Flow

```csharp
// AuthStateProvider.cs
public class LrmAuthStateProvider : AuthenticationStateProvider
{
    private readonly IHttpContextAccessor _httpContext;
    private readonly IUserService _userService;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var user = _httpContext.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated == true)
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var dbUser = await _userService.GetByIdAsync(int.Parse(userId));

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, dbUser.Id.ToString()),
                new(ClaimTypes.Name, dbUser.Username),
                new("plan", dbUser.Plan),
                new("avatar", dbUser.AvatarUrl ?? "")
            };

            var identity = new ClaimsIdentity(claims, "GitHub");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }

        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
    }
}
```

### Real-time Updates (SignalR)

```csharp
// SyncHub.cs
public class SyncHub : Hub
{
    public async Task JoinProject(int projectId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"project-{projectId}");
    }

    public async Task LeaveProject(int projectId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"project-{projectId}");
    }
}

// When sync completes, notify connected clients
public class SyncService
{
    private readonly IHubContext<SyncHub> _hubContext;

    public async Task NotifySyncComplete(int projectId, SyncResult result)
    {
        await _hubContext.Clients.Group($"project-{projectId}")
            .SendAsync("SyncComplete", result);
    }
}

---

## Pricing

| Tier | Price | Projects | Translation | Features |
|------|-------|----------|-------------|----------|
| Free | $0 | 1 | 10K chars/mo | CLI sync only |
| Pro | $9/mo | 5 | 100K chars/mo | GitHub App, web editor |
| Team | $29/mo | 20 | 500K chars/mo | Multiple users, priority |

### Translation Costs (our cost)
- Google: ~$20/million chars
- DeepL: ~$25/million chars
- Free providers: $0

At $9/mo for 100K chars, margin is healthy even with paid providers.

---

## Implementation Phases (Detailed Checklist)

### Phase 0: Project Setup
- [ ] Create `cloud/` folder structure
- [ ] Initialize `LrmCloud.sln` solution
- [ ] Create `LrmCloud.Api` project (ASP.NET Core Web API)
- [ ] Create `LrmCloud.Web` project (Blazor Server)
- [ ] Create `LrmCloud.Core` project (shared models)
- [ ] Set up Docker Compose (dev environment)
- [ ] Configure GitHub Actions for cloud CI/CD

### Phase 1: Infrastructure & Auth
- [ ] Set up DigitalOcean droplet (Ubuntu 24.04)
- [ ] Install Docker, Docker Compose
- [ ] Configure Nginx reverse proxy
- [ ] Set up Let's Encrypt SSL (certbot)
- [ ] Deploy PostgreSQL (managed or Docker)
- [ ] Create database schema (EF Core migrations)
- [ ] **Email/Password Auth:**
  - [ ] Registration endpoint
  - [ ] Login endpoint
  - [ ] Password hashing (bcrypt)
  - [ ] Email verification flow
  - [ ] Password reset flow
- [ ] **GitHub OAuth:**
  - [ ] Create GitHub OAuth App
  - [ ] OAuth login flow
  - [ ] Account linking (email ↔ GitHub)
- [ ] Session management (cookies)
- [ ] API key generation for CLI

### Phase 2: Core API
- [ ] **User endpoints:**
  - [ ] `GET /api/me` - current user
  - [ ] `PUT /api/me` - update profile
  - [ ] `GET /api/me/usage` - translation usage
- [ ] **Project endpoints:**
  - [ ] `GET /api/projects` - list projects
  - [ ] `POST /api/projects` - create project
  - [ ] `GET /api/projects/:id` - project details
  - [ ] `PUT /api/projects/:id` - update project
  - [ ] `DELETE /api/projects/:id` - delete project
- [ ] **Resource endpoints:**
  - [ ] `GET /api/projects/:id/keys` - list keys
  - [ ] `GET /api/projects/:id/keys/:key` - key details
  - [ ] `PUT /api/projects/:id/keys/:key` - update key
  - [ ] `POST /api/projects/:id/keys` - add key
  - [ ] `DELETE /api/projects/:id/keys/:key` - delete key
  - [ ] `POST /api/projects/:id/translate` - translate missing
  - [ ] `GET /api/projects/:id/stats` - statistics
  - [ ] `GET /api/projects/:id/validate` - validation

### Phase 3: CLI Sync
- [ ] **CLI commands:**
  - [ ] `lrm cloud login` - browser OAuth flow
  - [ ] `lrm cloud logout`
  - [ ] `lrm cloud init` - create project
  - [ ] `lrm cloud link` - link to existing project
  - [ ] `lrm push` - upload local files
  - [ ] `lrm pull` - download cloud files
  - [ ] `lrm cloud status` - sync status
- [ ] **Sync API:**
  - [ ] `POST /api/sync/push` - receive files
  - [ ] `GET /api/sync/pull` - send files
  - [ ] Conflict detection
  - [ ] Merge strategy

### Phase 4: GitHub App
- [ ] Register GitHub App
- [ ] Store private key securely
- [ ] **Webhook handler:**
  - [ ] Signature verification
  - [ ] `installation` event
  - [ ] `installation_repositories` event
  - [ ] `push` event
- [ ] **GitHub API operations:**
  - [ ] Get installation token
  - [ ] Read repository files
  - [ ] Create branch
  - [ ] Create/update file
  - [ ] Create pull request
- [ ] Auto-sync on push
- [ ] PR status checks (future)

### Phase 5: Blazor Frontend
- [ ] **Layout:**
  - [ ] Main layout with nav
  - [ ] Login/register pages
  - [ ] Auth state provider
- [ ] **Dashboard:**
  - [ ] Project list
  - [ ] Usage stats cards
  - [ ] Create project button
- [ ] **Project pages:**
  - [ ] Project detail view
  - [ ] Project settings
  - [ ] API key management
- [ ] **Translation editor:**
  - [ ] Key list with search
  - [ ] Multi-language columns
  - [ ] Inline editing
  - [ ] Translate missing button
  - [ ] Create PR button
- [ ] **Account pages:**
  - [ ] Profile settings
  - [ ] Link GitHub account
  - [ ] Billing/subscription

### Phase 6: Billing & Launch
- [ ] Stripe integration
- [ ] Subscription plans
- [ ] Usage metering
- [ ] Usage limits enforcement
- [ ] Landing page
- [ ] Documentation
- [ ] Email templates (transactional)
- [ ] ProductHunt submission prep

---

## Security Considerations

- GitHub tokens encrypted at rest (AES-256)
- API keys hashed (bcrypt)
- Rate limiting per user
- Input validation on all endpoints
- HTTPS only
- Regular backups

---

## Monitoring

- **Logs:** systemd journal + logrotate
- **Metrics:** Simple health endpoint + uptime monitoring (UptimeRobot free)
- **Alerts:** Email on service down
- **Later:** Grafana if needed

---

## Cost Projection

### Month 1-3 (MVP)
- Droplet: $12
- PostgreSQL: $15 (managed) or $0 (self-hosted)
- Domain: ~$1
- **Total:** $13-28/mo

### Month 4-6 (Growth)
- Larger droplet: $24
- Managed DB: $15
- Redis: $15
- **Total:** ~$55/mo

### Break-even
- At $9/mo Pro plan: 4-6 paying customers covers costs
- Target: 20 paying customers by month 6 = $180 MRR

---

## Project Structure

All SaaS in one isolated folder - maximum separation:

```
LocalizationManager/
├── LocalizationManager/           # CLI (existing, unchanged)
├── LocalizationManager.JsonLocalization/  # NuGet (existing, unchanged)
├── LocalizationManager.Tests/     # Tests (existing, unchanged)
├── LocalizationManager.sln        # Existing solution (unchanged)
│
└── cloud/                         # ALL SaaS CODE HERE - completely isolated
    ├── LrmCloud.sln               # Separate solution for cloud
    ├── src/
    │   ├── LrmCloud.Api/          # ASP.NET Core Web API
    │   │   ├── Controllers/
    │   │   ├── Services/
    │   │   ├── Data/              # EF Core, migrations
    │   │   └── Program.cs
    │   ├── LrmCloud.Web/          # Blazor frontend
    │   │   ├── Pages/
    │   │   └── Components/
    │   └── LrmCloud.Core/         # Shared cloud models/logic
    ├── tests/
    │   └── LrmCloud.Tests/
    ├── docker/
    │   ├── docker-compose.yml
    │   ├── docker-compose.prod.yml
    │   └── nginx/
    ├── deploy/
    │   ├── setup-server.sh
    │   └── deploy.sh
    └── README.md                  # Cloud-specific docs
```

### Separation Benefits
- CLI and Cloud have separate solutions
- Cloud can be deployed independently
- No changes to existing CLI codebase
- Cloud references CLI as NuGet or project reference (one-way dependency)

---

## Next Steps

1. Register domain (lrm.cloud? lrmcloud.io? getlrm.com?)
2. Set up DigitalOcean droplet
3. Create GitHub OAuth App
4. Extract LocalizationManager.Core from CLI
5. Create LocalizationManager.Cloud project
6. Start with Phase 1: basic auth and project management

# LRM Cloud - SaaS Roadmap

## Overview

Git-native localization management platform. Users connect repos, edit translations in web UI, sync back via PRs or CLI.

**Hosting:** DigitalOcean (manual Linux management, minimal cost)
**Sync:** Both GitHub App + CLI sync in parallel
**License:** Open source (MIT)

---

## Key Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| **Web UI** | Blazor WebAssembly | Stateless API, horizontal scaling, offline capability |
| **MVP Scope** | GitHub App + Web UI + CLI | Full vision from day one |
| **Teams** | Multi-user from start | Required for Team tier pricing |
| **Compliance** | GDPR-ready, SOC2 foundation | EU users, enterprise-ready architecture |
| **API Keys** | Hybrid (user's first, platform fallback) | Best UX with cost control |
| **Email** | Self-hosted sendmail + IMailService | Use existing infrastructure |
| **Database** | Self-hosted PostgreSQL | Cost savings, full control |
| **File Storage** | Self-hosted MinIO | S3-compatible, scalable, can migrate to AWS S3 later |
| **Conflict Resolution** | Optimistic locking + user prompt | Safe, user-controlled |
| **Configuration** | Single `config.json` (git-ignored) | No appsettings.json, all config in one place |
| **Infrastructure** | `setup.sh` + Docker Compose | Interactive or config-driven, isolated containers |

---

## Architecture

```
                         ┌────────────────────────────────────────┐
                         │          Client Browser                │
                         │  ┌──────────────────────────────────┐  │
                         │  │   Blazor WebAssembly (SPA)       │  │
                         │  │   - Translation Editor           │  │
                         │  │   - Project Dashboard            │  │
                         │  │   - Offline-capable (PWA)        │  │
                         │  └──────────────────────────────────┘  │
                         └───────────────────┬────────────────────┘
                                             │ REST API
┌────────────────────────────────────────────┼────────────────────────────────────┐
│                                            │                                    │
│  ┌─────────────────────────────────────────┴─────────────────────────────────┐  │
│  │                    Nginx (reverse proxy + SSL)                            │  │
│  │                    + Rate limiting + Security headers                     │  │
│  └─────────────────────────────────────────┬─────────────────────────────────┘  │
│                                            │                                    │
│  ┌─────────────────────────────────────────┴─────────────────────────────────┐  │
│  │                 ASP.NET Core Web API (Docker)                             │  │
│  │  ├── AuthController (Email/Password, GitHub OAuth)                        │  │
│  │  ├── ProjectsController (CRUD, team management)                           │  │
│  │  ├── ResourcesController (keys, translations)                             │  │
│  │  ├── SyncController (CLI push/pull)                                       │  │
│  │  ├── WebhooksController (GitHub events)                                   │  │
│  │  └── TranslationController (machine translation)                          │  │
│  └─────────────────────────────────────────┬─────────────────────────────────┘  │
│                                            │                                    │
│  ┌──────────────┐  ┌──────────────┐  ┌─────┴─────────┐  ┌──────────────────┐    │
│  │ PostgreSQL   │  │    Redis     │  │  File Store   │  │  Mail Server     │    │
│  │ (self-host)  │  │  (sessions,  │  │  (DO Spaces   │  │  (sendmail)      │    │
│  │              │  │   cache)     │  │   or local)   │  │                  │    │
│  └──────────────┘  └──────────────┘  └───────────────┘  └──────────────────┘    │
└─────────────────────────────────────────────────────────────────────────────────┘
```

---

## API Response Standards

All API endpoints follow a consistent response format using a hybrid approach:
- **Success responses**: Custom `ApiResponse<T>` wrapper
- **Error responses**: RFC 7807 ProblemDetails (standard format with tooling support)

### Success Response (with data)

```json
{
  "data": {
    "id": 1,
    "email": "user@example.com",
    "username": "john"
  },
  "meta": {
    "timestamp": "2025-12-07T10:30:00Z"
  }
}
```

### Success Response (message only)

```json
{
  "message": "Email verification sent",
  "meta": {
    "timestamp": "2025-12-07T10:30:00Z"
  }
}
```

### Paginated Response

```json
{
  "data": [
    { "id": 1, "key": "WelcomeMessage" },
    { "id": 2, "key": "GoodbyeMessage" }
  ],
  "meta": {
    "timestamp": "2025-12-07T10:30:00Z",
    "page": 1,
    "pageSize": 20,
    "totalCount": 150,
    "totalPages": 8
  }
}
```

### Error Response (ProblemDetails RFC 7807)

```json
{
  "type": "https://lrm.cloud/errors/auth-invalid-credentials",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Invalid email or password",
  "instance": "/api/auth/login",
  "timestamp": "2025-12-07T10:30:00Z",
  "errorCode": "AUTH_INVALID_CREDENTIALS",
  "traceId": "0HMOQ2..."
}
```

### Error Codes

Error codes use `SCREAMING_SNAKE_CASE` convention:

| Category | Codes |
|----------|-------|
| **AUTH_*** | `AUTH_INVALID_CREDENTIALS`, `AUTH_EMAIL_NOT_VERIFIED`, `AUTH_ACCOUNT_LOCKED`, `AUTH_TOKEN_EXPIRED`, `AUTH_TOKEN_INVALID`, `AUTH_UNAUTHORIZED`, `AUTH_FORBIDDEN` |
| **REG_*** | `REG_EMAIL_EXISTS`, `REG_DISABLED`, `REG_INVALID_TOKEN`, `REG_TOKEN_EXPIRED` |
| **VAL_*** | `VAL_INVALID_INPUT`, `VAL_REQUIRED_FIELD`, `VAL_INVALID_FORMAT` |
| **RES_*** | `RES_NOT_FOUND`, `RES_ALREADY_EXISTS`, `RES_CONFLICT`, `RES_VERSION_MISMATCH` |
| **SRV_*** | `SRV_INTERNAL_ERROR`, `SRV_SERVICE_UNAVAILABLE`, `SRV_RATE_LIMITED` |
| **EXT_*** | `EXT_GITHUB_ERROR`, `EXT_TRANSLATION_ERROR`, `EXT_MAIL_ERROR` |

### Controller Implementation

All controllers extend `ApiControllerBase` which provides helper methods:

```csharp
public class AuthController : ApiControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<UserDto>>> Register(RegisterRequest request)
    {
        // Validation error
        if (!IsValidEmail(request.Email))
            return BadRequest(ErrorCodes.VAL_INVALID_FORMAT, "Invalid email format");

        // Success with data
        var user = await _authService.RegisterAsync(request);
        return Created(nameof(GetUser), new { id = user.Id }, user);

        // Success with message
        return Success("Registration successful. Check your email.");

        // Not found
        return NotFound(ErrorCodes.RES_NOT_FOUND, "User not found");

        // Unauthorized
        return Unauthorized(ErrorCodes.AUTH_INVALID_CREDENTIALS, "Invalid email or password");
    }

    [HttpGet("users")]
    public async Task<ActionResult<ApiResponse<List<UserDto>>>> GetUsers(int page = 1, int pageSize = 20)
    {
        var (users, totalCount) = await _userService.GetPagedAsync(page, pageSize);
        return Paginated(users, page, pageSize, totalCount);
    }
}
```

### Files

| File | Purpose |
|------|---------|
| `LrmCloud.Shared/Api/ApiResponse.cs` | Response wrappers: `ApiResponse<T>`, `ApiResponse`, `ApiMeta` |
| `LrmCloud.Shared/Api/ErrorCodes.cs` | All error code constants |
| `LrmCloud.Shared/Api/ConflictResponse.cs` | Sync conflict response format |
| `LrmCloud.Api/Controllers/ApiControllerBase.cs` | Base controller with `Success()`, `Paginated()`, `BadRequest()`, etc. |
| `LrmCloud.Api/Middleware/GlobalExceptionHandler.cs` | Logs exceptions, returns ProblemDetails |

---

## Infrastructure

### Stack
- Ubuntu 24.04 LTS
- Docker + Docker Compose
- Nginx reverse proxy with security headers
- Certbot for SSL
- systemd for service management

---

## Configuration

**Principle:** Single `config.json` file for ALL configuration (git-ignored). No `appsettings.json`. Strongly-typed with JSON Schema validation and DI access.

### File Structure

```
cloud/src/LrmCloud.Api/
├── config.json                   # ⚠️ ALL config - in .gitignore
├── config.schema.json            # JSON Schema for validation/IDE support
└── config.example.json           # Template for new deployments (in git)
```

### config.json

```json
{
  "$schema": "./config.schema.json",

  "server": {
    "urls": "http://localhost:5000",
    "environment": "Development"
  },

  "logging": {
    "level": "Information",
    "console": true,
    "file": {
      "enabled": true,
      "path": "/var/log/lrmcloud/app.log",
      "rollingInterval": "Day"
    }
  },

  "cors": {
    "origins": ["https://lrm.cloud", "http://localhost:3000"]
  },

  "database": {
    "connectionString": "Host=localhost;Database=lrmcloud;Username=lrm;Password=xxx"
  },

  "redis": {
    "connectionString": "localhost:6379,password=xxx"
  },

  "encryption": {
    "tokenKey": "base64-encoded-32-byte-key-for-aes256"
  },

  "auth": {
    "jwtSecret": "64-char-random-secret",
    "jwtExpiryHours": 24,
    "github": {
      "clientId": "Ov23liXXXXXX",
      "clientSecret": "xxxxxxxxxxxxxxxxxxxxxx"
    }
  },

  "mail": {
    "host": "mail.yourdomain.com",
    "port": 587,
    "username": "noreply@lrm.cloud",
    "password": "xxxx",
    "fromAddress": "noreply@lrm.cloud",
    "fromName": "LRM Cloud"
  },

  "translation": {
    "platformKeys": {
      "google": "AIzaSyXXXXX",
      "deepl": "xxxxxxxx-xxxx-xxxx-xxxx",
      "azure": "xxxxxxxxxxxxxxxx"
    },
    "defaultProvider": "deepl"
  },

  "stripe": {
    "secretKey": "sk_live_XXXXX",
    "webhookSecret": "whsec_XXXXX",
    "publishableKey": "pk_live_XXXXX"
  },

  "features": {
    "registration": true,
    "githubSync": true,
    "freeTranslations": true
  },

  "limits": {
    "freeTranslationChars": 10000,
    "proTranslationChars": 100000,
    "maxProjectsPerUser": 5,
    "maxKeysPerProject": 10000
  }
}
```

### Strongly-Typed Configuration

```csharp
// Configuration/AppConfig.cs
public class AppConfig
{
    public ServerConfig Server { get; set; } = new();
    public LoggingConfig Logging { get; set; } = new();
    public CorsConfig Cors { get; set; } = new();
    public DatabaseConfig Database { get; set; } = new();
    public RedisConfig Redis { get; set; } = new();
    public EncryptionConfig Encryption { get; set; } = new();
    public AuthConfig Auth { get; set; } = new();
    public MailConfig Mail { get; set; } = new();
    public TranslationConfig Translation { get; set; } = new();
    public StripeConfig Stripe { get; set; } = new();
    public FeaturesConfig Features { get; set; } = new();
    public LimitsConfig Limits { get; set; } = new();
}

public class ServerConfig
{
    public string Urls { get; set; } = "http://localhost:5000";
    public string Environment { get; set; } = "Development";
}

public class DatabaseConfig
{
    public required string ConnectionString { get; set; }
}

public class AuthConfig
{
    public required string JwtSecret { get; set; }
    public int JwtExpiryHours { get; set; } = 24;
    public GitHubOAuthConfig? GitHub { get; set; }
}

public class FeaturesConfig
{
    public bool Registration { get; set; } = true;
    public bool GitHubSync { get; set; } = true;
    public bool FreeTranslations { get; set; } = true;
}

public class LimitsConfig
{
    public int FreeTranslationChars { get; set; } = 10000;
    public int ProTranslationChars { get; set; } = 100000;
    public int MaxProjectsPerUser { get; set; } = 5;
    public int MaxKeysPerProject { get; set; } = 10000;
}
// ... other config classes
```

### IConfigService

```csharp
// Services/IConfigService.cs
public interface IConfigService
{
    AppConfig Config { get; }

    // Convenience accessors
    string GetDatabaseConnectionString();
    string GetEncryptionKey();
    string? GetTranslationKey(string provider);
    bool IsFeatureEnabled(string feature);
}

// Services/ConfigService.cs
public class ConfigService : IConfigService
{
    public AppConfig Config { get; }

    public ConfigService()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
        if (!File.Exists(configPath))
            throw new FileNotFoundException($"config.json not found at {configPath}");

        var json = File.ReadAllText(configPath);
        Config = JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to parse config.json");

        ValidateRequired();
    }

    private void ValidateRequired()
    {
        if (string.IsNullOrEmpty(Config.Database?.ConnectionString))
            throw new InvalidOperationException("database.connectionString is required");
        if (string.IsNullOrEmpty(Config.Encryption?.TokenKey))
            throw new InvalidOperationException("encryption.tokenKey is required");
        if (string.IsNullOrEmpty(Config.Auth?.JwtSecret))
            throw new InvalidOperationException("auth.jwtSecret is required");
    }

    public string GetDatabaseConnectionString() => Config.Database.ConnectionString;
    public string GetEncryptionKey() => Config.Encryption.TokenKey;
    public string? GetTranslationKey(string provider) =>
        Config.Translation.PlatformKeys.GetValueOrDefault(provider.ToLower());
    public bool IsFeatureEnabled(string feature) => feature switch
    {
        "registration" => Config.Features.Registration,
        "githubSync" => Config.Features.GitHubSync,
        "freeTranslations" => Config.Features.FreeTranslations,
        _ => false
    };
}
```

### Registration in Program.cs

```csharp
// Program.cs - No appsettings.json, just config.json
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

// Clear default config sources (removes appsettings.json)
builder.Configuration.Sources.Clear();

// Load only config.json
builder.Configuration.AddJsonFile("config.json", optional: false, reloadOnChange: true);

// Register config service as singleton
builder.Services.AddSingleton<IConfigService, ConfigService>();

// Configure services using config
var configService = new ConfigService();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(configService.GetDatabaseConnectionString()));

builder.Services.AddStackExchangeRedisCache(options =>
    options.Configuration = configService.Config.Redis.ConnectionString);

// Configure Kestrel URLs from config
builder.WebHost.UseUrls(configService.Config.Server.Urls);
```

### .gitignore

```
# Config - NEVER commit (contains secrets)
config.json

# But DO commit the example
!config.example.json
```

### config.example.json (committed to git)

```json
{
  "$schema": "./config.schema.json",
  "server": {
    "urls": "http://localhost:5000",
    "environment": "Development"
  },
  "database": {
    "connectionString": "Host=localhost;Database=lrmcloud;Username=lrm;Password=CHANGE_ME"
  },
  "encryption": {
    "tokenKey": "GENERATE_32_BYTE_BASE64_KEY"
  },
  "auth": {
    "jwtSecret": "GENERATE_64_CHAR_SECRET"
  }
}
```

### Production Deployment

On the server, `config.json` is:
1. Created from `config.example.json` on first deploy
2. Stored in `/etc/lrmcloud/config.json`
3. Mounted into Docker container
4. Permissions: `600` (owner read/write only)

```yaml
# docker-compose.prod.yml
services:
  api:
    volumes:
      - /etc/lrmcloud/config.json:/app/config.json:ro
```

---

## Database Schema

### Users & Auth

```sql
CREATE TABLE users (
    id SERIAL PRIMARY KEY,
    auth_type VARCHAR(50) NOT NULL,        -- 'email', 'github', 'google'
    email VARCHAR(255) UNIQUE,
    email_verified BOOLEAN DEFAULT false,
    password_hash VARCHAR(255),            -- bcrypt hash (NULL for OAuth users)

    -- GitHub OAuth (optional)
    github_id BIGINT UNIQUE,
    github_access_token_encrypted TEXT,
    github_token_expires_at TIMESTAMPTZ,

    -- Profile
    username VARCHAR(255) NOT NULL,
    display_name VARCHAR(255),
    avatar_url TEXT,

    -- Subscription
    plan VARCHAR(50) DEFAULT 'free',       -- free, pro, team, enterprise
    stripe_customer_id VARCHAR(255),
    translation_chars_used INT DEFAULT 0,
    translation_chars_limit INT DEFAULT 10000,
    translation_chars_reset_at TIMESTAMPTZ,

    -- Security (tokens are hashed, not plain text)
    password_reset_token_hash VARCHAR(255),
    password_reset_expires TIMESTAMPTZ,
    email_verification_token_hash VARCHAR(255),
    last_login_at TIMESTAMPTZ,
    failed_login_attempts INT DEFAULT 0,
    locked_until TIMESTAMPTZ,

    -- Soft delete
    deleted_at TIMESTAMPTZ,

    -- Audit
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_users_email ON users(email);
CREATE INDEX idx_users_github_id ON users(github_id);

-- Row-Level Security
ALTER TABLE users ENABLE ROW LEVEL SECURITY;
CREATE POLICY users_own_data ON users
    USING (id = current_setting('app.user_id')::INT);
```

### Organizations (Teams)

```sql
CREATE TABLE organizations (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    slug VARCHAR(100) UNIQUE NOT NULL,
    owner_id INT NOT NULL REFERENCES users(id),
    plan VARCHAR(50) DEFAULT 'team',
    stripe_customer_id VARCHAR(255),
    translation_chars_used INT DEFAULT 0,
    translation_chars_limit INT DEFAULT 500000,

    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    deleted_at TIMESTAMPTZ
);

CREATE TABLE organization_members (
    id SERIAL PRIMARY KEY,
    organization_id INT NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    user_id INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    role VARCHAR(50) NOT NULL DEFAULT 'member',  -- owner, admin, member, viewer
    invited_by INT REFERENCES users(id),
    invited_at TIMESTAMPTZ,
    accepted_at TIMESTAMPTZ,

    created_at TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(organization_id, user_id)
);

CREATE INDEX idx_org_members_org ON organization_members(organization_id);
CREATE INDEX idx_org_members_user ON organization_members(user_id);
```

### Projects

```sql
CREATE TABLE projects (
    id SERIAL PRIMARY KEY,
    user_id INT REFERENCES users(id) ON DELETE CASCADE,
    organization_id INT REFERENCES organizations(id) ON DELETE CASCADE,
    name VARCHAR(255) NOT NULL,
    description TEXT,

    -- GitHub integration (NULL if CLI-only)
    github_repo VARCHAR(255),
    github_installation_id BIGINT,
    github_default_branch VARCHAR(100) DEFAULT 'main',
    github_webhook_secret VARCHAR(255),

    -- Localization settings
    localization_path VARCHAR(500) DEFAULT '.',
    format VARCHAR(50) NOT NULL,           -- resx, json, i18next
    default_language VARCHAR(10) DEFAULT 'en',

    -- Sync settings
    sync_mode VARCHAR(50) DEFAULT 'manual',
    auto_translate BOOLEAN DEFAULT false,
    auto_create_pr BOOLEAN DEFAULT true,

    -- State
    last_synced_at TIMESTAMPTZ,
    last_synced_commit VARCHAR(40),
    sync_status VARCHAR(50) DEFAULT 'pending',
    sync_error TEXT,

    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),

    CONSTRAINT project_owner CHECK (
        (user_id IS NOT NULL AND organization_id IS NULL) OR
        (user_id IS NULL AND organization_id IS NOT NULL)
    )
);

CREATE INDEX idx_projects_user ON projects(user_id);
CREATE INDEX idx_projects_org ON projects(organization_id);
CREATE INDEX idx_projects_github_repo ON projects(github_repo);

-- Row-Level Security
ALTER TABLE projects ENABLE ROW LEVEL SECURITY;
CREATE POLICY projects_access ON projects
    USING (
        user_id = current_setting('app.user_id')::INT
        OR organization_id IN (
            SELECT organization_id FROM organization_members
            WHERE user_id = current_setting('app.user_id')::INT
        )
    );
```

### Resource Keys & Translations

```sql
CREATE TABLE resource_keys (
    id SERIAL PRIMARY KEY,
    project_id INT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
    key_name VARCHAR(500) NOT NULL,
    key_path VARCHAR(500),
    is_plural BOOLEAN DEFAULT false,
    comment TEXT,
    version INT DEFAULT 1,  -- For optimistic locking

    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),

    UNIQUE(project_id, key_name)
);

CREATE INDEX idx_resource_keys_project ON resource_keys(project_id);

CREATE TABLE translations (
    id SERIAL PRIMARY KEY,
    resource_key_id INT NOT NULL REFERENCES resource_keys(id) ON DELETE CASCADE,
    language_code VARCHAR(10) NOT NULL,
    value TEXT,
    plural_form VARCHAR(20) DEFAULT '',    -- one, other, few, many, etc.
    status VARCHAR(50) DEFAULT 'pending',  -- pending, translated, reviewed, approved
    translated_by VARCHAR(50),
    reviewed_by INT REFERENCES users(id),
    version INT DEFAULT 1,  -- For optimistic locking

    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),

    UNIQUE(resource_key_id, language_code, plural_form)
);

CREATE INDEX idx_translations_key ON translations(resource_key_id);
CREATE INDEX idx_translations_language ON translations(language_code);
CREATE INDEX idx_translations_status ON translations(status);
CREATE INDEX idx_translations_updated ON translations(updated_at);
```

### API Keys

```sql
CREATE TABLE api_keys (
    id SERIAL PRIMARY KEY,
    user_id INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    project_id INT REFERENCES projects(id) ON DELETE CASCADE,
    key_prefix VARCHAR(10) NOT NULL,
    key_hash VARCHAR(255) NOT NULL,
    name VARCHAR(255),
    scopes VARCHAR(255) DEFAULT 'read,write',
    last_used_at TIMESTAMPTZ,
    expires_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_api_keys_user ON api_keys(user_id);
CREATE INDEX idx_api_keys_prefix ON api_keys(key_prefix);
CREATE INDEX idx_api_keys_expires ON api_keys(expires_at);
```

### Translation API Keys (Hierarchy)

```sql
CREATE TABLE user_api_keys (
    id SERIAL PRIMARY KEY,
    user_id INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    provider VARCHAR(50) NOT NULL,  -- google, deepl, openai, etc.
    encrypted_key TEXT NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(user_id, provider)
);

CREATE TABLE organization_api_keys (
    id SERIAL PRIMARY KEY,
    organization_id INT NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    provider VARCHAR(50) NOT NULL,
    encrypted_key TEXT NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(organization_id, provider)
);

CREATE TABLE project_api_keys (
    id SERIAL PRIMARY KEY,
    project_id INT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
    provider VARCHAR(50) NOT NULL,
    encrypted_key TEXT NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(project_id, provider)
);
```

### Sync & Conflicts

```sql
CREATE TABLE sync_history (
    id SERIAL PRIMARY KEY,
    project_id INT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
    sync_type VARCHAR(50) NOT NULL,
    direction VARCHAR(20),
    commit_sha VARCHAR(40),
    pr_number INT,
    pr_url TEXT,
    keys_added INT DEFAULT 0,
    keys_updated INT DEFAULT 0,
    keys_deleted INT DEFAULT 0,
    status VARCHAR(50),
    error_message TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE sync_conflicts (
    id SERIAL PRIMARY KEY,
    project_id INT NOT NULL REFERENCES projects(id),
    resource_key_id INT REFERENCES resource_keys(id),
    language_code VARCHAR(10),
    local_value TEXT,
    remote_value TEXT,
    local_updated_at TIMESTAMPTZ,
    remote_updated_at TIMESTAMPTZ,
    resolution VARCHAR(50),  -- local_wins, remote_wins, manual
    resolved_by INT REFERENCES users(id),
    resolved_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ DEFAULT NOW()
);
```

### Audit Log

```sql
CREATE TABLE audit_log (
    id SERIAL PRIMARY KEY,
    user_id INT REFERENCES users(id),
    project_id INT REFERENCES projects(id),
    action VARCHAR(100) NOT NULL,
    entity_type VARCHAR(50),
    entity_id INT,
    old_value JSONB,
    new_value JSONB,
    ip_address INET,
    user_agent TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_audit_log_user ON audit_log(user_id);
CREATE INDEX idx_audit_log_project ON audit_log(project_id);
CREATE INDEX idx_audit_log_created ON audit_log(created_at);
```

---

## Conflict Resolution Strategy

### Overview
Use **optimistic locking with user prompt** for conflicts:

1. Each resource has a `version` number
2. On sync, client sends its version
3. If versions don't match, conflict detected
4. User prompted to resolve

### CLI Sync Flow

```
lrm push
    │
    ├── POST /api/sync/push { files, localVersions }
    │
    ├── Server compares versions
    │       │
    │       ├── No conflicts → Apply changes, return new versions
    │       │
    │       └── Conflicts detected → Return 409 with conflict details
    │
    └── CLI shows conflict UI:

        ┌─────────────────────────────────────────────────────────┐
        │ Conflict detected for key: WelcomeMessage (en)          │
        │                                                         │
        │ LOCAL (modified 2 hours ago):                          │
        │   "Welcome to our app!"                                │
        │                                                         │
        │ REMOTE (modified 30 min ago by john@example.com):      │
        │   "Welcome to LRM Cloud!"                              │
        │                                                         │
        │ [L] Keep local  [R] Keep remote  [M] Manual merge      │
        └─────────────────────────────────────────────────────────┘
```

### Web UI Handling
- Real-time updates via polling (every 5s) or WebSocket
- If another user edits while you're editing:
  - Toast notification: "This key was updated by John"
  - Option to reload or overwrite

### API Response for Conflicts

```json
{
  "status": "conflict",
  "conflicts": [
    {
      "key": "WelcomeMessage",
      "language": "en",
      "local": { "value": "Welcome to our app!", "version": 3, "updatedAt": "..." },
      "remote": { "value": "Welcome to LRM Cloud!", "version": 4, "updatedAt": "...", "updatedBy": "john@..." }
    }
  ]
}
```

---

## API Endpoints

### Auth
```
POST /auth/register           # Email/password registration
POST /auth/login              # Email/password login
POST /auth/logout             # End session
POST /auth/forgot-password    # Request reset email
POST /auth/reset-password     # Set new password
POST /auth/verify-email       # Confirm email address
GET  /auth/github             # Redirect to GitHub OAuth
GET  /auth/github/callback    # OAuth callback
POST /auth/link-github        # Link GitHub to existing account
GET  /api/me                  # Current user info
```

### Projects
```
GET    /api/projects                    # List user's projects
POST   /api/projects                    # Create project
GET    /api/projects/:id                # Get project details
PUT    /api/projects/:id                # Update project
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

### Organizations
```
GET    /api/organizations               # List user's organizations
POST   /api/organizations               # Create organization
GET    /api/organizations/:id           # Get organization details
PUT    /api/organizations/:id           # Update organization
DELETE /api/organizations/:id           # Delete organization
GET    /api/organizations/:id/members   # List members
POST   /api/organizations/:id/members   # Invite member
DELETE /api/organizations/:id/members/:uid  # Remove member
PUT    /api/organizations/:id/members/:uid  # Update member role
```

### CLI Sync
```
POST /api/sync/push           # Upload local files (API key auth)
GET  /api/sync/pull           # Download current state
POST /api/sync/commit         # Create PR/commit with changes
```

### GDPR
```
GET    /api/me/export         # Export all user data (JSON)
DELETE /api/me                # Request account deletion
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
lrm cloud logout                   # Clear stored credentials
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

## Email Service Abstraction

```csharp
// IMailService.cs
public interface IMailService
{
    Task SendEmailAsync(string to, string subject, string htmlBody, string? textBody = null);
    Task SendTemplateEmailAsync(string to, string templateName, object model);
}

// SendmailService.cs - Your mail server
public class SendmailService : IMailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<SendmailService> _logger;

    public async Task SendEmailAsync(string to, string subject, string htmlBody, string? textBody)
    {
        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress(_config["Mail:FromName"], _config["Mail:FromAddress"]));
        msg.To.Add(MailboxAddress.Parse(to));
        msg.Subject = subject;

        var builder = new BodyBuilder
        {
            HtmlBody = htmlBody,
            TextBody = textBody ?? StripHtml(htmlBody)
        };
        msg.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(_config["Mail:Host"], _config.GetValue<int>("Mail:Port"), SecureSocketOptions.Auto);

        if (!string.IsNullOrEmpty(_config["Mail:Username"]))
            await client.AuthenticateAsync(_config["Mail:Username"], _config["Mail:Password"]);

        await client.SendAsync(msg);
        await client.DisconnectAsync(true);
    }
}

// Configuration in appsettings.json
{
  "Mail": {
    "Provider": "Sendmail",  // or "SendGrid", "Postmark" for future
    "Host": "mail.yourdomain.com",
    "Port": 587,
    "FromAddress": "noreply@lrm.cloud",
    "FromName": "LRM Cloud"
  }
}
```

---

## Translation API Key Hierarchy

```csharp
public class TranslationKeyResolver
{
    public async Task<string?> GetApiKey(Project project, User user, string provider)
    {
        // 1. Check project-level key
        var projectKey = await _db.ProjectApiKeys
            .FirstOrDefaultAsync(k => k.ProjectId == project.Id && k.Provider == provider);
        if (projectKey != null)
            return Decrypt(projectKey.EncryptedKey);

        // 2. Check user-level key
        var userKey = await _db.UserApiKeys
            .FirstOrDefaultAsync(k => k.UserId == user.Id && k.Provider == provider);
        if (userKey != null)
            return Decrypt(userKey.EncryptedKey);

        // 3. Check organization-level key (if team project)
        if (project.OrganizationId.HasValue)
        {
            var orgKey = await _db.OrganizationApiKeys
                .FirstOrDefaultAsync(k => k.OrganizationId == project.OrganizationId && k.Provider == provider);
            if (orgKey != null)
                return Decrypt(orgKey.EncryptedKey);
        }

        // 4. Fall back to platform key
        return _config[$"Translation:{provider}:PlatformKey"];
    }
}
```

---

## Security Implementation

### Critical Fixes (Before Launch)

1. **Account Enumeration Prevention**
```csharp
// Return same message for all login failures
return Unauthorized(new { message = "Invalid email or password" });
// Don't reveal if email exists
```

2. **Hash Password Reset Tokens**
```csharp
var token = GenerateSecureToken();
var tokenHash = BCrypt.HashPassword(token, 10);
user.PasswordResetTokenHash = tokenHash;
// Send plain token to user, store hash
```

3. **Security Headers (nginx)**
```nginx
add_header X-Frame-Options "DENY" always;
add_header X-Content-Type-Options "nosniff" always;
add_header X-XSS-Protection "1; mode=block" always;
add_header Referrer-Policy "strict-origin-when-cross-origin" always;
add_header Content-Security-Policy "default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; style-src 'self' 'unsafe-inline';" always;
add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;

# Rate limiting
limit_req_zone $binary_remote_addr zone=login:10m rate=5r/m;
location /api/auth/login {
    limit_req zone=login burst=3 nodelay;
}
```

4. **Row-Level Security**
```sql
-- Enabled on users, projects, resource_keys, translations tables
-- Policies defined above in schema
```

### SOC2 Foundation

1. **Audit Logging** - All changes logged with user, timestamp, old/new values
2. **Session Management**
   - Cookie: `HttpOnly`, `Secure`, `SameSite=Strict`
   - Session expiry: 24 hours (configurable)
   - Invalidate on password change
3. **Data Encryption**
   - AES-256-GCM for API keys and tokens
   - Key stored in environment variable (not code)
4. **Access Control**
   - Role-based: owner, admin, member, viewer
   - Scoped API keys

---

## GDPR Implementation

### Required Features

1. **Privacy Policy Page** - `/privacy`
2. **Cookie Consent** - Simple banner (only essential cookies for MVP)
3. **Data Export API**
```
GET /api/me/export
→ Returns JSON with all user data
```
4. **Account Deletion API**
```
DELETE /api/me
→ Soft delete with 30-day grace period
→ Hard delete after 30 days (background job)
```
5. **Data Processing Agreement** - For team admins

### Database Helper

```sql
CREATE FUNCTION export_user_data(user_id INT) RETURNS JSONB AS $$
SELECT jsonb_build_object(
    'user', row_to_json(u.*),
    'projects', (SELECT jsonb_agg(row_to_json(p.*)) FROM projects p WHERE p.user_id = user_id),
    'translations', (SELECT jsonb_agg(row_to_json(t.*)) FROM translations t
                     JOIN resource_keys k ON t.resource_key_id = k.id
                     JOIN projects p ON k.project_id = p.id
                     WHERE p.user_id = user_id),
    'audit_log', (SELECT jsonb_agg(row_to_json(a.*)) FROM audit_log a WHERE a.user_id = user_id)
)
FROM users u WHERE u.id = user_id;
$$ LANGUAGE SQL;
```

---

## GitHub App Integration

### Setup
- [ ] Create GitHub App at github.com/settings/apps/new
- [ ] App name: "LRM Cloud"
- [ ] Homepage: https://lrm.cloud
- [ ] Webhook URL: https://lrm.cloud/webhooks/github
- [ ] Webhook secret: Generate and store securely
- [ ] Generate private key (.pem file) - store in secrets

### Permissions
| Permission | Access | Reason |
|------------|--------|--------|
| Contents | Read & Write | Read localization files, create commits |
| Pull requests | Read & Write | Create PRs with translation changes |
| Metadata | Read | Get repo info |

### Webhook Events
| Event | Handler |
|-------|---------|
| `installation` | Save installation ID, list of repos |
| `installation_repositories` | Update repo list |
| `push` | Sync localization files on push |

---

## Pricing

| Tier | Price | Projects | Translation | Features |
|------|-------|----------|-------------|----------|
| Free | $0 | 1 | 10K chars/mo | CLI sync only |
| Pro | $9/mo | 5 | 100K chars/mo | GitHub App, web editor |
| Team | $29/mo | 20 | 500K chars/mo | Multiple users, roles, priority |

### Translation Costs (platform cost)
- Google: ~$20/million chars
- DeepL: ~$25/million chars
- Free providers (MyMemory, Lingva): $0

At $9/mo for 100K chars, margin is healthy even with paid providers.

---

## Implementation Phases

### Phase 0: Foundation (Week 1-2) ✅ COMPLETED
- [x] Create `cloud/` folder structure
- [x] Initialize `LrmCloud.sln` with projects:
  - `LrmCloud.Api` - ASP.NET Core Web API
  - `LrmCloud.Web` - Blazor WebAssembly
  - `LrmCloud.Shared` - Shared DTOs, models
  - `LrmCloud.Tests`
- [x] Set up Docker Compose (dev)
- [x] Create setup.sh (interactive config generation)
- [x] Create deploy.sh (automated deployment with nginx config regeneration)
- [x] Create Dockerfile.api (multi-stage build)
- [x] Implement CloudConfiguration (type-safe config binding)
- [x] Add health checks endpoint (/health)
- [x] Configure EF Core + migrations
- [x] Implement IMailService with Sendmail provider
- [x] Set up nginx with security headers
- [x] Fix deploy.sh nginx configuration bug (regenerate config on each deploy)

### Phase 1: Auth & Teams (Week 3-4)

**Authentication (✅ COMPLETED)**
- [x] Email/password registration
- [x] Email verification flow
- [x] Password reset flow (with hashed tokens)
- [x] Login with JWT + refresh tokens
- [x] Refresh token rotation
- [x] GitHub OAuth login
- [x] Account linking (email ↔ GitHub)
- [x] Get current user endpoint
- [x] Update user profile
- [x] Change email flow (with verification)
- [x] Change password (with session revocation)
- [x] Session management (view/revoke tokens)
- [x] Account deletion (soft delete with 30-day grace period)
- [x] Comprehensive unit tests (72 tests passing)

**Teams & Organizations (TODO - Next up)**
- [ ] Organization CRUD
- [ ] Team invitations
- [ ] Role-based access control

### Phase 2: Core API (Week 5-6)
- [ ] Project CRUD
- [ ] Resource key CRUD
- [ ] Translation CRUD
- [ ] Validation endpoint
- [ ] Stats endpoint
- [ ] Version tracking for optimistic locking
- [ ] Row-Level Security policies

### Phase 3: CLI Sync (Week 7-8)
- [ ] `lrm cloud login` - browser OAuth
- [ ] `lrm cloud init` - create project
- [ ] `lrm push` - upload with version check
- [ ] `lrm pull` - download with conflict detection
- [ ] Conflict resolution UI in CLI
- [ ] API key authentication

### Phase 4: GitHub App (Week 9-10)
- [ ] Register GitHub App
- [ ] Webhook handler with signature verification
- [ ] Installation event handler
- [ ] Push event → sync
- [ ] Create PR with translations
- [ ] Rate limit handling

### Phase 5: Blazor WebAssembly UI (Week 11-13)
- [ ] Auth state provider
- [ ] Dashboard (project list, stats)
- [ ] Project settings page
- [ ] Translation editor (virtualized grid)
- [ ] Search/filter
- [ ] Inline editing
- [ ] Translate missing button
- [ ] Create PR button
- [ ] Team management UI
- [ ] PWA support (offline read-only)

### Phase 6: Translation Service (Week 14)
- [ ] API key hierarchy (project → user → org → platform)
- [ ] Translation with fallback providers
- [ ] Usage tracking and limits
- [ ] Batch translation endpoint

### Phase 7: Billing & Compliance (Week 15-16)
- [ ] Stripe integration
- [ ] Subscription management
- [ ] Usage metering
- [ ] Privacy policy page
- [ ] Cookie consent banner
- [ ] Data export endpoint
- [ ] Account deletion flow
- [ ] Audit log UI

### Phase 8: Launch Prep (Week 17)
- [ ] Landing page
- [ ] Documentation
- [ ] Load testing (k6)
- [ ] Security testing (OWASP ZAP)
- [ ] Backup verification
- [ ] Monitoring setup

---

## Project Structure

```
LocalizationManager/
├── [existing CLI code]
│
└── cloud/
    ├── LrmCloud.sln
    ├── src/
    │   ├── LrmCloud.Api/
    │   │   ├── Controllers/
    │   │   │   ├── AuthController.cs
    │   │   │   ├── ProjectsController.cs
    │   │   │   ├── ResourcesController.cs
    │   │   │   ├── OrganizationsController.cs
    │   │   │   ├── SyncController.cs
    │   │   │   ├── WebhooksController.cs
    │   │   │   └── TranslationController.cs
    │   │   ├── Services/
    │   │   │   ├── IMailService.cs
    │   │   │   ├── SendmailService.cs
    │   │   │   ├── GitHubService.cs
    │   │   │   ├── SyncService.cs
    │   │   │   └── TranslationKeyResolver.cs
    │   │   ├── Data/
    │   │   │   ├── AppDbContext.cs
    │   │   │   └── Migrations/
    │   │   ├── Security/
    │   │   │   ├── RowLevelSecurityMiddleware.cs
    │   │   │   └── AuditLogMiddleware.cs
    │   │   └── Program.cs
    │   │
    │   ├── LrmCloud.Web/           # Blazor WASM
    │   │   ├── wwwroot/
    │   │   ├── Pages/
    │   │   │   ├── Index.razor
    │   │   │   ├── Dashboard.razor
    │   │   │   ├── Login.razor
    │   │   │   └── Projects/
    │   │   ├── Components/
    │   │   │   ├── TranslationEditor.razor
    │   │   │   ├── TranslationCell.razor
    │   │   │   └── TeamManager.razor
    │   │   ├── Services/
    │   │   │   └── ApiClient.cs
    │   │   └── Program.cs
    │   │
    │   └── LrmCloud.Shared/
    │       ├── DTOs/
    │       ├── Models/
    │       └── Constants.cs
    │
    ├── tests/
    │   └── LrmCloud.Tests/
    │
    ├── docker/
    │   ├── docker-compose.yml
    │   ├── docker-compose.prod.yml
    │   └── nginx/
    │       └── nginx.conf
    │
    └── deploy/
        ├── setup-server.sh
        └── deploy.sh
```

---

## Monitoring

- **Logs:** systemd journal + logrotate
- **Metrics:** Simple health endpoint + uptime monitoring (UptimeRobot free)
- **Alerts:** Email on service down
- **Later:** Grafana if needed

---

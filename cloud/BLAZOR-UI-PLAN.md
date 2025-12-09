# Blazor WebAssembly UI + Translation Service Implementation Plan

## Summary

| Decision | Choice |
|----------|--------|
| UI Library | **MudBlazor v8** (MIT, best virtualized grid) |
| Editor Layout | **Hybrid** (spreadsheet grid + detail drawer) |
| PWA Offline | **Read-only caching** |
| Collaboration | **Optimistic locking** (no real-time) |
| State Management | **Cascading Parameters** (simple, sufficient) |
| Translation | **Reuse Core library** via API wrapper |

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                    Blazor WebAssembly (SPA)                     │
├─────────────────────────────────────────────────────────────────┤
│  Pages/                    │  Components/                       │
│  ├── Auth/                 │  ├── TranslationEditor.razor       │
│  │   ├── Login.razor       │  ├── TranslationGrid.razor         │
│  │   ├── Register.razor    │  ├── KeyDetailDrawer.razor         │
│  │   └── ForgotPassword    │  ├── TranslateDialog.razor         │
│  ├── Dashboard.razor       │  ├── ProviderSelector.razor        │
│  ├── Projects/             │  ├── TranslationProgress.razor     │
│  │   ├── List.razor        │  └── ProjectCard.razor             │
│  │   ├── Editor.razor      │                                    │
│  │   └── Settings.razor    │  Services/                         │
│  └── Settings/             │  ├── AuthService.cs                │
│      ├── Profile.razor     │  ├── ProjectService.cs             │
│      ├── ApiKeys.razor     │  ├── ResourceService.cs            │
│      └── Team.razor        │  ├── TranslationService.cs         │
│                            │  └── NotificationService.cs        │
└─────────────────────────────────────────────────────────────────┘
                                      │ HttpClient
                                      ▼
┌─────────────────────────────────────────────────────────────────┐
│                     LrmCloud.Api (Backend)                      │
├─────────────────────────────────────────────────────────────────┤
│  Controllers/              │  Services/                         │
│  ├── AuthController        │  ├── ITranslationService           │
│  ├── ProjectsController    │  │   └── Uses Core Translation     │
│  ├── ResourcesController   │  ├── TranslationKeyResolver        │
│  └── TranslationController │  │   (project→user→org→platform)   │
│       ├── POST /translate  │  └── TranslationUsageTracker       │
│       ├── GET /providers   │                                    │
│       └── GET /usage       │                                    │
└─────────────────────────────────────────────────────────────────┘
```

---

## Translation Integration Strategy

### Reusing Existing Core Library

The CLI/TUI already has excellent translation infrastructure in `LocalizationManager.Core.Translation`:

```
LocalizationManager.Core/Translation/
├── ITranslationProvider.cs           # Interface
├── TranslationProviderFactory.cs     # Factory (10 providers)
├── TranslationCache.cs               # SQLite-based 30-day cache
├── RateLimiter.cs                    # Token bucket rate limiting
├── TranslationRequest.cs             # Request model
├── TranslationResponse.cs            # Response model
└── Providers/
    ├── GoogleTranslateProvider.cs
    ├── DeepLProvider.cs
    ├── LingvaTranslateProvider.cs    # FREE
    ├── MyMemoryProvider.cs           # FREE
    ├── LibreTranslateProvider.cs     # FREE (self-hosted)
    ├── OllamaProvider.cs             # FREE (local)
    ├── OpenAIProvider.cs
    ├── ClaudeProvider.cs
    ├── AzureOpenAIProvider.cs
    └── AzureTranslatorProvider.cs
```

### 4-Tier API Key Hierarchy (Cloud)

```
┌─────────────────────────────────────────────────────────────────┐
│                    API Key Resolution Order                      │
├─────────────────────────────────────────────────────────────────┤
│ 1. Project Key     │ Most specific - project admin set this     │
│ 2. Organization Key│ Team billing - shared across org projects  │
│ 3. User Key        │ Personal BYOK for personal projects        │
│ 4. Platform Key    │ Default fallback (subject to tier limits)  │
└─────────────────────────────────────────────────────────────────┘
```

**Rationale:**
- Project key is most specific (override for specific project)
- Organization key handles team billing (company provides key for all team projects)
- User key for personal account BYOK
- Platform fallback with usage limits

### Provider Categories

| Category | Providers | Key Source | Free Tier | Paid Tiers |
|----------|-----------|------------|-----------|------------|
| **Free (always available)** | Lingva, MyMemory | None needed | ✓ | ✓ |
| **Platform-provided** | Google, DeepL | Platform config | ✗ | ✓ |
| **BYOK-only** | OpenAI, Claude, Azure* | User must provide | ✓ (with key) | ✓ (with key) |

*Azure includes AzureOpenAI and AzureTranslator

### Tier Limits

| Tier | Chars/Month | Available Providers |
|------|-------------|---------------------|
| Free | 10,000 | Free providers only (Lingva, MyMemory) |
| Pro ($9/mo) | 100,000 | + Platform Google/DeepL |
| Team ($29/mo) | 500,000 | + Platform Google/DeepL |

### Encryption Strategy (Per-User Derived Keys)

```csharp
// Master key from config.json
var masterKey = config.Encryption.TokenKey;

// Derive per-user key using PBKDF2
var salt = Encoding.UTF8.GetBytes($"user:{userId}");
var derivedKey = Rfc2898DeriveBytes.Pbkdf2(masterKey, salt, 100_000, HashAlgorithmName.SHA256, 32);

// Encrypt API key with user's derived key
var encryptedKey = AesGcmEncrypt(apiKey, derivedKey);
```

**Benefits:**
- Key isolation per user (compromise one doesn't expose all)
- Single master key to manage in config.json
- Can migrate to Vault later without schema changes

### Cloud API Translation Flow

```
Blazor UI                     Cloud API                      Core Library
    │                             │                              │
    │  POST /api/translate        │                              │
    │  {keys, targetLangs,        │                              │
    │   provider, onlyMissing}    │                              │
    ├────────────────────────────►│                              │
    │                             │  1. Resolve API key          │
    │                             │     (project→user→org→       │
    │                             │      platform fallback)      │
    │                             │                              │
    │                             │  2. Check usage limits       │
    │                             │     (chars remaining)        │
    │                             │                              │
    │                             │  3. Create provider          │
    │                             ├─────────────────────────────►│
    │                             │  TranslationProviderFactory  │
    │                             │  .Create(provider, config)   │
    │                             │                              │
    │                             │  4. Translate via cache      │
    │                             ├─────────────────────────────►│
    │                             │  provider.TranslateAsync()   │
    │                             │◄─────────────────────────────┤
    │                             │                              │
    │                             │  5. Update usage tracking    │
    │                             │                              │
    │  200 OK                     │                              │
    │  {translated, usage}        │                              │
    │◄────────────────────────────┤                              │
```

---

## Phase 4A-0: Landing Page (First!)

**Goal**: Fast-loading, professional static landing page at `/`

### Requirements
- **NOT Blazor** - Pure HTML/CSS/JS for instant loading
- Served at `/` (root)
- "Login" / "Get Started" button → `/app` (Blazor app)
- Professional, clean, modern design
- Mobile responsive

### Tasks

- [ ] **4A.0.1** Create static landing page
  - [ ] `wwwroot/index.html` (static, served by nginx)
  - [ ] Hero section with tagline
  - [ ] Feature highlights (3-4 cards)
  - [ ] Pricing preview (Free/Pro/Team)
  - [ ] "Get Started Free" CTA button
  - [ ] Footer with links

- [ ] **4A.0.2** Styling
  - [ ] `wwwroot/css/landing.css` - Custom styles
  - [ ] Use system fonts (no external font loading)
  - [ ] Dark/light mode support via `prefers-color-scheme`
  - [ ] Smooth animations (CSS only, no JS libraries)

- [ ] **4A.0.3** Routing setup
  - [ ] nginx: `/` → static `index.html`
  - [ ] nginx: `/app/*` → Blazor WASM
  - [ ] nginx: `/api/*` → API backend
  - [ ] Login button: checks auth, redirects to `/app` or `/app/login`

### Landing Page Structure

```
┌─────────────────────────────────────────────────────────────────────┐
│ [LRM Logo]                              [Features] [Pricing] [Login]│
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│              Localization Made Simple                               │
│                                                                     │
│      Manage translations for your .NET and web apps with            │
│      10 translation providers, CLI + Web + TUI interfaces           │
│                                                                     │
│              [Get Started Free]  [View Demo]                        │
│                                                                     │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│   ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌───────────┐  │
│   │  10 AI      │  │  CLI + TUI  │  │  Team       │  │  BYOK     │  │
│   │  Providers  │  │  + Web UI   │  │  Collab     │  │  Support  │  │
│   │             │  │             │  │             │  │           │  │
│   │ Google,     │  │ Edit in     │  │ Orgs, roles │  │ Use your  │  │
│   │ DeepL,      │  │ terminal    │  │ invites     │  │ own API   │  │
│   │ OpenAI...   │  │ or browser  │  │             │  │ keys free │  │
│   └─────────────┘  └─────────────┘  └─────────────┘  └───────────┘  │
│                                                                     │
├─────────────────────────────────────────────────────────────────────┤
│                        Pricing                                      │
│                                                                     │
│   ┌─────────────┐  ┌─────────────┐  ┌─────────────┐                 │
│   │    Free     │  │     Pro     │  │    Team     │                 │
│   │    $0/mo    │  │    $9/mo    │  │   $29/mo    │                 │
│   │             │  │             │  │             │                 │
│   │ • 10K chars │  │ • 100K chars│  │ • 500K chars│                 │
│   │ • Free AI   │  │ • Google    │  │ • Google    │                 │
│   │ • 2 projects│  │ • DeepL     │  │ • DeepL     │                 │
│   │             │  │ • Unlimited │  │ • Org teams │                 │
│   │ [Start]     │  │ [Upgrade]   │  │ [Contact]   │                 │
│   └─────────────┘  └─────────────┘  └─────────────┘                 │
│                                                                     │
├─────────────────────────────────────────────────────────────────────┤
│  LRM Cloud | Docs | GitHub | Privacy | Terms         © 2025         │
└─────────────────────────────────────────────────────────────────────┘
```

### Files to Create

```
cloud/src/LrmCloud.Web/wwwroot/
├── index.html              # Landing page (static)
├── css/
│   └── landing.css         # Landing page styles
└── images/
    ├── logo.svg            # Logo
    ├── hero-screenshot.png # App screenshot
    └── favicon.ico         # Favicon

cloud/deploy/nginx/
└── nginx.conf.template     # Updated routing rules
```

### nginx Routing Update

```nginx
# Static landing page (fast!)
location = / {
    root /usr/share/nginx/html;
    try_files /index.html =404;
}

# Blazor WASM app
location /app {
    proxy_pass http://web;
    # ... existing config
}
```

---

## Phase 4A: Foundation

**Goal**: Basic app shell with auth working

### Tasks

- [ ] **4A.1** Add MudBlazor package and configure
  - [ ] `dotnet add package MudBlazor`
  - [ ] Configure in `Program.cs`
  - [ ] Add CSS/JS to `index.html`
  - [ ] Configure theme (light/dark toggle)

- [ ] **4A.2** Create layout structure
  - [ ] `MainLayout.razor` with MudLayout
  - [ ] Top AppBar (logo, user menu, theme toggle)
  - [ ] Left drawer/sidebar (navigation)
  - [ ] Main content area

- [ ] **4A.3** Authentication infrastructure
  - [ ] `AuthenticationStateProvider` (JWT-based)
  - [ ] `AuthService.cs` (login, register, logout)
  - [ ] Token storage in localStorage
  - [ ] Auto-refresh token handling
  - [ ] `HttpClient` auth header interceptor

- [ ] **4A.4** Auth pages
  - [ ] `Login.razor`
  - [ ] `Register.razor`
  - [ ] `ForgotPassword.razor`
  - [ ] `ResetPassword.razor`
  - [ ] `VerifyEmail.razor`

- [ ] **4A.5** Protected routing
  - [ ] `AuthorizeView` wrapper
  - [ ] Redirect to login if not authenticated
  - [ ] `@attribute [Authorize]` on protected pages

- [ ] **4A.6** GitHub OAuth (optional login method)
  - [ ] "Login with GitHub" button on Login.razor
  - [ ] Redirect to `/api/auth/github` (backend handles OAuth flow)
  - [ ] `GitHubCallback.razor` - handle token from query string
  - [ ] Account linking (if email exists, link GitHub to account)

### Files to Create

```
cloud/src/LrmCloud.Web/
├── Layout/
│   ├── MainLayout.razor          # MudLayout with drawer
│   └── NavMenu.razor             # Navigation links
├── Pages/
│   └── Auth/
│       ├── Login.razor
│       ├── Register.razor
│       ├── ForgotPassword.razor
│       ├── ResetPassword.razor
│       └── VerifyEmail.razor
├── Services/
│   ├── AuthService.cs            # Login/register/logout
│   ├── TokenStorageService.cs    # LocalStorage wrapper
│   └── LrmAuthStateProvider.cs   # Custom AuthStateProvider
├── wwwroot/
│   └── css/
│       └── app.css               # Theme overrides
└── Program.cs                    # MudBlazor + Auth setup
```

---

## Phase 4B: Dashboard

**Goal**: Project list with basic management

### Tasks

- [ ] **4B.1** Create ProjectService
  - [ ] `GetProjectsAsync()` - list user's projects
  - [ ] `GetProjectAsync(id)` - single project with stats
  - [ ] `CreateProjectAsync(request)`
  - [ ] `UpdateProjectAsync(id, request)`
  - [ ] `DeleteProjectAsync(id)`

- [ ] **4B.2** Dashboard page
  - [ ] Project cards grid (MudGrid + MudCard)
  - [ ] Stats per project (key count, completion %)
  - [ ] "New Project" button
  - [ ] Recent activity section (if time)

- [ ] **4B.3** Project dialogs
  - [ ] Create project modal (MudDialog)
  - [ ] Edit project modal
  - [ ] Delete confirmation

- [ ] **4B.4** Organization selector
  - [ ] Dropdown in AppBar or sidebar
  - [ ] Switch between personal/org projects
  - [ ] Remember last selection

- [ ] **4B.5** Project import (file upload)
  - [ ] File dropzone component (MudFileUpload)
  - [ ] Accept .resx, .json files (drag & drop or browse)
  - [ ] Auto-detect format (resx vs json-localization vs i18next)
  - [ ] Parse files using Core library, store in DB
  - [ ] Show "CLI Link" command after creation for local sync

### Files to Create

```
Pages/
├── Dashboard.razor
└── Projects/
    └── List.razor                # Redirects to Dashboard

Components/
├── ProjectCard.razor             # Card with stats
├── CreateProjectDialog.razor
└── EditProjectDialog.razor

Services/
└── ProjectService.cs
```

---

## Phase 4C: Translation Editor Core

**Goal**: Virtualized grid showing keys and translations

### Tasks

- [ ] **4C.1** Create ResourceService
  - [ ] `GetKeysAsync(projectId, filter)` - paginated/filtered
  - [ ] `GetKeyDetailAsync(projectId, key)` - with all translations
  - [ ] `UpdateTranslationAsync(projectId, key, lang, value)`
  - [ ] `CreateKeyAsync(projectId, request)`
  - [ ] `DeleteKeyAsync(projectId, key)`

- [ ] **4C.2** Translation grid component
  - [ ] MudDataGrid with virtualization
  - [ ] Dynamic columns based on project languages
  - [ ] Key column (frozen/sticky)
  - [ ] Status column (icons: missing, complete, partial)
  - [ ] Row selection (checkbox column)

- [ ] **4C.3** Inline cell editing
  - [ ] Click cell to enter edit mode
  - [ ] Tab to next cell
  - [ ] Enter to save, Escape to cancel
  - [ ] Dirty indicator for unsaved changes

- [ ] **4C.4** Grid styling
  - [ ] Missing translations highlighted
  - [ ] Plural keys indicator
  - [ ] Comment tooltip on hover

### Files to Create

```
Pages/Projects/
└── Editor.razor                  # Main editor page

Components/
├── TranslationGrid.razor         # MudDataGrid wrapper
└── TranslationCell.razor         # Editable cell

Services/
└── ResourceService.cs

Models/
├── KeyViewModel.cs               # Grid row model
└── TranslationViewModel.cs       # Cell model
```

### Grid Layout

```
┌──────────────────────────────────────────────────────────────────────────┐
│ [Search...        ] [Status: All ▼] [Language: All ▼] [+ Add Key]       │
├──────────────────────────────────────────────────────────────────────────┤
│ ☐ │ Key                    │ en (default)     │ de            │ fr      │
├───┼────────────────────────┼──────────────────┼───────────────┼─────────┤
│ ☐ │ WelcomeMessage         │ Welcome!         │ Willkommen!   │ ⚠️       │
│ ☐ │ ButtonSave         [P] │ Save             │ Speichern     │ Sauver  │
│ ☑ │ ErrorNotFound          │ Not found        │ Nicht gefund. │ Non tro.│
│ ☐ │ Greeting.Morning       │ Good morning     │ Guten Morgen  │ Bonjour │
└───┴────────────────────────┴──────────────────┴───────────────┴─────────┘
[P] = Plural key    ⚠️ = Missing translation    ☑ = Selected
```

---

## Phase 4D: Editor Features

**Goal**: Full editing experience with drawer and bulk operations

### Tasks

- [ ] **4D.1** Search and filter bar
  - [ ] Text search (key, value, comment)
  - [ ] Status filter dropdown (All, Missing, Complete, Partial)
  - [ ] Language filter (show/hide columns)
  - [ ] Debounced search (300ms)

- [ ] **4D.2** Key detail drawer
  - [ ] Slide-in panel (MudDrawer)
  - [ ] All translations in full textareas
  - [ ] Comment field
  - [ ] Character count per field
  - [ ] Per-language translate button
  - [ ] Save/Cancel buttons

- [ ] **4D.3** Bulk actions toolbar
  - [ ] Appears when rows selected
  - [ ] "Translate Selected" button
  - [ ] "Delete Selected" button
  - [ ] Selection count display

- [ ] **4D.4** Add/Delete key
  - [ ] Add key dialog (with plural option for JSON format)
  - [ ] Delete confirmation with cascade warning

- [ ] **4D.5** Keyboard shortcuts
  - [ ] Ctrl+S - Save changes
  - [ ] Ctrl+F - Focus search
  - [ ] Arrow keys - Navigate grid
  - [ ] Enter - Open drawer
  - [ ] Delete - Delete selected (with confirm)

### Files to Create

```
Components/
├── KeyDetailDrawer.razor         # Right-side drawer
├── SearchFilterBar.razor         # Top filter controls
├── BulkActionsBar.razor          # Floating bar when selected
├── AddKeyDialog.razor
└── ConfirmDeleteDialog.razor
```

### Detail Drawer Layout

```
┌─────────────────────────────────────────────────┐
│ ✕  Edit Key: WelcomeMessage                     │
├─────────────────────────────────────────────────┤
│ Status: ● 2/3 Complete                          │
│ Comment: [Main greeting on homepage           ] │
│ Character limit: 50                             │
├─────────────────────────────────────────────────┤
│                                                 │
│ en (default):                           [🔄]    │
│ ┌─────────────────────────────────────────────┐ │
│ │ Welcome!                              12/50 │ │
│ └─────────────────────────────────────────────┘ │
│                                                 │
│ de:                                     [🔄]    │
│ ┌─────────────────────────────────────────────┐ │
│ │ Willkommen!                           11/50 │ │
│ └─────────────────────────────────────────────┘ │
│                                                 │
│ fr:                                     [🔄]    │
│ ┌─────────────────────────────────────────────┐ │
│ │                                       0/50  │ │
│ └─────────────────────────────────────────────┘ │
│                                                 │
│        [Cancel]  [Translate All]  [Save]        │
└─────────────────────────────────────────────────┘
[🔄] = Translate this language using AI
```

---

## Phase 4E: Translation Service Integration

**Goal**: Working translation with provider selection and usage tracking

### Tasks

- [ ] **4E.1** Backend: TranslationController
  - [ ] `GET /api/projects/{id}/translation/providers` - available providers
  - [ ] `GET /api/projects/{id}/translation/usage` - usage stats
  - [ ] `POST /api/projects/{id}/translation/translate` - batch translate

- [ ] **4E.2** Backend: TranslationService
  - [ ] Reuse `LocalizationManager.Core.Translation` namespace
  - [ ] `TranslationKeyResolver` (hierarchy: project→user→org→platform)
  - [ ] `TranslationUsageTracker` (chars used per user/org)
  - [ ] SQLite `TranslationCache` integration

- [ ] **4E.3** Backend: API key hierarchy tables
  - [ ] `user_translation_keys` (user_id, provider, encrypted_key)
  - [ ] `organization_translation_keys` (org_id, provider, encrypted_key)
  - [ ] `project_translation_keys` (project_id, provider, encrypted_key)

- [ ] **4E.4** UI: TranslateDialog component
  - [ ] Provider selector (dropdown with icons)
  - [ ] Target language checkboxes
  - [ ] "Only translate missing" toggle
  - [ ] Progress bar (real-time updates via polling)
  - [ ] Result summary (X translated, Y failed)

- [ ] **4E.5** UI: TranslationService.cs (Blazor)
  - [ ] `GetProvidersAsync(projectId)` - returns configured providers
  - [ ] `GetUsageAsync(projectId)` - chars used/remaining
  - [ ] `TranslateAsync(request)` - batch translate

- [ ] **4E.6** UI: Provider configuration page
  - [ ] List configured providers
  - [ ] Add/Edit API key modal
  - [ ] Test connection button
  - [ ] Free providers highlighted (Lingva, MyMemory)

### API Endpoints

```
# Get available providers (with configuration status)
GET /api/projects/{id}/translation/providers
Response:
{
  "data": [
    { "name": "google", "displayName": "Google Cloud Translation",
      "configured": true, "requiresApiKey": true },
    { "name": "lingva", "displayName": "Lingva (Free)",
      "configured": true, "requiresApiKey": false },
    ...
  ]
}

# Get usage stats
GET /api/projects/{id}/translation/usage
Response:
{
  "data": {
    "charsUsed": 45000,
    "charsLimit": 100000,
    "resetsAt": "2025-01-01T00:00:00Z",
    "plan": "pro"
  }
}

# Translate keys
POST /api/projects/{id}/translation/translate
Request:
{
  "keys": ["WelcomeMessage", "ButtonSave"],
  "targetLanguages": ["de", "fr"],
  "provider": "google",
  "onlyMissing": true
}
Response:
{
  "data": {
    "translated": 3,
    "skipped": 1,
    "failed": 0,
    "charsUsed": 120,
    "results": [
      { "key": "WelcomeMessage", "lang": "de", "status": "success" },
      { "key": "WelcomeMessage", "lang": "fr", "status": "success" },
      { "key": "ButtonSave", "lang": "de", "status": "skipped",
        "reason": "already translated" }
    ]
  }
}
```

### Files to Create

```
# Backend
cloud/src/LrmCloud.Api/
├── Controllers/
│   ├── TranslationController.cs        # /api/projects/{id}/translation/*
│   └── TranslationKeysController.cs    # CRUD for API keys at all scopes
├── Services/
│   ├── ICloudTranslationService.cs
│   ├── CloudTranslationService.cs      # Wraps Core providers
│   ├── TranslationKeyResolver.cs       # 4-tier hierarchy resolution
│   ├── TranslationUsageTracker.cs      # Chars tracking + limit enforcement
│   ├── IEncryptionService.cs           # Per-user key derivation
│   └── EncryptionService.cs            # AES-GCM with PBKDF2 derived keys
├── Data/
│   └── Migrations/
│       └── AddTranslationKeyTables.cs
└── Models/
    └── TranslationKeyDto.cs

# Frontend
cloud/src/LrmCloud.Web/
├── Components/
│   ├── TranslateDialog.razor           # Provider selection + progress
│   ├── ProviderSelector.razor          # Dropdown with icons
│   ├── TranslationProgress.razor       # Real-time progress bar
│   └── ApiKeyDialog.razor              # Add/Edit API key modal
├── Pages/Settings/
│   ├── ApiKeys.razor                   # User-level API keys
│   ├── OrgApiKeys.razor                # Organization-level API keys
│   └── Usage.razor                     # Translation usage dashboard
├── Pages/Projects/
│   └── TranslationSettings.razor       # Project-level API keys + provider config
└── Services/
    ├── TranslationService.cs           # Translate API calls
    └── ApiKeyService.cs                # CRUD for API keys
```

### Backend: CloudTranslationService Implementation

```csharp
public class CloudTranslationService : ICloudTranslationService
{
    private readonly TranslationKeyResolver _keyResolver;
    private readonly TranslationUsageTracker _usageTracker;
    private readonly TranslationCache _cache;  // Reuse from Core

    public async Task<TranslationResult> TranslateAsync(
        TranslationRequest request, Project project, User user)
    {
        // 1. Resolve API key using 4-tier hierarchy
        var (apiKey, keySource) = await _keyResolver.ResolveKeyAsync(
            project, user, request.Provider);

        // 2. Check usage limits (only for platform keys)
        if (keySource == KeySource.Platform)
        {
            var canTranslate = await _usageTracker.CheckLimitAsync(user, request.EstimatedChars);
            if (!canTranslate)
                throw new UsageLimitExceededException(user.Plan, user.CharsRemaining);
        }

        // 3. Create provider using Core factory
        var config = BuildConfigWithKey(request.Provider, apiKey);
        var provider = TranslationProviderFactory.Create(request.Provider, config);

        // 4. Translate with caching
        var result = await _cache.GetOrTranslateAsync(request, provider);

        // 5. Track usage (for billing/analytics)
        await _usageTracker.RecordAsync(user, project, request.Provider, keySource, result.CharsUsed);

        return result;
    }
}
```

### Translate Dialog UI

```
┌──────────────────────────────────────────────────────────────────┐
│  Translate 5 Keys                                          [X]  │
├──────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Provider:  [Google Cloud Translation ▼]                        │
│                                                                  │
│  Target Languages:                                               │
│  [✓] German (de)                                                 │
│  [✓] French (fr)                                                 │
│  [ ] Spanish (es)                                                │
│                                                                  │
│  Options:                                                        │
│  [✓] Only translate missing values                               │
│                                                                  │
│  Usage: 45,000 / 100,000 chars (55% remaining)                   │
│  ████████████░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░                    │
│                                                                  │
│  Estimated cost: ~120 chars                                      │
│                                                                  │
├──────────────────────────────────────────────────────────────────┤
│                      [Cancel]  [Translate]                       │
└──────────────────────────────────────────────────────────────────┘
```

### API Key Management Endpoints

```
# User-level API keys
GET    /api/settings/translation-keys
       → [{ provider, configured, createdAt }]

PUT    /api/settings/translation-keys/{provider}
       ← { apiKey: "sk-..." }
       → { success: true }

DELETE /api/settings/translation-keys/{provider}
       → { success: true }

POST   /api/settings/translation-keys/{provider}/test
       → { valid: true, error?: "..." }

# Organization-level API keys (admin only)
GET    /api/organizations/{id}/translation-keys
PUT    /api/organizations/{id}/translation-keys/{provider}
DELETE /api/organizations/{id}/translation-keys/{provider}
POST   /api/organizations/{id}/translation-keys/{provider}/test

# Project-level API keys (project admin only)
GET    /api/projects/{id}/translation-keys
PUT    /api/projects/{id}/translation-keys/{provider}
DELETE /api/projects/{id}/translation-keys/{provider}
POST   /api/projects/{id}/translation-keys/{provider}/test
```

### API Keys Settings UI

```
┌─────────────────────────────────────────────────────────────────────┐
│  Translation API Keys                                               │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  Your API keys are used when translating in your personal projects. │
│  Keys are encrypted and stored securely.                            │
│                                                                     │
│  ┌─────────────────────────────────────────────────────────────────┐│
│  │ FREE PROVIDERS (No key required)                                ││
│  ├─────────────────────────────────────────────────────────────────┤│
│  │ ✓ Lingva (Google via proxy)                      [Available]   ││
│  │ ✓ MyMemory                                       [Available]   ││
│  └─────────────────────────────────────────────────────────────────┘│
│                                                                     │
│  ┌─────────────────────────────────────────────────────────────────┐│
│  │ PAID PROVIDERS (Bring your own key)                             ││
│  ├─────────────────────────────────────────────────────────────────┤│
│  │ ○ Google Cloud Translation          [+ Add Key]                ││
│  │ ✓ DeepL                 ●●●●●●●●●●●● [Test] [Edit] [Delete]    ││
│  │ ○ OpenAI                            [+ Add Key]                ││
│  │ ○ Claude                            [+ Add Key]                ││
│  │ ○ Azure Translator                  [+ Add Key]                ││
│  │ ○ Azure OpenAI                      [+ Add Key]                ││
│  └─────────────────────────────────────────────────────────────────┘│
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘

✓ = Configured    ○ = Not configured    ●●●● = Masked key
```

### Usage Dashboard UI

```
┌─────────────────────────────────────────────────────────────────────┐
│  Translation Usage                                    December 2025 │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  Plan: Pro ($9/mo)                                                  │
│  Usage: 45,230 / 100,000 characters                                 │
│  ████████████████░░░░░░░░░░░░░░░░░░░░░░░░░  45%                     │
│  Resets: January 1, 2026                                            │
│                                                                     │
│  ┌─────────────────────────────────────────────────────────────────┐│
│  │ Usage by Provider                                               ││
│  ├─────────────────────────────────────────────────────────────────┤│
│  │ Google (Platform)        32,150 chars  ███████████░░░░░ 71%    ││
│  │ Lingva (Free)             8,430 chars  ███░░░░░░░░░░░░░ 19%    ││
│  │ DeepL (Your Key)          4,650 chars  ██░░░░░░░░░░░░░░ 10%    ││
│  └─────────────────────────────────────────────────────────────────┘│
│                                                                     │
│  ┌─────────────────────────────────────────────────────────────────┐│
│  │ Recent Translations                                             ││
│  ├─────────────────────────────────────────────────────────────────┤│
│  │ Today 14:23   my-app      5 keys → de, fr    Google    1,230   ││
│  │ Today 10:15   website     12 keys → es       Lingva    2,890   ││
│  │ Yesterday     my-app      3 keys → it        DeepL       450   ││
│  └─────────────────────────────────────────────────────────────────┘│
│                                                                     │
│  ⓘ Characters from your own API keys don't count against limits    │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Phase 4F: Settings & Teams

**Goal**: Project settings and team management

### Tasks

- [ ] **4F.1** Project settings page
  - [ ] Display current lrm.json configuration
  - [ ] Editable: Default language, validation rules
  - [ ] Read-only: Format, file paths (tooltip: "Edit via CLI")
  - [ ] Save with optimistic locking

- [ ] **4F.2** Language management
  - [ ] Add language button
  - [ ] Remove language (with confirmation)
  - [ ] Set default language

- [ ] **4F.3** Team management
  - [ ] List team members with roles
  - [ ] Invite member (by email)
  - [ ] Change role (Admin, Member, Viewer)
  - [ ] Remove member

- [ ] **4F.4** User profile settings
  - [ ] Update display name
  - [ ] Change email (with verification)
  - [ ] Change password
  - [ ] Delete account

- [ ] **4F.5** Translation provider API keys (User level)
  - [ ] List configured providers with status
  - [ ] Add/Edit API key modal
  - [ ] Test key validity button
  - [ ] Show free providers (always available)
  - [ ] Delete key with confirmation

- [ ] **4F.6** Organization API keys (Org admin only)
  - [ ] Same UI pattern as user keys
  - [ ] Scope indicator: "These keys are used by all org projects"
  - [ ] Override user keys for org projects

- [ ] **4F.7** Project translation settings
  - [ ] Project-level API key overrides
  - [ ] Default provider selection
  - [ ] Show effective key source ("Using: Org key" / "Using: Your key")

- [ ] **4F.8** Usage dashboard
  - [ ] Current usage vs limit bar
  - [ ] Usage breakdown by provider
  - [ ] Recent translation history
  - [ ] "BYOK doesn't count" notice

### Files to Create

```
Pages/
├── Projects/
│   ├── Settings.razor            # General project config
│   └── TranslationSettings.razor # Project-level translation keys
└── Settings/
    ├── Profile.razor             # User settings
    ├── ApiKeys.razor             # User translation keys
    ├── Usage.razor               # Usage dashboard
    └── Team.razor                # Team members

Pages/Organizations/
└── ApiKeys.razor                 # Org-level translation keys

Components/
├── InviteMemberDialog.razor
├── ChangeRoleDialog.razor
├── ApiKeyDialog.razor            # Add/Edit API key modal
├── UsageBar.razor                # Reusable usage progress bar
└── ProviderList.razor            # Reusable provider list with status
```

---

## Phase 4G: Polish

**Goal**: Production-ready UX

### Tasks

- [ ] **4G.1** Loading states
  - [ ] Skeleton loaders for grid
  - [ ] Spinner for API calls
  - [ ] Disabled buttons during operations

- [ ] **4G.2** Error handling
  - [ ] Global error boundary
  - [ ] Toast notifications (MudSnackbar)
  - [ ] Retry logic for failed requests
  - [ ] Offline detection

- [ ] **4G.3** Responsive design
  - [ ] Mobile-friendly layout
  - [ ] Collapsible sidebar
  - [ ] Touch-friendly controls

- [ ] **4G.4** PWA support
  - [ ] Service worker (cache static assets)
  - [ ] Manifest.json
  - [ ] Offline project list viewing
  - [ ] "You're offline" banner

- [ ] **4G.5** Final touches
  - [ ] Keyboard navigation throughout
  - [ ] Focus management
  - [ ] Confirm unsaved changes on leave
  - [ ] Empty states (no projects, no keys)

### Files to Create

```
Components/
├── LoadingSkeleton.razor
├── ErrorBoundary.razor
├── OfflineBanner.razor
└── EmptyState.razor

wwwroot/
├── manifest.json
├── service-worker.js
└── icons/
    ├── icon-192.png
    └── icon-512.png
```

---

## Phase 4H: Organizations & Teams

**Goal**: Full multi-tenant support with organizations, team management, and permissions

### Roles & Permissions

```
┌─────────────────────────────────────────────────────────────────┐
│                    Role Hierarchy                                │
├─────────────────────────────────────────────────────────────────┤
│ OWNER   │ Full control, billing, delete org, transfer ownership │
│ ADMIN   │ Manage members, projects, settings (no billing)       │
│ MEMBER  │ Create/edit projects, translate, no settings          │
│ VIEWER  │ Read-only access to projects                          │
└─────────────────────────────────────────────────────────────────┘
```

### Tasks

#### Backend

- [ ] **4H.1** OrganizationsController
  - [ ] `GET /api/organizations` - list user's orgs
  - [ ] `POST /api/organizations` - create org
  - [ ] `GET /api/organizations/{id}` - org details + stats
  - [ ] `PUT /api/organizations/{id}` - update (name, settings)
  - [ ] `DELETE /api/organizations/{id}` - delete (owner only)
  - [ ] `POST /api/organizations/{id}/transfer` - transfer ownership

- [ ] **4H.2** OrganizationMembersController
  - [ ] `GET /api/organizations/{id}/members` - list members with roles
  - [ ] `POST /api/organizations/{id}/members/invite` - send invite email
  - [ ] `PUT /api/organizations/{id}/members/{userId}` - change role
  - [ ] `DELETE /api/organizations/{id}/members/{userId}` - remove member
  - [ ] `POST /api/organizations/{id}/members/leave` - leave org

- [ ] **4H.3** InvitationsController
  - [ ] `GET /api/invitations` - pending invitations for user
  - [ ] `POST /api/invitations/{token}/accept` - accept invite
  - [ ] `POST /api/invitations/{token}/decline` - decline invite
  - [ ] `DELETE /api/invitations/{id}` - cancel (inviter)

- [ ] **4H.4** Authorization services
  - [ ] `IAuthorizationService` - check permissions
  - [ ] `[RequireOrgRole(Role.Admin)]` attribute
  - [ ] `[RequireProjectAccess]` attribute
  - [ ] Row-level security policies

#### Frontend

- [ ] **4H.5** Organization pages
  - [ ] `Organizations/Index.razor` - list user's orgs
  - [ ] `Organizations/Create.razor` - create org form
  - [ ] `Organizations/{id}/Settings.razor` - org settings
  - [ ] `Organizations/{id}/Members.razor` - member management
  - [ ] `Organizations/{id}/Billing.razor` - plan & usage

- [ ] **4H.6** Member management UI
  - [ ] Members list with role badges
  - [ ] Invite member dialog (email input)
  - [ ] Change role dropdown
  - [ ] Remove member confirmation
  - [ ] Pending invitations list

- [ ] **4H.7** Invitation flow
  - [ ] Invitation email template
  - [ ] `/invite/{token}` landing page
  - [ ] Accept/decline buttons
  - [ ] Auto-login for new users

- [ ] **4H.8** Context switching
  - [ ] Org selector in AppBar
  - [ ] "Personal" vs org context
  - [ ] Remember last selected org
  - [ ] Projects filter by context

### API Endpoints

```
# Organizations
GET    /api/organizations                      → list orgs
POST   /api/organizations                      ← { name, slug? }
GET    /api/organizations/{id}                 → org details
PUT    /api/organizations/{id}                 ← { name }
DELETE /api/organizations/{id}                 → success

# Members
GET    /api/organizations/{id}/members         → [{ userId, email, role, joinedAt }]
POST   /api/organizations/{id}/members/invite  ← { email, role }
PUT    /api/organizations/{id}/members/{uid}   ← { role }
DELETE /api/organizations/{id}/members/{uid}   → success

# Invitations
GET    /api/invitations                        → pending invites for current user
POST   /api/invitations/{token}/accept         → { organizationId }
POST   /api/invitations/{token}/decline        → success
```

### Files to Create

```
# Backend
cloud/src/LrmCloud.Api/
├── Controllers/
│   ├── OrganizationsController.cs
│   ├── OrganizationMembersController.cs
│   └── InvitationsController.cs
├── Services/
│   ├── IOrganizationService.cs
│   ├── OrganizationService.cs
│   ├── IInvitationService.cs
│   ├── InvitationService.cs
│   └── IPermissionService.cs
├── Authorization/
│   ├── RequireOrgRoleAttribute.cs
│   ├── RequireProjectAccessAttribute.cs
│   └── PermissionHandler.cs
└── Data/Migrations/
    └── AddOrganizationTables.cs

# Frontend
cloud/src/LrmCloud.Web/
├── Pages/Organizations/
│   ├── Index.razor              # List orgs
│   ├── Create.razor             # Create org form
│   ├── Settings.razor           # Org settings
│   ├── Members.razor            # Member management
│   └── Billing.razor            # Plan & usage
├── Pages/
│   └── AcceptInvite.razor       # /invite/{token}
├── Components/
│   ├── OrgSelector.razor        # AppBar dropdown
│   ├── InviteMemberDialog.razor
│   ├── MemberRoleChip.razor
│   └── PendingInvites.razor
└── Services/
    ├── OrganizationService.cs
    └── InvitationService.cs
```

### Database Schema

```sql
-- Organizations
CREATE TABLE organizations (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    slug VARCHAR(100) UNIQUE NOT NULL,
    owner_id INT NOT NULL REFERENCES users(id),
    plan VARCHAR(50) DEFAULT 'free',
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    deleted_at TIMESTAMPTZ
);

-- Organization members
CREATE TABLE organization_members (
    id SERIAL PRIMARY KEY,
    organization_id INT NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    user_id INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    role VARCHAR(50) NOT NULL DEFAULT 'member',  -- owner, admin, member, viewer
    invited_by INT REFERENCES users(id),
    joined_at TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(organization_id, user_id)
);

-- Pending invitations
CREATE TABLE invitations (
    id SERIAL PRIMARY KEY,
    organization_id INT NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    email VARCHAR(255) NOT NULL,
    role VARCHAR(50) NOT NULL DEFAULT 'member',
    token VARCHAR(255) UNIQUE NOT NULL,
    invited_by INT NOT NULL REFERENCES users(id),
    expires_at TIMESTAMPTZ NOT NULL,
    accepted_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_invitations_email ON invitations(email);
CREATE INDEX idx_invitations_token ON invitations(token);

-- Update projects table to support org ownership
ALTER TABLE projects ADD COLUMN organization_id INT REFERENCES organizations(id);
-- A project belongs to EITHER user_id (personal) OR organization_id (team)
```

### UI Mockups

#### Organization List

```
┌─────────────────────────────────────────────────────────────────────┐
│  My Organizations                                    [+ New Org]    │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌───────────────────────────────────────┐  ┌─────────────────────┐ │
│  │ Acme Corp                       OWNER │  │ Open Source Co MEMBER│ │
│  │ 5 members · 12 projects               │  │ 3 members · 4 proj   │ │
│  │ Plan: Team ($29/mo)                   │  │ Plan: Pro ($9/mo)    │ │
│  │ [Settings] [Members]                  │  │ [View Projects]      │ │
│  └───────────────────────────────────────┘  └─────────────────────┘ │
│                                                                     │
│  Pending Invitations                                                │
│  ┌─────────────────────────────────────────────────────────────────┐│
│  │ StartupXYZ invited you as ADMIN         [Accept] [Decline]     ││
│  └─────────────────────────────────────────────────────────────────┘│
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

#### Member Management

```
┌─────────────────────────────────────────────────────────────────────┐
│  Acme Corp · Members                                                │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  [+ Invite Member]                                                  │
│                                                                     │
│  ┌─────────────────────────────────────────────────────────────────┐│
│  │ Name              Email                 Role      Actions       ││
│  ├─────────────────────────────────────────────────────────────────┤│
│  │ John Doe (you)    john@acme.com        [OWNER]   —             ││
│  │ Jane Smith        jane@acme.com        [Admin ▼] [Remove]      ││
│  │ Bob Wilson        bob@acme.com         [Member▼] [Remove]      ││
│  │ Alice Brown       alice@acme.com       [Viewer▼] [Remove]      ││
│  └─────────────────────────────────────────────────────────────────┘│
│                                                                     │
│  Pending Invitations                                                │
│  ┌─────────────────────────────────────────────────────────────────┐│
│  │ charlie@example.com    MEMBER    Expires in 6 days   [Cancel]  ││
│  └─────────────────────────────────────────────────────────────────┘│
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Testing Plan

### Unit Tests

- [ ] AuthService tests (login, token refresh)
- [ ] TranslationKeyResolver tests (hierarchy)
- [ ] TranslationUsageTracker tests (limits)

### Integration Tests

- [ ] Auth flow (register → verify → login)
- [ ] Project CRUD
- [ ] Translation flow (with mock provider)

### E2E Tests (Playwright)

- [ ] Login and create project
- [ ] Add and translate keys
- [ ] Team invitation flow

---

## Dependencies

### NuGet Packages (Web)

```xml
<PackageReference Include="MudBlazor" Version="8.*" />
<PackageReference Include="Blazored.LocalStorage" Version="4.*" />
<PackageReference Include="Microsoft.AspNetCore.Components.Authorization" Version="9.*" />
```

### NuGet Packages (API - Translation)

```xml
<!-- Already in LocalizationManager.Core, just reference the project -->
<ProjectReference Include="..\..\..\LocalizationManager.Core\LocalizationManager.Core.csproj" />
```

---

## Database Migrations

### Translation API Keys

```sql
-- User-level translation keys
CREATE TABLE user_translation_keys (
    id SERIAL PRIMARY KEY,
    user_id INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    provider VARCHAR(50) NOT NULL,
    encrypted_key TEXT NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(user_id, provider)
);

-- Organization-level translation keys
CREATE TABLE organization_translation_keys (
    id SERIAL PRIMARY KEY,
    organization_id INT NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    provider VARCHAR(50) NOT NULL,
    encrypted_key TEXT NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(organization_id, provider)
);

-- Project-level translation keys
CREATE TABLE project_translation_keys (
    id SERIAL PRIMARY KEY,
    project_id INT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
    provider VARCHAR(50) NOT NULL,
    encrypted_key TEXT NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(project_id, provider)
);

-- Usage tracking
CREATE TABLE translation_usage (
    id SERIAL PRIMARY KEY,
    user_id INT REFERENCES users(id),
    organization_id INT REFERENCES organizations(id),
    month DATE NOT NULL,  -- First day of month
    chars_used BIGINT DEFAULT 0,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(user_id, organization_id, month)
);
```

---

## Notes from Existing Code

### TUI Translation Pattern (to replicate in Blazor)

From `ResourceEditorWindow.Dialogs.cs`:

1. **Provider selection** - Dropdown with all configured providers
2. **Target language checkboxes** - All non-default languages
3. **"Only missing" toggle** - Skip already translated values
4. **Progress bar** - Real-time updates during translation
5. **Cache integration** - SQLite cache for 30 days
6. **Plural key support** - Translate each plural form separately
7. **Context awareness** - Show key/comment for single-key translation

### TranslationProviderFactory

From `TranslationProviderFactory.cs`:

```csharp
// 10 providers, 4 are FREE:
new ProviderInfo("lingva", "Lingva (Google via proxy)", RequiresApiKey: false),
new ProviderInfo("mymemory", "MyMemory", RequiresApiKey: false),
new ProviderInfo("libretranslate", "LibreTranslate", RequiresApiKey: false),
new ProviderInfo("ollama", "Ollama (Local LLM)", RequiresApiKey: false),

// 6 require API keys:
new ProviderInfo("google", "Google Cloud Translation", RequiresApiKey: true),
new ProviderInfo("deepl", "DeepL", RequiresApiKey: true),
new ProviderInfo("openai", "OpenAI", RequiresApiKey: true),
new ProviderInfo("claude", "Anthropic Claude", RequiresApiKey: true),
new ProviderInfo("azureopenai", "Azure OpenAI", RequiresApiKey: true),
new ProviderInfo("azuretranslator", "Azure Translator", RequiresApiKey: true),
```

### Key Hierarchy for API Key Resolution (4-Tier)

```
1. Project-level key     │ Most specific - project admin configures
2. Organization-level key│ Team billing - shared across org projects
3. User-level key        │ Personal BYOK for personal projects
4. Platform key          │ config.json - subject to tier limits
```

**Usage Tracking Rules:**
- Platform keys: Count against tier limits (Free: 10K, Pro: 100K, Team: 500K chars/month)
- BYOK: Track for analytics only, no limits applied
- Show users: "Using your API key - no limits apply"

**Provider Availability by Tier:**
| Tier | Free Providers | Platform Providers | BYOK Providers |
|------|----------------|-------------------|----------------|
| Free | Lingva, MyMemory | ❌ | ✓ (if configured) |
| Pro/Team | Lingva, MyMemory | Google, DeepL | ✓ (if configured) |

---

## Gap Analysis (Backend vs Plan)

### Already Exists in Backend
| Component | Status | Notes |
|-----------|--------|-------|
| AuthController | Exists | Login, register, verify email, password reset |
| GitHubAuthService | Exists | OAuth callback handling |
| ProjectsController | Exists | CRUD operations |
| OrganizationsController | Exists | CRUD + member management |
| ResourcesController | Exists | Key/translation CRUD |
| API Key Tables | Exists | UserApiKey, OrgApiKey, ProjectApiKey |
| AuditLog | Exists | Entity + table in DB |
| IMailService | Exists | SMTP email sending |
| MinioStorageService | Exists | File storage for projects |

### Still Needs Implementation
| Component | Phase | Priority |
|-----------|-------|----------|
| TranslationController | 4E | High |
| translation_usage table | 4E | High |
| CloudTranslationService | 4E | High |
| GitHub OAuth UI (Login button) | 4A | Medium |
| Project Import (file upload + CLI link) | 4B | Medium |
| Notifications System | 4J | Medium |
| Activity Feed | 4J | Medium |
| Billing/Stripe Integration | 4I | Low (post-MVP) |

### Project Import Options
Users can import resources via:
1. **File Upload (UI)**: Drag & drop .resx/.json files in Create Project dialog
2. **CLI Sync**: `lrm cloud push` from local project
3. **CLI Link**: Create in UI, then `lrm cloud init --link <project-id>` locally

---

## Phase 4J: Notifications & Activity Feed

**Goal**: In-app notifications and activity tracking

### Tasks

#### Backend

- [ ] **4J.1** Activity tracking
  - [ ] `Activity` entity (user_id, project_id, action, details, created_at)
  - [ ] `IActivityService` - log activities automatically
  - [ ] Integrate with ResourceService (log key add/edit/delete)
  - [ ] Integrate with TranslationService (log translations)
  - [ ] `GET /api/activities` - paginated activity feed
  - [ ] `GET /api/projects/{id}/activities` - project-specific

- [ ] **4J.2** Notifications system
  - [ ] `Notification` entity (user_id, type, title, message, read, created_at)
  - [ ] `INotificationService` - create notifications
  - [ ] Notification triggers:
    - Invited to organization
    - Translation completed (bulk)
    - Project shared with you
    - Usage limit warning (80%, 100%)
  - [ ] `GET /api/notifications` - list notifications
  - [ ] `PUT /api/notifications/{id}/read` - mark as read
  - [ ] `PUT /api/notifications/read-all` - mark all read

#### Frontend

- [ ] **4J.3** Notification bell (AppBar)
  - [ ] Bell icon with unread count badge
  - [ ] Dropdown showing recent notifications
  - [ ] Click to navigate to related resource
  - [ ] "Mark all as read" button
  - [ ] Poll every 30s for new notifications (or SignalR later)

- [ ] **4J.4** Activity feed (Dashboard)
  - [ ] `ActivityFeed.razor` component
  - [ ] Shows recent changes across all projects
  - [ ] Filter by project
  - [ ] Infinite scroll or pagination
  - [ ] Activity icons by type (add, edit, delete, translate)

- [ ] **4J.5** Project activity tab
  - [ ] Activity history in project settings
  - [ ] Who changed what, when
  - [ ] Filterable by user, action type

### Database Schema

```sql
-- Activities (audit trail)
CREATE TABLE activities (
    id SERIAL PRIMARY KEY,
    user_id INT REFERENCES users(id),
    project_id INT REFERENCES projects(id),
    organization_id INT REFERENCES organizations(id),
    action VARCHAR(50) NOT NULL,  -- 'key_created', 'key_updated', 'translation_added', etc.
    entity_type VARCHAR(50),       -- 'resource_key', 'translation', 'project'
    entity_id INT,
    details JSONB,                 -- { "key": "WelcomeMessage", "lang": "de", "old": "...", "new": "..." }
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_activities_user ON activities(user_id);
CREATE INDEX idx_activities_project ON activities(project_id);
CREATE INDEX idx_activities_created ON activities(created_at DESC);

-- Notifications
CREATE TABLE notifications (
    id SERIAL PRIMARY KEY,
    user_id INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    type VARCHAR(50) NOT NULL,     -- 'invite', 'translation_complete', 'usage_warning'
    title VARCHAR(255) NOT NULL,
    message TEXT,
    link VARCHAR(500),             -- URL to navigate to
    read BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_notifications_user ON notifications(user_id, read);
CREATE INDEX idx_notifications_created ON notifications(created_at DESC);
```

### Activity Types

| Action | Description | Details |
|--------|-------------|---------|
| `key_created` | New key added | `{ key, project }` |
| `key_updated` | Key value changed | `{ key, lang, old, new }` |
| `key_deleted` | Key removed | `{ key }` |
| `translation_added` | Translation added | `{ key, lang, provider }` |
| `translation_bulk` | Bulk translation | `{ count, langs, provider }` |
| `project_created` | New project | `{ name }` |
| `member_invited` | Team invite sent | `{ email, role }` |
| `member_joined` | Invite accepted | `{ name, role }` |

### UI Mockups

#### Notification Bell

```
┌──────────────────────────────────────────────────────┐
│ [Logo]              LRM Cloud          [🔔 3] [👤 ▼] │
├──────────────────────────────────────────────────────┤
                                          │            │
                           ┌──────────────┴────────────┤
                           │ Notifications             │
                           ├───────────────────────────┤
                           │ 🎉 John invited you to    │
                           │    Acme Corp              │
                           │    2 minutes ago          │
                           ├───────────────────────────┤
                           │ ✅ Translation complete   │
                           │    12 keys → de, fr       │
                           │    1 hour ago             │
                           ├───────────────────────────┤
                           │ ⚠️ Usage at 80%          │
                           │    Upgrade for more chars │
                           │    Yesterday              │
                           ├───────────────────────────┤
                           │ [Mark all as read]        │
                           └───────────────────────────┘
```

#### Activity Feed (Dashboard)

```
┌─────────────────────────────────────────────────────────┐
│ Recent Activity                          [All Projects ▼]│
├─────────────────────────────────────────────────────────┤
│ 📝 You updated "WelcomeMessage" in my-app        2m ago │
│ 🌐 You translated 5 keys to de, fr in my-app     1h ago │
│ ➕ Jane added "NewFeature" in website            2h ago │
│ 🗑️ Bob deleted "OldKey" in legacy-app        Yesterday │
│ 👋 Alice joined Acme Corp as Admin           Yesterday │
│                                                         │
│                    [Load More]                          │
└─────────────────────────────────────────────────────────┘
```

### Files to Create

```
# Backend
cloud/src/LrmCloud.Api/
├── Controllers/
│   ├── ActivitiesController.cs
│   └── NotificationsController.cs
├── Services/
│   ├── IActivityService.cs
│   ├── ActivityService.cs
│   ├── INotificationService.cs
│   └── NotificationService.cs
└── Data/Migrations/
    └── AddNotificationsAndActivities.cs

# Entities
cloud/src/LrmCloud.Shared/Entities/
├── Activity.cs
└── Notification.cs

# Frontend
cloud/src/LrmCloud.Web/
├── Components/
│   ├── NotificationBell.razor
│   ├── NotificationDropdown.razor
│   ├── ActivityFeed.razor
│   └── ActivityItem.razor
├── Services/
│   ├── NotificationService.cs
│   └── ActivityService.cs
└── Pages/
    └── Notifications.razor    # Full notifications page
```

---

## Phase 4I: Billing & Subscriptions (Post-MVP)

**Goal**: Subscription management with Stripe

### Tasks

#### Backend

- [ ] **4I.1** Stripe integration
  - [ ] Add Stripe.net package
  - [ ] `IStripeService` interface
  - [ ] `StripeService` implementation
  - [ ] Webhook endpoint `/api/webhooks/stripe`

- [ ] **4I.2** Subscription entities
  - [ ] `Subscription` entity (user_id/org_id, plan, stripe_subscription_id)
  - [ ] `PaymentHistory` entity (amount, status, invoice_url)
  - [ ] Migration: AddBillingTables

- [ ] **4I.3** BillingController
  - [ ] `GET /api/billing/subscription` - current plan
  - [ ] `POST /api/billing/checkout` - create checkout session
  - [ ] `POST /api/billing/portal` - customer portal link
  - [ ] `POST /api/billing/cancel` - cancel subscription

#### Frontend

- [ ] **4I.4** Billing pages
  - [ ] `Settings/Billing.razor` - current plan, usage
  - [ ] `Settings/Upgrade.razor` - plan comparison
  - [ ] `Organizations/{id}/Billing.razor` - org billing

- [ ] **4I.5** Billing components
  - [ ] `PlanCard.razor` - plan feature comparison
  - [ ] `UsageMeter.razor` - chars used this month
  - [ ] `PaymentHistory.razor` - invoice list

### Stripe Webhook Events

```csharp
// Handle in /api/webhooks/stripe
switch (stripeEvent.Type)
{
    case "checkout.session.completed":
        // Activate subscription
        break;
    case "customer.subscription.updated":
        // Plan change
        break;
    case "customer.subscription.deleted":
        // Downgrade to free
        break;
    case "invoice.payment_failed":
        // Send warning email
        break;
}
```

### Database Schema (Billing)

```sql
CREATE TABLE subscriptions (
    id SERIAL PRIMARY KEY,
    user_id INT REFERENCES users(id),
    organization_id INT REFERENCES organizations(id),
    stripe_customer_id VARCHAR(255) NOT NULL,
    stripe_subscription_id VARCHAR(255),
    plan VARCHAR(50) NOT NULL DEFAULT 'free',
    status VARCHAR(50) NOT NULL DEFAULT 'active',
    current_period_start TIMESTAMPTZ,
    current_period_end TIMESTAMPTZ,
    cancel_at_period_end BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    CONSTRAINT chk_subscription_owner CHECK (
        (user_id IS NOT NULL AND organization_id IS NULL) OR
        (user_id IS NULL AND organization_id IS NOT NULL)
    )
);

CREATE TABLE payment_history (
    id SERIAL PRIMARY KEY,
    subscription_id INT NOT NULL REFERENCES subscriptions(id),
    stripe_invoice_id VARCHAR(255) NOT NULL,
    amount_cents INT NOT NULL,
    currency VARCHAR(3) DEFAULT 'usd',
    status VARCHAR(50) NOT NULL,
    invoice_url TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW()
);
```

---

## Start Here

To begin implementation, run:

```bash
cd /home/nick/source/LocalizationManager/cloud/src/LrmCloud.Web
dotnet add package MudBlazor
dotnet add package Blazored.LocalStorage
```

Then start with Phase 4A.1 (MudBlazor configuration).

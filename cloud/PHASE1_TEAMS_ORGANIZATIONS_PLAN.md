# Phase 1: Teams & Organizations Implementation Plan

## Overview

Complete the Teams & Organizations functionality for Phase 1, enabling:
- Multi-tenant architecture with organizations
- Team collaboration with role-based access control
- Member invitations and management
- Organization ownership and deletion

## Progress Tracker

### ✅ Prerequisites (Completed)
- [x] Authentication system fully implemented
- [x] User management complete
- [x] Email service configured

### 📋 To Do
- [ ] **Step 1: Database Schema & Entities**
- [ ] **Step 2: Organization CRUD Service**
- [ ] **Step 3: Organization API Endpoints**
- [ ] **Step 4: Member Management**
- [ ] **Step 5: Role-Based Access Control**
- [ ] **Step 6: Invitation System**
- [ ] **Step 7: Unit Tests**

---

## Step 1: Database Schema & Entities

### Database Tables

#### Organizations Table
```sql
CREATE TABLE organizations (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    slug VARCHAR(255) UNIQUE NOT NULL,  -- URL-friendly identifier
    description TEXT,
    owner_id INTEGER NOT NULL REFERENCES users(id),
    plan VARCHAR(50) NOT NULL DEFAULT 'free',  -- free, team, enterprise
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
    deleted_at TIMESTAMP
);

CREATE INDEX idx_organizations_owner_id ON organizations(owner_id);
CREATE INDEX idx_organizations_slug ON organizations(slug);
CREATE INDEX idx_organizations_deleted_at ON organizations(deleted_at);
```

#### Organization Members Table (Junction)
```sql
CREATE TABLE organization_members (
    id SERIAL PRIMARY KEY,
    organization_id INTEGER NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    role VARCHAR(50) NOT NULL DEFAULT 'member',  -- owner, admin, member, viewer
    invited_by INTEGER REFERENCES users(id),
    invited_at TIMESTAMP NOT NULL DEFAULT NOW(),
    joined_at TIMESTAMP,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),

    UNIQUE(organization_id, user_id)  -- User can only be in org once
);

CREATE INDEX idx_organization_members_org_id ON organization_members(organization_id);
CREATE INDEX idx_organization_members_user_id ON organization_members(user_id);
```

#### Pending Invitations Table
```sql
CREATE TABLE organization_invitations (
    id SERIAL PRIMARY KEY,
    organization_id INTEGER NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    email VARCHAR(255) NOT NULL,
    role VARCHAR(50) NOT NULL DEFAULT 'member',
    token_hash VARCHAR(255) NOT NULL,  -- BCrypt hashed invitation token
    invited_by INTEGER NOT NULL REFERENCES users(id),
    expires_at TIMESTAMP NOT NULL,  -- 7 days from creation
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    accepted_at TIMESTAMP,

    UNIQUE(organization_id, email)  -- Can't invite same email twice to same org
);

CREATE INDEX idx_organization_invitations_email ON organization_invitations(email);
CREATE INDEX idx_organization_invitations_token_hash ON organization_invitations(token_hash);
```

### Entity Classes

**Organization.cs** (LrmCloud.Shared/Entities/)
```csharp
[Table("organizations")]
public class Organization
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("name")]
    public required string Name { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("slug")]
    public required string Slug { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Required]
    [Column("owner_id")]
    public int OwnerId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("plan")]
    public string Plan { get; set; } = "free";

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Required]
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public User Owner { get; set; } = null!;
    public ICollection<OrganizationMember> Members { get; set; } = new List<OrganizationMember>();
}
```

**OrganizationMember.cs**
```csharp
[Table("organization_members")]
public class OrganizationMember
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("organization_id")]
    public int OrganizationId { get; set; }

    [Required]
    [Column("user_id")]
    public int UserId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("role")]
    public string Role { get; set; } = OrganizationRole.Member;

    [Column("invited_by")]
    public int? InvitedBy { get; set; }

    [Required]
    [Column("invited_at")]
    public DateTime InvitedAt { get; set; }

    [Column("joined_at")]
    public DateTime? JoinedAt { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Required]
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Organization Organization { get; set; } = null!;
    public User User { get; set; } = null!;
    public User? Inviter { get; set; }
}
```

**OrganizationInvitation.cs**
```csharp
[Table("organization_invitations")]
public class OrganizationInvitation
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("organization_id")]
    public int OrganizationId { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("email")]
    public required string Email { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("role")]
    public string Role { get; set; } = OrganizationRole.Member;

    [Required]
    [MaxLength(255)]
    [Column("token_hash")]
    public required string TokenHash { get; set; }

    [Required]
    [Column("invited_by")]
    public int InvitedBy { get; set; }

    [Required]
    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("accepted_at")]
    public DateTime? AcceptedAt { get; set; }

    // Navigation properties
    public Organization Organization { get; set; } = null!;
    public User Inviter { get; set; } = null!;
}
```

**OrganizationRole.cs** (Constants)
```csharp
public static class OrganizationRole
{
    public const string Owner = "owner";
    public const string Admin = "admin";
    public const string Member = "member";
    public const string Viewer = "viewer";
}
```

### Migration
```bash
dotnet ef migrations add AddOrganizationsAndTeams
```

---

## Step 2: Organization CRUD Service

**IOrganizationService.cs**
```csharp
public interface IOrganizationService
{
    // Organization CRUD
    Task<(bool Success, OrganizationDto? Organization, string? ErrorMessage)> CreateOrganizationAsync(int userId, CreateOrganizationRequest request);
    Task<OrganizationDto?> GetOrganizationAsync(int organizationId, int userId);
    Task<List<OrganizationDto>> GetUserOrganizationsAsync(int userId);
    Task<(bool Success, OrganizationDto? Organization, string? ErrorMessage)> UpdateOrganizationAsync(int organizationId, int userId, UpdateOrganizationRequest request);
    Task<(bool Success, string? ErrorMessage)> DeleteOrganizationAsync(int organizationId, int userId);

    // Member Management
    Task<List<OrganizationMemberDto>> GetMembersAsync(int organizationId, int userId);
    Task<(bool Success, string? ErrorMessage)> InviteMemberAsync(int organizationId, int userId, InviteMemberRequest request);
    Task<(bool Success, string? ErrorMessage)> AcceptInvitationAsync(int userId, string token);
    Task<(bool Success, string? ErrorMessage)> RemoveMemberAsync(int organizationId, int userId, int memberUserId);
    Task<(bool Success, string? ErrorMessage)> UpdateMemberRoleAsync(int organizationId, int userId, int memberUserId, string newRole);

    // Authorization Helpers
    Task<bool> IsOwnerAsync(int organizationId, int userId);
    Task<bool> IsAdminOrOwnerAsync(int organizationId, int userId);
    Task<bool> IsMemberAsync(int organizationId, int userId);
    Task<string?> GetUserRoleAsync(int organizationId, int userId);
}
```

**Key Business Rules:**
1. Organization slug must be unique and URL-friendly
2. Owner automatically gets "owner" role
3. Owner cannot be removed from organization
4. Owner can delete organization (soft delete)
5. Only owner can change organization plan
6. Admin can invite/remove members (except owner)
7. Member and Viewer can only read
8. Invitations expire after 7 days
9. Can't invite existing member
10. Email sent for all invitations

---

## Step 3: Organization API Endpoints

**OrganizationsController.cs**

```csharp
[ApiController]
[Route("api/organizations")]
[Authorize]
public class OrganizationsController : ApiControllerBase
{
    // Organization CRUD
    GET    /api/organizations                     // List user's organizations
    POST   /api/organizations                     // Create organization
    GET    /api/organizations/{id}                // Get organization details
    PUT    /api/organizations/{id}                // Update organization (owner only)
    DELETE /api/organizations/{id}                // Delete organization (owner only)

    // Member Management
    GET    /api/organizations/{id}/members        // List members (member+ required)
    POST   /api/organizations/{id}/members        // Invite member (admin+ required)
    DELETE /api/organizations/{id}/members/{uid}  // Remove member (admin+ required, can't remove owner)
    PUT    /api/organizations/{id}/members/{uid}  // Update role (owner only)

    // Invitations
    POST   /api/organizations/accept-invitation   // Accept invitation (public with token)
}
```

---

## Step 4: Member Management

### Invitation Flow

1. **Admin invites user by email**
   - Check email not already a member
   - Check no pending invitation exists
   - Generate secure token (32 bytes, BCrypt hashed)
   - Create invitation record
   - Send invitation email

2. **Invited user receives email**
   - Email contains acceptance link with token
   - Link: `https://lrm.cloud/accept-invitation?token=xyz`

3. **User clicks link**
   - If not logged in: redirect to login/register with return URL
   - If logged in: verify token and add to organization
   - Delete invitation record
   - Send welcome email to new member
   - Notify inviter

### Email Templates

**Invitation Email**
```html
Subject: You've been invited to join {OrgName} on LRM Cloud

Hello!

{InviterName} has invited you to join {OrgName} on LRM Cloud as a {Role}.

Click here to accept: [Accept Invitation]

This invitation expires in 7 days.

If you don't have an account yet, you'll be able to create one when you click the link.
```

**Welcome Email** (after accepting)
```html
Subject: Welcome to {OrgName}!

You've successfully joined {OrgName} as a {Role}.

You can now:
- View projects
- Collaborate with team members
- [Additional permissions based on role]

Start collaborating: [Go to Dashboard]
```

---

## Step 5: Role-Based Access Control

### Role Hierarchy

| Role | Permissions |
|------|------------|
| **Owner** | Everything (including delete org, change plan, change owner) |
| **Admin** | Manage members, manage projects, edit translations |
| **Member** | View projects, edit translations |
| **Viewer** | View projects (read-only) |

### Authorization Policies

**Startup.cs / Program.cs**
```csharp
services.AddAuthorization(options =>
{
    options.AddPolicy("OrganizationOwner", policy =>
        policy.Requirements.Add(new OrganizationRoleRequirement(OrganizationRole.Owner)));

    options.AddPolicy("OrganizationAdmin", policy =>
        policy.Requirements.Add(new OrganizationRoleRequirement(OrganizationRole.Admin)));

    options.AddPolicy("OrganizationMember", policy =>
        policy.Requirements.Add(new OrganizationRoleRequirement(OrganizationRole.Member)));
});
```

### Authorization Handler

**OrganizationRoleHandler.cs**
```csharp
public class OrganizationRoleHandler : AuthorizationHandler<OrganizationRoleRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OrganizationRoleRequirement requirement)
    {
        // Extract organization ID from route
        // Check user's role in organization
        // Succeed if role meets requirement
    }
}
```

---

## Step 6: Invitation System

### Invitation Token Security
- 32-byte cryptographically secure random token
- BCrypt hashed before storage (work factor 12)
- Single-use tokens (deleted after acceptance)
- 7-day expiration
- Email verification required

### Invitation Lifecycle
1. Created → Pending
2. Accepted → Member added, invitation deleted
3. Expired → Soft delete or cleanup job removes
4. Cancelled → Admin can revoke pending invitations

---

## Step 7: Unit Tests

### OrganizationServiceTests

**Organization CRUD Tests:**
- `CreateOrganization_ValidRequest_CreatesOrg`
- `CreateOrganization_DuplicateSlug_ReturnsError`
- `CreateOrganization_AutomaticallyAddsOwner`
- `GetUserOrganizations_ReturnsOnlyUserOrgs`
- `UpdateOrganization_NonOwner_ReturnsError`
- `DeleteOrganization_Owner_SoftDeletes`
- `DeleteOrganization_NonOwner_ReturnsError`

**Member Management Tests:**
- `InviteMember_ValidEmail_SendsInvitation`
- `InviteMember_ExistingMember_ReturnsError`
- `InviteMember_PendingInvitation_ReturnsError`
- `AcceptInvitation_ValidToken_AddsMember`
- `AcceptInvitation_ExpiredToken_ReturnsError`
- `RemoveMember_Admin_RemovesMember`
- `RemoveMember_CannotRemoveOwner`
- `UpdateMemberRole_Owner_UpdatesRole`
- `UpdateMemberRole_NonOwner_ReturnsError`

**Authorization Tests:**
- `IsOwner_ReturnsCorrectly`
- `IsAdminOrOwner_ReturnsCorrectly`
- `IsMember_ReturnsCorrectly`
- `GetUserRole_ReturnsCorrectRole`

---

## DTOs Required

### Request DTOs

**CreateOrganizationRequest.cs**
```csharp
public class CreateOrganizationRequest
{
    [Required(ErrorMessage = "Organization name is required")]
    [MaxLength(255)]
    public string Name { get; set; } = null!;

    [MaxLength(255)]
    public string? Slug { get; set; }  // Optional, auto-generated from name if not provided

    [MaxLength(1000)]
    public string? Description { get; set; }
}
```

**UpdateOrganizationRequest.cs**
```csharp
public class UpdateOrganizationRequest
{
    [MaxLength(255)]
    public string? Name { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }
}
```

**InviteMemberRequest.cs**
```csharp
public class InviteMemberRequest
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public string Role { get; set; } = OrganizationRole.Member;
}
```

**AcceptInvitationRequest.cs**
```csharp
public class AcceptInvitationRequest
{
    [Required(ErrorMessage = "Invitation token is required")]
    public string Token { get; set; } = null!;
}
```

**UpdateMemberRoleRequest.cs**
```csharp
public class UpdateMemberRoleRequest
{
    [Required(ErrorMessage = "Role is required")]
    [MaxLength(50)]
    public string Role { get; set; } = null!;
}
```

### Response DTOs

**OrganizationDto.cs**
```csharp
public class OrganizationDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string? Description { get; set; }
    public int OwnerId { get; set; }
    public string Plan { get; set; } = null!;
    public int MemberCount { get; set; }
    public string UserRole { get; set; } = null!;  // Current user's role in this org
    public DateTime CreatedAt { get; set; }
}
```

**OrganizationMemberDto.cs**
```csharp
public class OrganizationMemberDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Email { get; set; } = null!;
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public string Role { get; set; } = null!;
    public DateTime JoinedAt { get; set; }
    public string? InvitedBy { get; set; }  // Username of inviter
}
```

---

## Security Considerations

1. **Authorization Checks**
   - Every endpoint must verify user's permission
   - Use `[Authorize(Policy = "OrganizationOwner")]` attributes
   - Double-check in service layer

2. **Slug Generation**
   - Auto-generate from name if not provided
   - Make URL-friendly (lowercase, hyphens, no special chars)
   - Ensure uniqueness with retry logic

3. **Invitation Tokens**
   - Cryptographically secure random generation
   - BCrypt hashed storage
   - Single-use enforcement
   - Expiration enforcement

4. **Soft Deletes**
   - Organizations soft deleted (can be recovered)
   - Cascade delete members on org deletion
   - Keep audit trail

5. **Rate Limiting**
   - Limit invitation emails (max 10/hour per org)
   - Prevent invitation spam

---

## Success Criteria

Teams & Organizations is complete when:
- [x] All database tables created
- [x] All entities defined with proper relationships
- [x] Organization CRUD fully functional
- [x] Member invitation system working
- [x] Role-based authorization enforced
- [x] All unit tests passing
- [x] Email notifications sent correctly
- [x] Security best practices followed

---

## Estimated Complexity

| Component | Complexity | Estimated Time |
|-----------|-----------|----------------|
| Database Schema + Entities | Medium | 2 hours |
| Organization CRUD | Medium | 3 hours |
| Member Management | High | 4 hours |
| Invitation System | Medium | 3 hours |
| Role-Based Access Control | High | 4 hours |
| Unit Tests | Medium | 3 hours |

**Total: ~19 hours**

---

## Notes

- This sets up the foundation for multi-tenancy
- Projects (Phase 2) will belong to organizations
- All future features will use organization context
- Consider adding organization settings (billing, quotas) in future

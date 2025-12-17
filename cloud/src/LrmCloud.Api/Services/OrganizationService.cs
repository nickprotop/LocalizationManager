using LrmCloud.Api.Data;
using LrmCloud.Api.Helpers;
using LrmCloud.Shared.Configuration;
using LrmCloud.Shared.Constants;
using LrmCloud.Shared.DTOs.Organizations;
using LrmCloud.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Scriban;
using System.Text.RegularExpressions;

namespace LrmCloud.Api.Services;

public class OrganizationService : IOrganizationService
{
    private readonly AppDbContext _db;
    private readonly IMailService _mailService;
    private readonly CloudConfiguration _config;
    private readonly ILogger<OrganizationService> _logger;

    public OrganizationService(
        AppDbContext db,
        IMailService mailService,
        CloudConfiguration config,
        ILogger<OrganizationService> logger)
    {
        _db = db;
        _mailService = mailService;
        _config = config;
        _logger = logger;
    }

    // ============================================================
    // Organization CRUD
    // ============================================================

    public async Task<(bool Success, OrganizationDto? Organization, string? ErrorMessage)> CreateOrganizationAsync(
        int userId, CreateOrganizationRequest request)
    {
        try
        {
            // Generate slug if not provided
            var slug = string.IsNullOrWhiteSpace(request.Slug)
                ? GenerateSlug(request.Name)
                : NormalizeSlug(request.Slug);

            // Check for duplicate slug
            var existingOrg = await _db.Organizations
                .FirstOrDefaultAsync(o => o.Slug == slug);

            if (existingOrg != null)
            {
                // Try with a random suffix
                slug = $"{slug}-{Guid.NewGuid().ToString()[..8]}";

                // Check again
                existingOrg = await _db.Organizations
                    .FirstOrDefaultAsync(o => o.Slug == slug);

                if (existingOrg != null)
                {
                    return (false, null, "Organization slug is already in use. Please try a different name.");
                }
            }

            // Create organization - organizations always start on team plan
            // (free tier doesn't support team features)
            var organization = new Organization
            {
                Name = request.Name,
                Slug = slug,
                Description = request.Description,
                OwnerId = userId,
                Plan = "team",
                TranslationCharsLimit = _config.Limits.TeamTranslationChars,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Organizations.Add(organization);
            await _db.SaveChangesAsync();

            // Automatically add owner as member with owner role
            var ownerMembership = new OrganizationMember
            {
                OrganizationId = organization.Id,
                UserId = userId,
                Role = OrganizationRole.Owner,
                InvitedAt = DateTime.UtcNow,
                JoinedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _db.OrganizationMembers.Add(ownerMembership);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Organization created: {OrgId} by user {UserId}", organization.Id, userId);

            // Return DTO
            var dto = new OrganizationDto
            {
                Id = organization.Id,
                Name = organization.Name,
                Slug = organization.Slug,
                Description = organization.Description,
                OwnerId = organization.OwnerId,
                Plan = organization.Plan,
                MemberCount = 1,
                UserRole = OrganizationRole.Owner,
                CreatedAt = organization.CreatedAt
            };

            return (true, dto, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating organization for user {UserId}", userId);
            return (false, null, "An error occurred while creating the organization.");
        }
    }

    public async Task<OrganizationDto?> GetOrganizationAsync(int organizationId, int userId)
    {
        var org = await _db.Organizations
            .Include(o => o.Members)
            .FirstOrDefaultAsync(o => o.Id == organizationId);

        if (org == null)
        {
            return null;
        }

        // Check if user is a member
        var membership = org.Members.FirstOrDefault(m => m.UserId == userId);
        if (membership == null)
        {
            return null;
        }

        return new OrganizationDto
        {
            Id = org.Id,
            Name = org.Name,
            Slug = org.Slug,
            Description = org.Description,
            OwnerId = org.OwnerId,
            Plan = org.Plan,
            MemberCount = org.Members.Count,
            UserRole = membership.Role,
            CreatedAt = org.CreatedAt
        };
    }

    public async Task<List<OrganizationDto>> GetUserOrganizationsAsync(int userId)
    {
        var memberships = await _db.OrganizationMembers
            .Include(m => m.Organization)
                .ThenInclude(o => o!.Members)
            .Where(m => m.UserId == userId)
            .ToListAsync();

        return memberships.Select(m => new OrganizationDto
        {
            Id = m.Organization!.Id,
            Name = m.Organization.Name,
            Slug = m.Organization.Slug,
            Description = m.Organization.Description,
            OwnerId = m.Organization.OwnerId,
            Plan = m.Organization.Plan,
            MemberCount = m.Organization.Members.Count,
            UserRole = m.Role,
            CreatedAt = m.Organization.CreatedAt
        }).ToList();
    }

    public async Task<(bool Success, OrganizationDto? Organization, string? ErrorMessage)> UpdateOrganizationAsync(
        int organizationId, int userId, UpdateOrganizationRequest request)
    {
        try
        {
            // Check if user is owner
            if (!await IsOwnerAsync(organizationId, userId))
            {
                return (false, null, "Only the organization owner can update organization details.");
            }

            var org = await _db.Organizations
                .Include(o => o.Members)
                .FirstOrDefaultAsync(o => o.Id == organizationId);

            if (org == null)
            {
                return (false, null, "Organization not found.");
            }

            // Update fields
            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                org.Name = request.Name;
            }

            if (request.Description != null)
            {
                org.Description = request.Description;
            }

            org.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            _logger.LogInformation("Organization {OrgId} updated by user {UserId}", organizationId, userId);

            var dto = new OrganizationDto
            {
                Id = org.Id,
                Name = org.Name,
                Slug = org.Slug,
                Description = org.Description,
                OwnerId = org.OwnerId,
                Plan = org.Plan,
                MemberCount = org.Members.Count,
                UserRole = OrganizationRole.Owner,
                CreatedAt = org.CreatedAt
            };

            return (true, dto, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating organization {OrgId}", organizationId);
            return (false, null, "An error occurred while updating the organization.");
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> DeleteOrganizationAsync(int organizationId, int userId)
    {
        try
        {
            // Check if user is owner
            if (!await IsOwnerAsync(organizationId, userId))
            {
                return (false, "Only the organization owner can delete the organization.");
            }

            var org = await _db.Organizations
                .FirstOrDefaultAsync(o => o.Id == organizationId);

            if (org == null)
            {
                return (false, "Organization not found.");
            }

            // Soft delete
            org.DeletedAt = DateTime.UtcNow;
            org.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            _logger.LogInformation("Organization {OrgId} soft deleted by user {UserId}", organizationId, userId);

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting organization {OrgId}", organizationId);
            return (false, "An error occurred while deleting the organization.");
        }
    }

    // ============================================================
    // Member Management
    // ============================================================

    public async Task<List<OrganizationMemberDto>> GetMembersAsync(int organizationId, int userId)
    {
        // Check if user is a member
        if (!await IsMemberAsync(organizationId, userId))
            return new List<OrganizationMemberDto>();

        var members = await _db.OrganizationMembers
            .Include(m => m.User)
            .Include(m => m.InvitedBy)
            .Where(m => m.OrganizationId == organizationId)
            .OrderBy(m => m.Role == OrganizationRole.Owner ? 0 : 1)
            .ThenBy(m => m.CreatedAt)
            .ToListAsync();

        return members.Select(m => new OrganizationMemberDto
        {
            Id = m.Id,
            UserId = m.UserId,
            Email = m.User!.Email!,
            Username = m.User.Username,
            DisplayName = m.User.DisplayName,
            AvatarUrl = m.User.AvatarUrl,
            Role = m.Role,
            JoinedAt = m.JoinedAt,
            InvitedByUsername = m.InvitedBy?.Username
        }).ToList();
    }

    public async Task<(bool Success, string? ErrorMessage)> InviteMemberAsync(
        int organizationId, int userId, InviteMemberRequest request)
    {
        try
        {
            // Check if user is admin or owner
            if (!await IsAdminOrOwnerAsync(organizationId, userId))
            {
                return (false, "Only organization admins and owners can invite members.");
            }

            // Validate role
            if (!OrganizationRole.IsValid(request.Role))
            {
                return (false, "Invalid role specified.");
            }

            // Don't allow inviting as owner
            if (request.Role == OrganizationRole.Owner)
            {
                return (false, "Cannot invite someone as owner. Transfer ownership instead.");
            }

            var org = await _db.Organizations
                .Include(o => o.Members)
                .FirstOrDefaultAsync(o => o.Id == organizationId);

            if (org == null)
            {
                return (false, "Organization not found.");
            }

            // Check team member limit based on organization's plan
            var maxMembers = _config.Limits.GetMaxTeamMembers(org.Plan);
            var currentMemberCount = org.Members.Count;
            var pendingInvitationCount = await _db.OrganizationInvitations
                .CountAsync(i => i.OrganizationId == organizationId &&
                                 i.AcceptedAt == null &&
                                 i.ExpiresAt > DateTime.UtcNow);

            var totalPotentialMembers = currentMemberCount + pendingInvitationCount;

            if (totalPotentialMembers >= maxMembers)
            {
                _logger.LogWarning(
                    "Organization {OrgId} has reached team member limit ({CurrentCount} members + {PendingCount} pending / {MaxMembers} max) for plan {Plan}",
                    organizationId, currentMemberCount, pendingInvitationCount, maxMembers, org.Plan);
                return (false, $"Team member limit reached ({totalPotentialMembers}/{maxMembers}). Upgrade your organization's plan to invite more members.");
            }

            var normalizedEmail = request.Email.ToLowerInvariant();

            // Check if user is already a member
            var existingMember = await _db.OrganizationMembers
                .Include(m => m.User)
                .FirstOrDefaultAsync(m =>
                    m.OrganizationId == organizationId &&
                    m.User!.Email == normalizedEmail);

            if (existingMember != null)
            {
                return (false, "This user is already a member of the organization.");
            }

            // Check if there's a pending invitation
            var existingInvitation = await _db.OrganizationInvitations
                .FirstOrDefaultAsync(i =>
                    i.OrganizationId == organizationId &&
                    i.Email == normalizedEmail &&
                    i.AcceptedAt == null &&
                    i.ExpiresAt > DateTime.UtcNow);

            if (existingInvitation != null)
            {
                return (false, "There is already a pending invitation for this email address.");
            }

            // Generate invitation token
            var invitationToken = TokenGenerator.GenerateSecureToken(32);
            var tokenHash = BCrypt.Net.BCrypt.HashPassword(invitationToken, 12);

            // Create invitation
            var invitation = new OrganizationInvitation
            {
                OrganizationId = organizationId,
                Email = normalizedEmail,
                Role = request.Role,
                TokenHash = tokenHash,
                InvitedBy = userId,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow
            };

            _db.OrganizationInvitations.Add(invitation);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Invitation created for {Email} to organization {OrgId}", normalizedEmail, organizationId);

            // Send invitation email
            await SendInvitationEmailAsync(org, invitation, invitationToken, userId);

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inviting member to organization {OrgId}", organizationId);
            return (false, "An error occurred while sending the invitation.");
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> AcceptInvitationAsync(int userId, string token)
    {
        try
        {
            // Find invitations for the user's email
            var user = await _db.Users.FindAsync(userId);
            if (user == null || string.IsNullOrEmpty(user.Email))
            {
                return (false, "User not found.");
            }

            var invitations = await _db.OrganizationInvitations
                .Include(i => i.Organization)
                .Where(i =>
                    i.Email == user.Email.ToLowerInvariant() &&
                    i.AcceptedAt == null &&
                    i.ExpiresAt > DateTime.UtcNow)
                .ToListAsync();

            if (invitations.Count == 0)
            {
                return (false, "No valid invitation found.");
            }

            // Find the invitation that matches the token
            OrganizationInvitation? matchingInvitation = null;
            foreach (var inv in invitations)
            {
                if (BCrypt.Net.BCrypt.Verify(token, inv.TokenHash))
                {
                    matchingInvitation = inv;
                    break;
                }
            }

            if (matchingInvitation == null)
            {
                return (false, "Invalid or expired invitation token.");
            }

            // Check if already a member (race condition protection)
            var existingMember = await _db.OrganizationMembers
                .FirstOrDefaultAsync(m =>
                    m.OrganizationId == matchingInvitation.OrganizationId &&
                    m.UserId == userId);

            if (existingMember != null)
            {
                // Already a member, just mark invitation as accepted
                matchingInvitation.AcceptedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                return (false, "You are already a member of this organization.");
            }

            // Check team member limit (race condition protection)
            var org = await _db.Organizations
                .Include(o => o.Members)
                .FirstOrDefaultAsync(o => o.Id == matchingInvitation.OrganizationId);

            if (org != null)
            {
                var maxMembers = _config.Limits.GetMaxTeamMembers(org.Plan);
                if (org.Members.Count >= maxMembers)
                {
                    _logger.LogWarning(
                        "Organization {OrgId} has reached team member limit ({CurrentCount}/{MaxMembers}) when accepting invitation",
                        matchingInvitation.OrganizationId, org.Members.Count, maxMembers);
                    return (false, "This organization has reached its team member limit. Please contact the organization owner.");
                }
            }

            // Add user as member
            var member = new OrganizationMember
            {
                OrganizationId = matchingInvitation.OrganizationId,
                UserId = userId,
                Role = matchingInvitation.Role,
                InvitedById = matchingInvitation.InvitedBy,
                InvitedAt = matchingInvitation.CreatedAt,
                JoinedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _db.OrganizationMembers.Add(member);

            // Mark invitation as accepted
            matchingInvitation.AcceptedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            _logger.LogInformation("User {UserId} accepted invitation to organization {OrgId}",
                userId, matchingInvitation.OrganizationId);

            // Send welcome email
            await SendWelcomeEmailAsync(user, matchingInvitation.Organization!, matchingInvitation.Role);

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accepting invitation for user {UserId}", userId);
            return (false, "An error occurred while accepting the invitation.");
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> DeclineInvitationAsync(int userId, string token)
    {
        try
        {
            // Find invitations for the user's email
            var user = await _db.Users.FindAsync(userId);
            if (user == null || string.IsNullOrEmpty(user.Email))
            {
                return (false, "User not found.");
            }

            var invitations = await _db.OrganizationInvitations
                .Where(i =>
                    i.Email == user.Email.ToLowerInvariant() &&
                    i.AcceptedAt == null &&
                    i.ExpiresAt > DateTime.UtcNow)
                .ToListAsync();

            if (invitations.Count == 0)
            {
                return (false, "No valid invitation found.");
            }

            // Find the invitation that matches the token
            OrganizationInvitation? matchingInvitation = null;
            foreach (var inv in invitations)
            {
                if (BCrypt.Net.BCrypt.Verify(token, inv.TokenHash))
                {
                    matchingInvitation = inv;
                    break;
                }
            }

            if (matchingInvitation == null)
            {
                return (false, "Invalid or expired invitation token.");
            }

            // Delete the invitation (declined)
            _db.OrganizationInvitations.Remove(matchingInvitation);
            await _db.SaveChangesAsync();

            _logger.LogInformation("User {UserId} declined invitation to organization {OrgId}",
                userId, matchingInvitation.OrganizationId);

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error declining invitation for user {UserId}", userId);
            return (false, "An error occurred while declining the invitation.");
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> AcceptInvitationByIdAsync(int userId, int invitationId)
    {
        try
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null || string.IsNullOrEmpty(user.Email))
            {
                return (false, "User not found.");
            }

            // Find invitation by ID and verify it belongs to this user's email
            var invitation = await _db.OrganizationInvitations
                .Include(i => i.Organization)
                .FirstOrDefaultAsync(i =>
                    i.Id == invitationId &&
                    i.Email == user.Email.ToLowerInvariant() &&
                    i.AcceptedAt == null &&
                    i.ExpiresAt > DateTime.UtcNow);

            if (invitation == null)
            {
                return (false, "Invitation not found or has expired.");
            }

            // Check if already a member
            var existingMember = await _db.OrganizationMembers
                .FirstOrDefaultAsync(m =>
                    m.OrganizationId == invitation.OrganizationId &&
                    m.UserId == userId);

            if (existingMember != null)
            {
                invitation.AcceptedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                return (false, "You are already a member of this organization.");
            }

            // Check team member limit (race condition protection)
            var org = await _db.Organizations
                .Include(o => o.Members)
                .FirstOrDefaultAsync(o => o.Id == invitation.OrganizationId);

            if (org != null)
            {
                var maxMembers = _config.Limits.GetMaxTeamMembers(org.Plan);
                if (org.Members.Count >= maxMembers)
                {
                    _logger.LogWarning(
                        "Organization {OrgId} has reached team member limit ({CurrentCount}/{MaxMembers}) when accepting invitation by ID",
                        invitation.OrganizationId, org.Members.Count, maxMembers);
                    return (false, "This organization has reached its team member limit. Please contact the organization owner.");
                }
            }

            // Add user as member
            var member = new OrganizationMember
            {
                OrganizationId = invitation.OrganizationId,
                UserId = userId,
                Role = invitation.Role,
                InvitedById = invitation.InvitedBy,
                InvitedAt = invitation.CreatedAt,
                JoinedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _db.OrganizationMembers.Add(member);
            invitation.AcceptedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            _logger.LogInformation("User {UserId} accepted invitation {InvId} to organization {OrgId}",
                userId, invitationId, invitation.OrganizationId);

            // Send welcome email
            await SendWelcomeEmailAsync(user, invitation.Organization!, invitation.Role);

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accepting invitation {InvId} for user {UserId}", invitationId, userId);
            return (false, "An error occurred while accepting the invitation.");
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> DeclineInvitationByIdAsync(int userId, int invitationId)
    {
        try
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null || string.IsNullOrEmpty(user.Email))
            {
                return (false, "User not found.");
            }

            // Find invitation by ID and verify it belongs to this user's email
            var invitation = await _db.OrganizationInvitations
                .FirstOrDefaultAsync(i =>
                    i.Id == invitationId &&
                    i.Email == user.Email.ToLowerInvariant() &&
                    i.AcceptedAt == null &&
                    i.ExpiresAt > DateTime.UtcNow);

            if (invitation == null)
            {
                return (false, "Invitation not found or has expired.");
            }

            // Delete the invitation
            _db.OrganizationInvitations.Remove(invitation);
            await _db.SaveChangesAsync();

            _logger.LogInformation("User {UserId} declined invitation {InvId} to organization {OrgId}",
                userId, invitationId, invitation.OrganizationId);

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error declining invitation {InvId} for user {UserId}", invitationId, userId);
            return (false, "An error occurred while declining the invitation.");
        }
    }

    public async Task<List<PendingInvitationDto>> GetPendingInvitationsAsync(int userId)
    {
        try
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null || string.IsNullOrEmpty(user.Email))
                return new List<PendingInvitationDto>();

            var invitations = await _db.OrganizationInvitations
                .Include(i => i.Organization)
                .Include(i => i.Inviter)
                .Where(i =>
                    i.Email == user.Email.ToLowerInvariant() &&
                    i.AcceptedAt == null &&
                    i.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            // We need to return tokens in a way the frontend can use them
            // Since we store hashed tokens, we need to generate new ones for display
            // Actually, we should NOT return the actual token in the list -
            // instead we use the invitation ID and generate a new lookup token

            // For security, we'll return a hash-based identifier that can be used
            // to look up the invitation, but the actual acceptance needs the original token
            // which was sent via email.

            // Actually, looking at the design - the user gets the token via email link.
            // For the "pending invitations" list, we show them the invitations but
            // they need to use the email link to actually accept/decline.
            // Or we can generate a temporary token based on the invitation ID.

            // Simpler approach: Return invitation ID as the "token" since the user
            // is already authenticated with their email - we can verify the match.

            return invitations.Select(i => new PendingInvitationDto
            {
                Token = i.Id.ToString(), // Use invitation ID as lookup token for authenticated users
                OrganizationId = i.OrganizationId,
                OrganizationName = i.Organization?.Name ?? "Unknown",
                OrganizationSlug = i.Organization?.Slug ?? "",
                Role = i.Role,
                InvitedByEmail = i.Inviter?.Email,
                InvitedByName = i.Inviter?.DisplayName ?? i.Inviter?.Username,
                ExpiresAt = i.ExpiresAt,
                CreatedAt = i.CreatedAt
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending invitations for user {UserId}", userId);
            return new List<PendingInvitationDto>();
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> LeaveOrganizationAsync(int organizationId, int userId)
    {
        try
        {
            // Check if user is a member
            var member = await _db.OrganizationMembers
                .FirstOrDefaultAsync(m =>
                    m.OrganizationId == organizationId &&
                    m.UserId == userId);

            if (member == null)
            {
                return (false, "You are not a member of this organization.");
            }

            // Check if user is the owner
            var org = await _db.Organizations
                .FirstOrDefaultAsync(o => o.Id == organizationId);

            if (org == null)
            {
                return (false, "Organization not found.");
            }

            if (org.OwnerId == userId)
            {
                return (false, "The owner cannot leave the organization. Transfer ownership first or delete the organization.");
            }

            // Remove membership
            _db.OrganizationMembers.Remove(member);
            await _db.SaveChangesAsync();

            _logger.LogInformation("User {UserId} left organization {OrgId}", userId, organizationId);

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leaving organization {OrgId} for user {UserId}", organizationId, userId);
            return (false, "An error occurred while leaving the organization.");
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> RemoveMemberAsync(
        int organizationId, int userId, int memberUserId)
    {
        try
        {
            // Check if user is admin or owner
            if (!await IsAdminOrOwnerAsync(organizationId, userId))
            {
                return (false, "Only organization admins and owners can remove members.");
            }

            // Cannot remove the owner
            var org = await _db.Organizations
                .FirstOrDefaultAsync(o => o.Id == organizationId);

            if (org == null)
            {
                return (false, "Organization not found.");
            }

            if (org.OwnerId == memberUserId)
            {
                return (false, "Cannot remove the organization owner.");
            }

            // Find member
            var member = await _db.OrganizationMembers
                .FirstOrDefaultAsync(m =>
                    m.OrganizationId == organizationId &&
                    m.UserId == memberUserId);

            if (member == null)
            {
                return (false, "Member not found.");
            }

            _db.OrganizationMembers.Remove(member);
            await _db.SaveChangesAsync();

            _logger.LogInformation("User {MemberUserId} removed from organization {OrgId} by {UserId}",
                memberUserId, organizationId, userId);

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing member {MemberUserId} from organization {OrgId}",
                memberUserId, organizationId);
            return (false, "An error occurred while removing the member.");
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateMemberRoleAsync(
        int organizationId, int userId, int memberUserId, string newRole)
    {
        try
        {
            // Check if user is owner
            if (!await IsOwnerAsync(organizationId, userId))
            {
                return (false, "Only the organization owner can update member roles.");
            }

            // Validate role
            if (!OrganizationRole.IsValid(newRole))
            {
                return (false, "Invalid role specified.");
            }

            // Cannot change owner's role
            var org = await _db.Organizations
                .FirstOrDefaultAsync(o => o.Id == organizationId);

            if (org == null)
            {
                return (false, "Organization not found.");
            }

            if (org.OwnerId == memberUserId && newRole != OrganizationRole.Owner)
            {
                return (false, "Cannot change the owner's role. Transfer ownership first.");
            }

            // Cannot promote someone to owner (use transfer ownership instead)
            if (newRole == OrganizationRole.Owner)
            {
                return (false, "Cannot promote to owner. Use transfer ownership instead.");
            }

            // Find member
            var member = await _db.OrganizationMembers
                .FirstOrDefaultAsync(m =>
                    m.OrganizationId == organizationId &&
                    m.UserId == memberUserId);

            if (member == null)
            {
                return (false, "Member not found.");
            }

            member.Role = newRole;
            await _db.SaveChangesAsync();

            _logger.LogInformation("Member {MemberUserId} role updated to {NewRole} in organization {OrgId} by {UserId}",
                memberUserId, newRole, organizationId, userId);

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating role for member {MemberUserId} in organization {OrgId}",
                memberUserId, organizationId);
            return (false, "An error occurred while updating the member's role.");
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> TransferOwnershipAsync(
        int organizationId, int currentOwnerId, int newOwnerId)
    {
        try
        {
            // Verify current user is the owner
            var org = await _db.Organizations
                .FirstOrDefaultAsync(o => o.Id == organizationId);

            if (org == null)
            {
                return (false, "Organization not found.");
            }

            if (org.OwnerId != currentOwnerId)
            {
                return (false, "Only the current owner can transfer ownership.");
            }

            // Verify new owner is a member
            var newOwnerMember = await _db.OrganizationMembers
                .Include(m => m.User)
                .FirstOrDefaultAsync(m =>
                    m.OrganizationId == organizationId &&
                    m.UserId == newOwnerId);

            if (newOwnerMember == null)
            {
                return (false, "The new owner must be a member of the organization.");
            }

            // Get current owner's membership
            var currentOwnerMember = await _db.OrganizationMembers
                .FirstOrDefaultAsync(m =>
                    m.OrganizationId == organizationId &&
                    m.UserId == currentOwnerId);

            // Update organization owner
            org.OwnerId = newOwnerId;
            org.UpdatedAt = DateTime.UtcNow;

            // Update roles
            if (currentOwnerMember != null)
            {
                currentOwnerMember.Role = OrganizationRole.Admin; // Demote to admin
            }

            newOwnerMember.Role = OrganizationRole.Owner;

            await _db.SaveChangesAsync();

            _logger.LogInformation("Organization {OrgId} ownership transferred from user {OldOwner} to {NewOwner}",
                organizationId, currentOwnerId, newOwnerId);

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transferring ownership of organization {OrgId}", organizationId);
            return (false, "An error occurred while transferring ownership.");
        }
    }

    // ============================================================
    // Authorization Helpers
    // ============================================================

    public async Task<bool> IsOwnerAsync(int organizationId, int userId)
    {
        var org = await _db.Organizations
            .FirstOrDefaultAsync(o => o.Id == organizationId);

        return org?.OwnerId == userId;
    }

    public async Task<bool> IsAdminOrOwnerAsync(int organizationId, int userId)
    {
        var role = await GetUserRoleAsync(organizationId, userId);
        return role != null && OrganizationRole.IsAdminOrOwner(role);
    }

    public async Task<bool> IsMemberAsync(int organizationId, int userId)
    {
        var role = await GetUserRoleAsync(organizationId, userId);
        return role != null;
    }

    public async Task<string?> GetUserRoleAsync(int organizationId, int userId)
    {
        var member = await _db.OrganizationMembers
            .FirstOrDefaultAsync(m =>
                m.OrganizationId == organizationId &&
                m.UserId == userId);

        return member?.Role;
    }

    // ============================================================
    // Helper Methods
    // ============================================================

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    private static string GenerateSlug(string name)
    {
        // Convert to lowercase
        var slug = name.ToLowerInvariant();

        // Replace spaces with hyphens
        slug = Regex.Replace(slug, @"\s+", "-", RegexOptions.None, RegexTimeout);

        // Remove invalid characters
        slug = Regex.Replace(slug, @"[^a-z0-9\-]", "", RegexOptions.None, RegexTimeout);

        // Remove multiple consecutive hyphens
        slug = Regex.Replace(slug, @"\-{2,}", "-", RegexOptions.None, RegexTimeout);

        // Trim hyphens from start and end
        slug = slug.Trim('-');

        // Limit length
        if (slug.Length > 100)
        {
            slug = slug[..100].TrimEnd('-');
        }

        return slug;
    }

    private static string NormalizeSlug(string slug)
    {
        return GenerateSlug(slug);
    }

    private async Task SendInvitationEmailAsync(
        Organization org, OrganizationInvitation invitation, string plainToken, int invitedByUserId)
    {
        try
        {
            var inviter = await _db.Users.FindAsync(invitedByUserId);
            var invitationLink = $"{_config.Server.BaseUrl}/accept-invitation?token={plainToken}";

            var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "Email", "OrganizationInvitation.html");

            // Check if template exists, if not use simple fallback
            string html;
            if (File.Exists(templatePath))
            {
                var templateText = await File.ReadAllTextAsync(templatePath);
                var template = Template.Parse(templateText);
                html = await template.RenderAsync(new
                {
                    organization_name = org.Name,
                    inviter_name = inviter?.Username ?? "Someone",
                    role = invitation.Role,
                    invitation_link = invitationLink,
                    expires_days = 7
                });
            }
            else
            {
                html = $@"
<html>
<body>
<h2>You've been invited to join {org.Name}</h2>
<p>{inviter?.Username ?? "Someone"} has invited you to join {org.Name} on LRM Cloud as a {invitation.Role}.</p>
<p><a href=""{invitationLink}"">Accept Invitation</a></p>
<p>This invitation expires in 7 days.</p>
<p>If you don't have an account yet, you'll be able to create one when you click the link.</p>
</body>
</html>";
            }

            await _mailService.TrySendEmailAsync(_logger, 
                to: invitation.Email,
                subject: $"You've been invited to join {org.Name} on LRM Cloud",
                htmlBody: html
            );

            _logger.LogInformation("Invitation email sent to {Email} for organization {OrgId}", invitation.Email, org.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending invitation email to {Email}", invitation.Email);
        }
    }

    private async Task SendWelcomeEmailAsync(User user, Organization org, string role)
    {
        try
        {
            var dashboardLink = $"{_config.Server.BaseUrl}/dashboard";

            var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "Email", "OrganizationWelcome.html");

            // Check if template exists, if not use simple fallback
            string html;
            if (File.Exists(templatePath))
            {
                var templateText = await File.ReadAllTextAsync(templatePath);
                var template = Template.Parse(templateText);
                html = await template.RenderAsync(new
                {
                    username = user.Username,
                    organization_name = org.Name,
                    role = role,
                    dashboard_link = dashboardLink
                });
            }
            else
            {
                html = $@"
<html>
<body>
<h2>Welcome to {org.Name}!</h2>
<p>You've successfully joined {org.Name} as a {role}.</p>
<p><a href=""{dashboardLink}"">Go to Dashboard</a></p>
</body>
</html>";
            }

            await _mailService.TrySendEmailAsync(_logger, 
                to: user.Email!,
                subject: $"Welcome to {org.Name}!",
                htmlBody: html
            );

            _logger.LogInformation("Welcome email sent to {Email} for organization {OrgId}", user.Email, org.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending welcome email to {Email}", user.Email);
        }
    }
}

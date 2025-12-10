using LrmCloud.Api.Services;
using LrmCloud.Shared.Api;
using LrmCloud.Shared.DTOs.Organizations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LrmCloud.Api.Controllers;

/// <summary>
/// Controller for managing organizations and their members.
/// </summary>
[ApiController]
[Route("api/organizations")]
[Authorize]
public class OrganizationsController : ApiControllerBase
{
    private readonly IOrganizationService _organizationService;
    private readonly ILogger<OrganizationsController> _logger;

    public OrganizationsController(
        IOrganizationService organizationService,
        ILogger<OrganizationsController> logger)
    {
        _organizationService = organizationService;
        _logger = logger;
    }

    // ============================================================
    // Organization CRUD
    // ============================================================

    /// <summary>
    /// Get all organizations the current user is a member of
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<OrganizationDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<List<OrganizationDto>>>> GetUserOrganizations()
    {
        var userId = GetAuthenticatedUserId();
        if (!userId.HasValue)
            return Unauthorized(ErrorCodes.AUTH_UNAUTHORIZED, "User not authenticated");

        var organizations = await _organizationService.GetUserOrganizationsAsync(userId.Value);
        return Success(organizations);
    }

    /// <summary>
    /// Create a new organization
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<OrganizationDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<OrganizationDto>>> CreateOrganization(
        [FromBody] CreateOrganizationRequest request)
    {
        var userId = GetAuthenticatedUserId();
        if (!userId.HasValue)
            return Unauthorized(ErrorCodes.AUTH_UNAUTHORIZED, "User not authenticated");

        var (success, organization, errorMessage) =
            await _organizationService.CreateOrganizationAsync(userId.Value, request);

        if (!success || organization == null)
            return BadRequest(ErrorCodes.VAL_INVALID_INPUT, errorMessage ?? "Failed to create organization");

        return Created(nameof(GetOrganization), new { id = organization.Id }, organization);
    }

    /// <summary>
    /// Get a specific organization by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<OrganizationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<OrganizationDto>>> GetOrganization(int id)
    {
        var userId = GetAuthenticatedUserId();
        if (!userId.HasValue)
            return Unauthorized(ErrorCodes.AUTH_UNAUTHORIZED, "User not authenticated");

        var organization = await _organizationService.GetOrganizationAsync(id, userId.Value);

        if (organization == null)
            return NotFound(ErrorCodes.RES_NOT_FOUND, "Organization not found or you don't have access to it");

        return Success(organization);
    }

    /// <summary>
    /// Update an organization (owner only)
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResponse<OrganizationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<OrganizationDto>>> UpdateOrganization(
        int id,
        [FromBody] UpdateOrganizationRequest request)
    {
        var userId = GetAuthenticatedUserId();
        if (!userId.HasValue)
            return Unauthorized(ErrorCodes.AUTH_UNAUTHORIZED, "User not authenticated");

        var (success, organization, errorMessage) =
            await _organizationService.UpdateOrganizationAsync(id, userId.Value, request);

        if (!success)
        {
            if (errorMessage?.Contains("not found") == true)
                return NotFound(ErrorCodes.RES_NOT_FOUND, errorMessage);

            if (errorMessage?.Contains("owner") == true)
                return Forbidden(ErrorCodes.AUTH_FORBIDDEN, errorMessage);

            return BadRequest(ErrorCodes.VAL_INVALID_INPUT, errorMessage ?? "Failed to update organization");
        }

        return Success(organization!);
    }

    /// <summary>
    /// Transfer organization ownership to another member (owner only)
    /// </summary>
    [HttpPost("{id}/transfer")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse>> TransferOwnership(
        int id,
        [FromBody] TransferOwnershipRequest request)
    {
        var userId = GetAuthenticatedUserId();
        if (!userId.HasValue)
            return Unauthorized(ErrorCodes.AUTH_UNAUTHORIZED, "User not authenticated");

        var (success, errorMessage) =
            await _organizationService.TransferOwnershipAsync(id, userId.Value, request.NewOwnerId);

        if (!success)
        {
            if (errorMessage?.Contains("owner") == true)
                return Forbidden(ErrorCodes.AUTH_FORBIDDEN, errorMessage);

            return BadRequest(ErrorCodes.VAL_INVALID_INPUT, errorMessage ?? "Failed to transfer ownership");
        }

        return Success("Ownership transferred successfully");
    }

    /// <summary>
    /// Delete an organization (owner only, soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse>> DeleteOrganization(int id)
    {
        var userId = GetAuthenticatedUserId();
        if (!userId.HasValue)
            return Unauthorized(ErrorCodes.AUTH_UNAUTHORIZED, "User not authenticated");

        var (success, errorMessage) = await _organizationService.DeleteOrganizationAsync(id, userId.Value);

        if (!success)
        {
            if (errorMessage?.Contains("not found") == true)
                return NotFound(ErrorCodes.RES_NOT_FOUND, errorMessage);

            if (errorMessage?.Contains("owner") == true)
                return Forbidden(ErrorCodes.AUTH_FORBIDDEN, errorMessage);

            return BadRequest(ErrorCodes.VAL_INVALID_INPUT, errorMessage ?? "Failed to delete organization");
        }

        return Success("Organization deleted successfully");
    }

    // ============================================================
    // Member Management
    // ============================================================

    /// <summary>
    /// Get all members of an organization
    /// </summary>
    [HttpGet("{id}/members")]
    [ProducesResponseType(typeof(ApiResponse<List<OrganizationMemberDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<List<OrganizationMemberDto>>>> GetMembers(int id)
    {
        var userId = GetAuthenticatedUserId();
        if (!userId.HasValue)
            return Unauthorized(ErrorCodes.AUTH_UNAUTHORIZED, "User not authenticated");

        var members = await _organizationService.GetMembersAsync(id, userId.Value);

        // Empty list means user doesn't have access
        if (members.Count == 0)
        {
            // Check if organization exists
            var org = await _organizationService.GetOrganizationAsync(id, userId.Value);
            if (org == null)
                return Forbidden(ErrorCodes.AUTH_FORBIDDEN, "You don't have access to this organization");
        }

        return Success(members);
    }

    /// <summary>
    /// Invite a member to the organization (admin+ required)
    /// </summary>
    [HttpPost("{id}/members")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse>> InviteMember(
        int id,
        [FromBody] InviteMemberRequest request)
    {
        var userId = GetAuthenticatedUserId();
        if (!userId.HasValue)
            return Unauthorized(ErrorCodes.AUTH_UNAUTHORIZED, "User not authenticated");

        var (success, errorMessage) =
            await _organizationService.InviteMemberAsync(id, userId.Value, request);

        if (!success)
        {
            if (errorMessage?.Contains("admin") == true || errorMessage?.Contains("owner") == true)
                return Forbidden(ErrorCodes.AUTH_FORBIDDEN, errorMessage);

            return BadRequest(ErrorCodes.VAL_INVALID_INPUT, errorMessage ?? "Failed to invite member");
        }

        return Success("Invitation sent successfully");
    }

    /// <summary>
    /// Remove a member from the organization (admin+ required, cannot remove owner)
    /// </summary>
    [HttpDelete("{id}/members/{userId}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse>> RemoveMember(int id, int userId)
    {
        var currentUserId = GetAuthenticatedUserId();
        if (!currentUserId.HasValue)
            return Unauthorized(ErrorCodes.AUTH_UNAUTHORIZED, "User not authenticated");

        var (success, errorMessage) =
            await _organizationService.RemoveMemberAsync(id, currentUserId.Value, userId);

        if (!success)
        {
            if (errorMessage?.Contains("not found") == true)
                return NotFound(ErrorCodes.RES_NOT_FOUND, errorMessage);

            if (errorMessage?.Contains("admin") == true || errorMessage?.Contains("owner") == true)
                return Forbidden(ErrorCodes.AUTH_FORBIDDEN, errorMessage);

            return BadRequest(ErrorCodes.VAL_INVALID_INPUT, errorMessage ?? "Failed to remove member");
        }

        return Success("Member removed successfully");
    }

    /// <summary>
    /// Update a member's role in the organization (owner only)
    /// </summary>
    [HttpPut("{id}/members/{userId}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse>> UpdateMemberRole(
        int id,
        int userId,
        [FromBody] UpdateMemberRoleRequest request)
    {
        var currentUserId = GetAuthenticatedUserId();
        if (!currentUserId.HasValue)
            return Unauthorized(ErrorCodes.AUTH_UNAUTHORIZED, "User not authenticated");

        var (success, errorMessage) =
            await _organizationService.UpdateMemberRoleAsync(id, currentUserId.Value, userId, request.Role);

        if (!success)
        {
            if (errorMessage?.Contains("not found") == true)
                return NotFound(ErrorCodes.RES_NOT_FOUND, errorMessage);

            if (errorMessage?.Contains("owner") == true)
                return Forbidden(ErrorCodes.AUTH_FORBIDDEN, errorMessage);

            return BadRequest(ErrorCodes.VAL_INVALID_INPUT, errorMessage ?? "Failed to update member role");
        }

        return Success("Member role updated successfully");
    }

    // ============================================================
    // Invitation Management
    // ============================================================

    /// <summary>
    /// Get pending invitations for the current user
    /// </summary>
    [HttpGet("/api/invitations")]
    [ProducesResponseType(typeof(ApiResponse<List<PendingInvitationDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<List<PendingInvitationDto>>>> GetPendingInvitations()
    {
        var userId = GetAuthenticatedUserId();
        if (!userId.HasValue)
            return Unauthorized(ErrorCodes.AUTH_UNAUTHORIZED, "User not authenticated");

        var invitations = await _organizationService.GetPendingInvitationsAsync(userId.Value);
        return Success(invitations);
    }

    /// <summary>
    /// Accept an invitation to join an organization (by token from email)
    /// </summary>
    [HttpPost("accept-invitation")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse>> AcceptInvitation(
        [FromBody] AcceptInvitationRequest request)
    {
        var userId = GetAuthenticatedUserId();
        if (!userId.HasValue)
            return Unauthorized(ErrorCodes.AUTH_UNAUTHORIZED, "User not authenticated");

        var (success, errorMessage) =
            await _organizationService.AcceptInvitationAsync(userId.Value, request.Token);

        if (!success)
            return BadRequest(ErrorCodes.VAL_INVALID_INPUT, errorMessage ?? "Failed to accept invitation");

        return Success("Invitation accepted successfully");
    }

    /// <summary>
    /// Accept an invitation by ID (for authenticated users viewing their pending list)
    /// </summary>
    [HttpPost("/api/invitations/{invitationId}/accept")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse>> AcceptInvitationById(int invitationId)
    {
        var userId = GetAuthenticatedUserId();
        if (!userId.HasValue)
            return Unauthorized(ErrorCodes.AUTH_UNAUTHORIZED, "User not authenticated");

        var (success, errorMessage) =
            await _organizationService.AcceptInvitationByIdAsync(userId.Value, invitationId);

        if (!success)
            return BadRequest(ErrorCodes.VAL_INVALID_INPUT, errorMessage ?? "Failed to accept invitation");

        return Success("Invitation accepted successfully");
    }

    /// <summary>
    /// Decline an invitation by ID (for authenticated users viewing their pending list)
    /// </summary>
    [HttpPost("/api/invitations/{invitationId}/decline")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse>> DeclineInvitationById(int invitationId)
    {
        var userId = GetAuthenticatedUserId();
        if (!userId.HasValue)
            return Unauthorized(ErrorCodes.AUTH_UNAUTHORIZED, "User not authenticated");

        var (success, errorMessage) =
            await _organizationService.DeclineInvitationByIdAsync(userId.Value, invitationId);

        if (!success)
            return BadRequest(ErrorCodes.VAL_INVALID_INPUT, errorMessage ?? "Failed to decline invitation");

        return Success("Invitation declined successfully");
    }

    /// <summary>
    /// Leave an organization (non-owners only)
    /// </summary>
    [HttpPost("{id}/leave")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse>> LeaveOrganization(int id)
    {
        var userId = GetAuthenticatedUserId();
        if (!userId.HasValue)
            return Unauthorized(ErrorCodes.AUTH_UNAUTHORIZED, "User not authenticated");

        var (success, errorMessage) =
            await _organizationService.LeaveOrganizationAsync(id, userId.Value);

        if (!success)
            return BadRequest(ErrorCodes.VAL_INVALID_INPUT, errorMessage ?? "Failed to leave organization");

        return Success("You have left the organization");
    }

    // ============================================================
    // Helper Methods
    // ============================================================

    private int? GetAuthenticatedUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            return null;

        return userId;
    }
}

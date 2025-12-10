using System.Net.Http.Json;
using LrmCloud.Shared.Api;
using LrmCloud.Shared.DTOs.Organizations;

namespace LrmCloud.Web.Services;

/// <summary>
/// Frontend service for organization operations.
/// </summary>
public class OrganizationService
{
    private readonly HttpClient _http;

    public OrganizationService(HttpClient http)
    {
        _http = http;
    }

    // ============================================================
    // Organization CRUD
    // ============================================================

    public async Task<List<OrganizationDto>> GetOrganizationsAsync()
    {
        var response = await _http.GetFromJsonAsync<ApiResponse<List<OrganizationDto>>>("organizations");
        return response?.Data ?? new List<OrganizationDto>();
    }

    public async Task<OrganizationDto?> GetOrganizationAsync(int id)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<OrganizationDto>>($"organizations/{id}");
            return response?.Data;
        }
        catch
        {
            return null;
        }
    }

    public async Task<(bool IsSuccess, OrganizationDto? Organization, string? ErrorMessage)> CreateOrganizationAsync(CreateOrganizationRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("organizations", request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<OrganizationDto>>();
                return (true, result?.Data, null);
            }

            var error = await ReadErrorMessageAsync(response);
            return (false, null, error);
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }

    public async Task<(bool IsSuccess, OrganizationDto? Organization, string? ErrorMessage)> UpdateOrganizationAsync(int id, UpdateOrganizationRequest request)
    {
        try
        {
            var response = await _http.PutAsJsonAsync($"organizations/{id}", request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<OrganizationDto>>();
                return (true, result?.Data, null);
            }

            var error = await ReadErrorMessageAsync(response);
            return (false, null, error);
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }

    public async Task<(bool IsSuccess, string? ErrorMessage)> DeleteOrganizationAsync(int id)
    {
        try
        {
            var response = await _http.DeleteAsync($"organizations/{id}");
            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }

            var error = await ReadErrorMessageAsync(response);
            return (false, error);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    // ============================================================
    // Member Management
    // ============================================================

    public async Task<List<OrganizationMemberDto>> GetMembersAsync(int organizationId)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<OrganizationMemberDto>>>($"organizations/{organizationId}/members");
            return response?.Data ?? new List<OrganizationMemberDto>();
        }
        catch
        {
            return new List<OrganizationMemberDto>();
        }
    }

    public async Task<(bool IsSuccess, string? ErrorMessage)> InviteMemberAsync(int organizationId, InviteMemberRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync($"organizations/{organizationId}/members", request);
            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }

            var error = await ReadErrorMessageAsync(response);
            return (false, error);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool IsSuccess, string? ErrorMessage)> UpdateMemberRoleAsync(int organizationId, int userId, string newRole)
    {
        try
        {
            var request = new UpdateMemberRoleRequest { Role = newRole };
            var response = await _http.PutAsJsonAsync($"organizations/{organizationId}/members/{userId}", request);
            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }

            var error = await ReadErrorMessageAsync(response);
            return (false, error);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool IsSuccess, string? ErrorMessage)> RemoveMemberAsync(int organizationId, int userId)
    {
        try
        {
            var response = await _http.DeleteAsync($"organizations/{organizationId}/members/{userId}");
            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }

            var error = await ReadErrorMessageAsync(response);
            return (false, error);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    // ============================================================
    // Invitations
    // ============================================================

    public async Task<List<PendingInvitationDto>> GetPendingInvitationsAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<PendingInvitationDto>>>("invitations");
            return response?.Data ?? new List<PendingInvitationDto>();
        }
        catch
        {
            return new List<PendingInvitationDto>();
        }
    }

    public async Task<(bool IsSuccess, string? ErrorMessage)> AcceptInvitationAsync(string token)
    {
        try
        {
            var request = new AcceptInvitationRequest { Token = token };
            var response = await _http.PostAsJsonAsync("organizations/accept-invitation", request);
            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }

            var error = await ReadErrorMessageAsync(response);
            return (false, error);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool IsSuccess, string? ErrorMessage)> AcceptInvitationByIdAsync(int invitationId)
    {
        try
        {
            var response = await _http.PostAsync($"invitations/{invitationId}/accept", null);
            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }

            var error = await ReadErrorMessageAsync(response);
            return (false, error);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool IsSuccess, string? ErrorMessage)> DeclineInvitationByIdAsync(int invitationId)
    {
        try
        {
            var response = await _http.PostAsync($"invitations/{invitationId}/decline", null);
            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }

            var error = await ReadErrorMessageAsync(response);
            return (false, error);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool IsSuccess, string? ErrorMessage)> LeaveOrganizationAsync(int organizationId)
    {
        try
        {
            var response = await _http.PostAsync($"organizations/{organizationId}/leave", null);
            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }

            var error = await ReadErrorMessageAsync(response);
            return (false, error);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool IsSuccess, string? ErrorMessage)> TransferOwnershipAsync(int organizationId, int newOwnerId)
    {
        try
        {
            var request = new TransferOwnershipRequest { NewOwnerId = newOwnerId };
            var response = await _http.PostAsJsonAsync($"organizations/{organizationId}/transfer", request);
            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }

            var error = await ReadErrorMessageAsync(response);
            return (false, error);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    // ============================================================
    // Helper Methods
    // ============================================================

    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync();
            if (content.Contains("\"detail\""))
            {
                var problem = System.Text.Json.JsonDocument.Parse(content);
                if (problem.RootElement.TryGetProperty("detail", out var detail))
                {
                    return detail.GetString() ?? "An error occurred";
                }
            }
            return content.Length < 200 ? content : "An error occurred";
        }
        catch
        {
            return $"Request failed with status {response.StatusCode}";
        }
    }
}

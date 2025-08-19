using System.Net;
using System.Net.Http.Json;
using Shared.Models;

namespace Frontend.Client.Services;

public class ActivityService
{
    private readonly HttpClient _httpClient;

    public ActivityService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ActivityListResponse?> GetActivitiesAsync(int page = 1, int pageSize = 20, ActivityStatus? status = null)
    {
        var url = $"api/activities?page={page}&pageSize={pageSize}" + (status.HasValue ? $"&status={(int)status.Value}" : string.Empty);
        var resp = await _httpClient.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<ActivityListResponse>();
    }

    public async Task<ActivityDetailsResponse?> GetActivityAsync(long id)
    {
        var resp = await _httpClient.GetAsync($"api/activities/{id}");
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<ActivityDetailsResponse>();
    }

    public async Task<ApiResponse<ActivityResponse>?> UpdateActivityAsync(long id, ActivityUpdateRequest request)
    {
        var resp = await _httpClient.PutAsJsonAsync($"api/activities/{id}", request);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<ApiResponse<ActivityResponse>>();
    }

    public async Task<ApiResponse<ActivityResponse>?> SeedActivityAsync(SeedActivityRequest request)
    {
        var resp = await _httpClient.PostAsJsonAsync("api/activities/seed", request);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<ApiResponse<ActivityResponse>>();
    }

    public async Task<ApiResponse<string>?> DeleteActivityAsync(long id)
    {
        var resp = await _httpClient.DeleteAsync($"api/activities/{id}");        
        if (!resp.IsSuccessStatusCode)
        {
            if (resp.StatusCode == HttpStatusCode.NotFound)
            {
                return ApiResponse<string>.Failure("Activity not found or you don't have permission to delete it");
            }
            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                return ApiResponse<string>.Failure("You are not authorized to delete this activity");
            }
            
            var errorContent = await resp.Content.ReadAsStringAsync();
            try
            {
                var errorResponse = await resp.Content.ReadFromJsonAsync<ApiResponse<object>>();
                return ApiResponse<string>.Failure(errorResponse?.Message ?? $"Delete failed with status {resp.StatusCode}");
            }
            catch
            {
                return ApiResponse<string>.Failure($"Delete failed with status {resp.StatusCode}: {errorContent}");
            }
        }
        
        Console.WriteLine("Delete successful");
        var serverResponse = await resp.Content.ReadFromJsonAsync<ApiResponse<object>>();
        if (serverResponse != null)
        {
            return new ApiResponse<string>(
                serverResponse.IsSuccess, 
                serverResponse.Message, 
                serverResponse.Message
            );
        }
        
        return ApiResponse<string>.Success("Activity deleted successfully", "Activity deleted successfully");
    }
}

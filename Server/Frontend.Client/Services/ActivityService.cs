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
}

using System.Text.Json;
using Microsoft.JSInterop;

namespace Frontend.Client.Services;

public class WeeklyGoalStorageService
{
    private readonly IJSRuntime _js;
    private const string Prefix = "weekly-goal::";

    public WeeklyGoalStorageService(IJSRuntime js) => _js = js;

    public async Task SetAsync<T>(string key, T value)
        => await _js.InvokeVoidAsync("localStorage.setItem", Prefix + key, JsonSerializer.Serialize(value));

    public async Task<T?> GetAsync<T>(string key)
    {
        var json = await _js.InvokeAsync<string?>("localStorage.getItem", Prefix + key);
        return string.IsNullOrWhiteSpace(json) ? default : JsonSerializer.Deserialize<T>(json);
    }

    public async Task RemoveAsync(string key)
        => await _js.InvokeVoidAsync("localStorage.removeItem", Prefix + key);
}

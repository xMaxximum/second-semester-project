using System.Net.Http.Json;
using System.Text.Json;
using Shared.Models;

namespace Frontend.Client.Services
{
    public class AuthService
    {
        private readonly HttpClient _httpClient;
        private readonly JwtAuthenticationStateProvider _authStateProvider;

        public AuthService(HttpClient httpClient, JwtAuthenticationStateProvider authStateProvider)
        {
            _httpClient = httpClient;
            _authStateProvider = authStateProvider;
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/auth/login", request);
                var result = await response.Content.ReadFromJsonAsync<LoginResponse>();

                if (result?.IsSuccess == true && !string.IsNullOrEmpty(result.Token))
                {
                    _authStateProvider.Login(result.Token);
                }

                return result ?? new LoginResponse { IsSuccess = false, Message = "Login failed" };
            }
            catch (Exception ex)
            {
                return new LoginResponse { IsSuccess = false, Message = $"Login error: {ex.Message}" };
            }
        }

        public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/auth/register", request);
                var result = await response.Content.ReadFromJsonAsync<RegisterResponse>();
                return result ?? new RegisterResponse(false) { Message = "Registration failed" };
            }
            catch (Exception ex)
            {
                return new RegisterResponse(false) { Message = $"Registration error: {ex.Message}" };
            }
        }

        public async Task<bool> RefreshTokenAsync()
        {
            try
            {
                var response = await _httpClient.PostAsync("api/auth/refresh-token", null);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
                    if (result?.IsSuccess == true && !string.IsNullOrEmpty(result.Token))
                    {
                        _authStateProvider.Login(result.Token);
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task LogoutAsync()
        {
            try
            {
                await _httpClient.PostAsync("api/auth/logout", null);
            }
            catch
            {
                // Ignore errors during logout
            }
            finally
            {
                _authStateProvider.Logout();
            }
        }
    }
}

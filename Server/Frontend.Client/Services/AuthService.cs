using System.Net.Http.Json;
using System.Text.Json;
using System.Net.Http.Headers;
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

        public async Task<GetProfileResponse> GetProfileAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/auth/profile");
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<GetProfileResponse>();
                    return result ?? new GetProfileResponse { IsSuccess = false, Message = "Failed to retrieve profile" };
                }
                return new GetProfileResponse { IsSuccess = false, Message = "Failed to retrieve profile" };
            }
            catch (Exception ex)
            {
                return new GetProfileResponse { IsSuccess = false, Message = $"Profile error: {ex.Message}" };
            }
        }

        public async Task<UpdateProfileResponse> UpdateProfileAsync(UpdateProfileRequest request)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync("api/auth/profile", request);
                var result = await response.Content.ReadFromJsonAsync<UpdateProfileResponse>();
                return result ?? new UpdateProfileResponse(false) { Message = "Profile update failed" };
            }
            catch (Exception ex)
            {
                return new UpdateProfileResponse(false) { Message = $"Profile update error: {ex.Message}" };
            }
        }

        public async Task<ChangeEmailResponse> ChangeEmailAsync(ChangeEmailRequest request)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync("api/auth/change-email", request);
                var result = await response.Content.ReadFromJsonAsync<ChangeEmailResponse>();
                return result ?? new ChangeEmailResponse(false) { Message = "Email change failed" };
            }
            catch (Exception ex)
            {
                return new ChangeEmailResponse(false) { Message = $"Email change error: {ex.Message}" };
            }
        }

        public async Task<ChangePasswordResponse> ChangePasswordAsync(ChangePasswordRequest request)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync("api/auth/change-password", request);
                var result = await response.Content.ReadFromJsonAsync<ChangePasswordResponse>();
                return result ?? new ChangePasswordResponse(false) { Message = "Password change failed" };
            }
            catch (Exception ex)
            {
                return new ChangePasswordResponse(false) { Message = $"Password change error: {ex.Message}" };
            }
        }

        public async Task<DeleteAccountResponse> DeleteAccountAsync(DeleteAccountRequest request)
        {
            try
            {
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, System.Text.Encoding.UTF8, MediaTypeHeaderValue.Parse("application/json"));
                var httpRequest = new HttpRequestMessage(HttpMethod.Delete, "api/auth/delete-account")
                {
                    Content = content
                };
                
                var response = await _httpClient.SendAsync(httpRequest);
                var result = await response.Content.ReadFromJsonAsync<DeleteAccountResponse>();
                return result ?? new DeleteAccountResponse(false) { Message = "Account deletion failed" };
            }
            catch (Exception ex)
            {
                return new DeleteAccountResponse(false) { Message = $"Account deletion error: {ex.Message}" };
            }
        }
    }
}

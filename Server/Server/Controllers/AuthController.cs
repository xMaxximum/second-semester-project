using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Server.Models;
using Shared.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Server.Controllers
{
    [ApiController]
    [Route(Constants.DefaultRoute)]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly ILogger<AuthController> _logger;
        private readonly IConfiguration _configuration;

        public AuthController(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            ILogger<AuthController> logger,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _configuration = configuration;
        }

        [HttpPost("register")]
        public async Task<ActionResult<Shared.Models.RegisterResponse>> Register(Shared.Models.RegisterRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage).ToList();
                    return BadRequest(new Shared.Models.RegisterResponse(false) { Message = "Validation failed", Errors = errors });
                }

                var user = new User()
                {
                    Email = request.Email,
                    UserName = request.UserName,
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(user, request.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("User {Email} registered successfully", request.Email);
                    return Ok(new Shared.Models.RegisterResponse(true) { Message = "Registration successful" });
                }
                else
                {
                    var errors = result.Errors.Select(x => x.Description).ToList();
                    return BadRequest(new Shared.Models.RegisterResponse(false)
                    {
                        Message = "Registration failed",
                        Errors = errors
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user registration");
                return StatusCode(500, new Shared.Models.RegisterResponse(false) { Message = "Internal server error" });
            }
        }

        [HttpPost("login")]
        public async Task<ActionResult<Shared.Models.LoginResponse>> Login(Shared.Models.LoginRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(Shared.Models.LoginResponse.Failed);
                }

                var user = await _userManager.FindByEmailAsync(request.Email);
                if (user != null)
                {
                    var signIn = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
                    if (signIn.Succeeded)
                    {
                        var jwt = CreateJWT(user);
                        AppendRefreshTokenCookie(user, HttpContext.Response.Cookies);

                        _logger.LogInformation("User {Email} logged in successfully", request.Email);
                        return Ok(new Shared.Models.LoginResponse
                        {
                            IsSuccess = true,
                            Token = jwt,
                            Message = "Login successful",
                            Expiration = DateTime.UtcNow.AddHours(Constants.JwtExpirationHours)
                        });
                    }
                }

                _logger.LogWarning("Failed login attempt for {Email}", request.Email);
                return Unauthorized(Shared.Models.LoginResponse.Failed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user login");
                return StatusCode(500, Shared.Models.LoginResponse.Failed);
            }
        }

        [HttpPost("refresh-token")]
        public ActionResult<Shared.Models.LoginResponse> RefreshToken()
        {
            try
            {
                var cookie = HttpContext.Request.Cookies[Constants.RefreshTokenCookieKey];
                if (cookie != null)
                {
                    var user = _userManager.Users.FirstOrDefault(user => user.SecurityStamp == cookie);
                    if (user != null)
                    {
                        var jwtToken = CreateJWT(user);
                        _logger.LogInformation("Token refreshed for user {Email}", user.Email);
                        return Ok(new LoginResponse
                        {
                            IsSuccess = true,
                            Token = jwtToken,
                            Message = "Token refreshed",
                            Expiration = DateTime.UtcNow.AddHours(Constants.JwtExpirationHours)
                        });
                    }
                }

                return Unauthorized(LoginResponse.Failed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token refresh");
                return StatusCode(500, LoginResponse.Failed);
            }
        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            try
            {
                // Clear the refresh token cookie
                HttpContext.Response.Cookies.Delete(Constants.RefreshTokenCookieKey);
                return Ok(new { message = "Logged out successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return StatusCode(500, new { message = "Error during logout" });
            }
        }

        [HttpGet("profile")]
        [Authorize]
        public async Task<ActionResult<Shared.Models.GetProfileResponse>> GetProfile()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new Shared.Models.GetProfileResponse
                    {
                        IsSuccess = false,
                        Message = "User not found"
                    });
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return NotFound(new Shared.Models.GetProfileResponse
                    {
                        IsSuccess = false,
                        Message = "User not found"
                    });
                }

                var profile = new Shared.Models.UserProfile
                {
                    Email = user.Email ?? "",
                    UserName = user.UserName ?? "",
                    EmailConfirmed = user.EmailConfirmed,
                };

                return Ok(new Shared.Models.GetProfileResponse
                {
                    IsSuccess = true,
                    Message = "Profile retrieved successfully",
                    Profile = profile
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user profile");
                return StatusCode(500, new Shared.Models.GetProfileResponse
                {
                    IsSuccess = false,
                    Message = "Internal server error"
                });
            }
        }

        [HttpPut("profile")]
        [Authorize]
        public async Task<ActionResult<Shared.Models.UpdateProfileResponse>> UpdateProfile(Shared.Models.UpdateProfileRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage).ToList();
                    return BadRequest(new Shared.Models.UpdateProfileResponse(false)
                    {
                        Message = "Validation failed",
                        Errors = errors
                    });
                }

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new Shared.Models.UpdateProfileResponse(false)
                    {
                        Message = "User not found"
                    });
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return NotFound(new Shared.Models.UpdateProfileResponse(false)
                    {
                        Message = "User not found"
                    });
                }

                // Check if username is already taken by another user
                var existingUser = await _userManager.FindByNameAsync(request.UserName);
                if (existingUser != null && existingUser.Id != user.Id)
                {
                    return BadRequest(new Shared.Models.UpdateProfileResponse(false)
                    {
                        Message = "Username is already taken",
                        Errors = new List<string> { "Username is already taken" }
                    });
                }

                // Update user properties
                user.UserName = request.UserName;

                var result = await _userManager.UpdateAsync(user);

                if (result.Succeeded)
                {
                    var updatedProfile = new Shared.Models.UserProfile
                    {
                        Email = user.Email ?? "",
                        UserName = user.UserName ?? "",
                        EmailConfirmed = user.EmailConfirmed,
                    };

                    _logger.LogInformation("Profile updated successfully for user {UserId}", userId);
                    return Ok(new Shared.Models.UpdateProfileResponse(true)
                    {
                        Message = "Profile updated successfully",
                        Profile = updatedProfile
                    });
                }
                else
                {
                    var errors = result.Errors.Select(x => x.Description).ToList();
                    return BadRequest(new Shared.Models.UpdateProfileResponse(false)
                    {
                        Message = "Profile update failed",
                        Errors = errors
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user profile");
                return StatusCode(500, new Shared.Models.UpdateProfileResponse(false)
                {
                    Message = "Internal server error"
                });
            }
        }

        [HttpPut("change-email")]
        [Authorize]
        public async Task<ActionResult<Shared.Models.ChangeEmailResponse>> ChangeEmail(Shared.Models.ChangeEmailRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage).ToList();
                    return BadRequest(new Shared.Models.ChangeEmailResponse(false)
                    {
                        Message = "Validation failed",
                        Errors = errors
                    });
                }

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new Shared.Models.ChangeEmailResponse(false)
                    {
                        Message = "User not found"
                    });
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return NotFound(new Shared.Models.ChangeEmailResponse(false)
                    {
                        Message = "User not found"
                    });
                }

                // Verify current password
                var passwordCheck = await _userManager.CheckPasswordAsync(user, request.CurrentPassword);
                if (!passwordCheck)
                {
                    return BadRequest(new Shared.Models.ChangeEmailResponse(false)
                    {
                        Message = "Current password is incorrect",
                        Errors = new List<string> { "Current password is incorrect" }
                    });
                }

                // Check if email is already taken
                var existingUser = await _userManager.FindByEmailAsync(request.NewEmail);
                if (existingUser != null)
                {
                    return BadRequest(new Shared.Models.ChangeEmailResponse(false)
                    {
                        Message = "Email is already in use",
                        Errors = new List<string> { "Email is already in use" }
                    });
                }

                var oldEmail = user.Email;

                // Update email
                user.Email = request.NewEmail;
                user.EmailConfirmed = true; // For simplicity, auto-confirm the email

                var result = await _userManager.UpdateAsync(user);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Email changed successfully for user {UserId} from {OldEmail} to {NewEmail}",
                        userId, oldEmail, request.NewEmail);
                    return Ok(new Shared.Models.ChangeEmailResponse(true)
                    {
                        Message = "Email changed successfully"
                    });
                }
                else
                {
                    var errors = result.Errors.Select(x => x.Description).ToList();
                    return BadRequest(new Shared.Models.ChangeEmailResponse(false)
                    {
                        Message = "Email change failed",
                        Errors = errors
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing user email");
                return StatusCode(500, new Shared.Models.ChangeEmailResponse(false)
                {
                    Message = "Internal server error"
                });
            }
        }

        [HttpPut("change-password")]
        [Authorize]
        public async Task<ActionResult<Shared.Models.ChangePasswordResponse>> ChangePassword(Shared.Models.ChangePasswordRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage).ToList();
                    return BadRequest(new Shared.Models.ChangePasswordResponse(false)
                    {
                        Message = "Validation failed",
                        Errors = errors
                    });
                }

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new Shared.Models.ChangePasswordResponse(false)
                    {
                        Message = "User not found"
                    });
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return NotFound(new Shared.Models.ChangePasswordResponse(false)
                    {
                        Message = "User not found"
                    });
                }

                var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Password changed successfully for user {UserId}", userId);
                    return Ok(new Shared.Models.ChangePasswordResponse(true)
                    {
                        Message = "Password changed successfully"
                    });
                }
                else
                {
                    var errors = result.Errors.Select(x => x.Description).ToList();
                    return BadRequest(new Shared.Models.ChangePasswordResponse(false)
                    {
                        Message = "Password change failed",
                        Errors = errors
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing user password");
                return StatusCode(500, new Shared.Models.ChangePasswordResponse(false)
                {
                    Message = "Internal server error"
                });
            }
        }

        [HttpDelete("delete-account")]
        [Authorize]
        public async Task<ActionResult<Shared.Models.DeleteAccountResponse>> DeleteAccount(Shared.Models.DeleteAccountRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage).ToList();
                    return BadRequest(new Shared.Models.DeleteAccountResponse(false)
                    {
                        Message = "Validation failed",
                        Errors = errors
                    });
                }

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new Shared.Models.DeleteAccountResponse(false)
                    {
                        Message = "User not found"
                    });
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return NotFound(new Shared.Models.DeleteAccountResponse(false)
                    {
                        Message = "User not found"
                    });
                }

                // Verify current password
                var passwordCheck = await _userManager.CheckPasswordAsync(user, request.CurrentPassword);
                if (!passwordCheck)
                {
                    return BadRequest(new Shared.Models.DeleteAccountResponse(false)
                    {
                        Message = "Current password is incorrect",
                        Errors = new List<string> { "Current password is incorrect" }
                    });
                }

                // Delete the user
                var result = await _userManager.DeleteAsync(user);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Account deleted successfully for user {UserId}", userId);
                    return Ok(new Shared.Models.DeleteAccountResponse(true)
                    {
                        Message = "Account deleted successfully"
                    });
                }
                else
                {
                    var errors = result.Errors.Select(x => x.Description).ToList();
                    return BadRequest(new Shared.Models.DeleteAccountResponse(false)
                    {
                        Message = "Account deletion failed",
                        Errors = errors
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user account");
                return StatusCode(500, new Shared.Models.DeleteAccountResponse(false)
                {
                    Message = "Internal server error"
                });
            }
        }

        private string CreateJWT(User user)
        {
            var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration.GetRequiredSection("JwtKey").Value!));
            var credentials = new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, user.UserName ?? ""),
                new Claim(ClaimTypes.Email, user.Email ?? ""),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };

            var token = new JwtSecurityToken(
                issuer: Constants.JwtIssuer,
                audience: Constants.JwtAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(Constants.JwtExpirationHours),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static void AppendRefreshTokenCookie(User user, IResponseCookies cookies)
        {
            var options = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddDays(30) // Refresh token lasts longer than JWT
            };
            cookies.Append(Constants.RefreshTokenCookieKey, user.SecurityStamp ?? "", options);
        }
    }
}

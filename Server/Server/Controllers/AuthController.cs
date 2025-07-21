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
                    UserName = request.DisplayName,
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

using System.ComponentModel.DataAnnotations;

namespace Shared.Models
{
    public class LoginRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        public bool IsSuccess { get; set; }
        public string Token { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime Expiration { get; set; }

        public static LoginResponse Failed => new() { IsSuccess = false, Message = "Invalid email or password" };
    }

    public class RegisterRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [Compare("Password")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string DisplayName { get; set; } = string.Empty;
    }

    public class RegisterResponse
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new();

        public RegisterResponse(bool isSuccess)
        {
            IsSuccess = isSuccess;
        }
    }
}

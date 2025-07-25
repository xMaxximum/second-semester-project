using System.ComponentModel.DataAnnotations;

namespace Shared.Models
{
    public class GetProfileResponse
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public UserProfile? Profile { get; set; }
    }

    public class UserProfile
    {
        public string Email { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool EmailConfirmed { get; set; }
    }

    public class UpdateProfileRequest
    {
        [Required]
        [StringLength(50, MinimumLength = 1)]
        public string UserName { get; set; } = string.Empty;
    }

    public class UpdateProfileResponse
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new();
        public UserProfile? Profile { get; set; }

        public UpdateProfileResponse(bool isSuccess)
        {
            IsSuccess = isSuccess;
        }
    }

    public class ChangeEmailRequest
    {
        [Required]
        [EmailAddress]
        public string NewEmail { get; set; } = string.Empty;

        [Required]
        public string CurrentPassword { get; set; } = string.Empty;
    }

    public class ChangeEmailResponse
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new();

        public ChangeEmailResponse(bool isSuccess)
        {
            IsSuccess = isSuccess;
        }
    }

    public class ChangePasswordRequest
    {
        [Required]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [Compare("NewPassword")]
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }

    public class ChangePasswordResponse
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new();

        public ChangePasswordResponse(bool isSuccess)
        {
            IsSuccess = isSuccess;
        }
    }

    public class DeleteAccountRequest
    {
        [Required]
        public string CurrentPassword { get; set; } = string.Empty;
    }

    public class DeleteAccountResponse
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new();

        public DeleteAccountResponse(bool isSuccess)
        {
            IsSuccess = isSuccess;
        }
    }
}

namespace Shared.Models
{
    public class DeviceDto
    {
        public string DeviceId { get; set; } = "";
        public string Name { get; set; } = "";
        public string? FirmwareVersion { get; set; }
        public DateTime RegisteredAt { get; set; }
        public bool IsActive { get; set; }
        public bool HasAuthToken { get; set; }
        public bool HasPendingActivation { get; set; }
        public string? ActivationCode { get; set; }
        public DateTime? ActivationCodeExpiry { get; set; }
        public string? AuthToken { get; set; }
    }

    public class ActivationCodeResponse
    {
        public string DeviceId { get; set; } = "";
        public string ActivationCode { get; set; } = "";
        public DateTime ExpiresAt { get; set; }
        public int ExpiresInMinutes { get; set; }
    }
}

using Microsoft.AspNetCore.Identity;

namespace Server.Models
{
    public class User : IdentityUser<long>
    {
        public string DisplayName { get; set; } = string.Empty;
    }
}

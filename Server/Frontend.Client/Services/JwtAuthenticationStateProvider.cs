using Microsoft.AspNetCore.Components.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Frontend.Client.Services
{
    public class JwtAuthenticationStateProvider : AuthenticationStateProvider
    {
        private static readonly AuthenticationState NotAuthenticatedState = 
            new AuthenticationState(new ClaimsPrincipal());

        private User? _user;

        public string? DisplayName => _user?.DisplayName;
        public bool IsLoggedIn => _user != null;
        public string? Token => _user?.Jwt;

        public void Login(string jwt)
        {
            var claims = ParseClaimsFromJwt(jwt);
            var identity = new ClaimsIdentity(claims, "jwt");
            var principal = new ClaimsPrincipal(identity);
            
            var displayName = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? "";
            _user = new User(displayName, jwt, principal);
            
            NotifyAuthenticationStateChanged(Task.FromResult(GetState()));
        }

        public void Logout()
        {
            _user = null;
            NotifyAuthenticationStateChanged(Task.FromResult(GetState()));
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            return Task.FromResult(GetState());
        }

        private AuthenticationState GetState()
        {
            return _user != null ? new AuthenticationState(_user.ClaimsPrincipal) : NotAuthenticatedState;
        }

        private List<Claim> ParseClaimsFromJwt(string jwt)
        {
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(jwt);
            return token.Claims.ToList();
        }
    }

    public class User
    {
        public string DisplayName { get; }
        public string Jwt { get; }
        public ClaimsPrincipal ClaimsPrincipal { get; }

        public User(string displayName, string jwt, ClaimsPrincipal claimsPrincipal)
        {
            DisplayName = displayName;
            Jwt = jwt;
            ClaimsPrincipal = claimsPrincipal;
        }
    }
}

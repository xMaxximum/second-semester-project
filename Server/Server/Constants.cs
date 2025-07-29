namespace Server
{
    public static class Constants
    {
        public const string RoutePrefix = "api";
        public const string DefaultRoute = $"{RoutePrefix}/[controller]";
        
        // JWT Configuration
        public const string JwtIssuer = "CycloneServer";
        public const string JwtAudience = "CycloneClient";
        public const int JwtExpirationHours = 24 * 7;
        
        // Cookie Names
        public const string RefreshTokenCookieKey = "RefreshToken";
    }
}

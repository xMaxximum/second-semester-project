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
        
        // MQTT Configuration 
        public const string MqttHost = "mqtt-dhbw-hdh-ai2024.duckdns.org";
        public const int  MqttPort = 1883;
        public const string MqttUser = "BikeUser";
    }
}

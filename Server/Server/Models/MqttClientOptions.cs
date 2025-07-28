namespace Server.Models
{
    public class MqttClientOptions
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string User { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Topic { get; set; } = "#";
    }
}

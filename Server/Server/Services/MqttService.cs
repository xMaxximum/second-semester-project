using HiveMQtt;
using HiveMQtt.Client;
using HiveMQtt.Client.Options;

namespace Server.Services
{
    public class MqttService: IHostedService
    {
        private readonly ILogger<MqttService> _logger;
        private HiveMQClient _client;

        public MqttService(ILogger<MqttService> logger)
        {
            _logger = logger;
            var clientOptions = new HiveMQClientOptions 
                { 
                    Host = "mqtt-dhbw-hdh-ai2024.duckdns.org", 
                    Port = 1883,
                    UserName = "BikeUser",
                    Password = Environment.GetEnvironmentVariable("MQTT_Password"),
                };
            _client = new HiveMQClient(clientOptions);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var connectedResult = await _client.ConnectAsync().ConfigureAwait(false);

            _client.OnMessageReceived += (sender, args) =>
            {
                // Handle Message in args.PublishMessage
                Console.WriteLine("Message Received: {}", args.PublishMessage.PayloadAsString);
            };

            // topic hier festlegen
            await _client.SubscribeAsync("topic/test").ConfigureAwait(false);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _client.UnsubscribeAsync("topic/test");
            await _client.DisconnectAsync();
        }
    }
}

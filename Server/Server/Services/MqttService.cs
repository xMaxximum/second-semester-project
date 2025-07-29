using HiveMQtt.Client;
using HiveMQtt.Client.Events;
using HiveMQtt.Client.Options;
using HiveMQtt.MQTT5.ReasonCodes;
using Microsoft.Extensions.Options;
using Server.Models;

namespace Server.Services
{
    public class MqttService: IHostedService
    {
        private readonly ILogger<MqttService> _logger;
        private HiveMQClient _client;
        private readonly MqttClientOptions _options;

        public MqttService(ILogger<MqttService> logger, IOptions<MqttClientOptions> options)
        {
            _logger = logger;
            _options = options.Value;
            
            var clientOptions = new HiveMQClientOptions 
                {
                    Host = _options.Host,
                    Port = _options.Port,
                    UserName = _options.User,
                    Password = _options.Password,
                };
            _client = new HiveMQClient(clientOptions);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("trying to connect to MQTT broker at {host}:{port}", _options.Host, _options.Port);
                var connectedResult = await _client.ConnectAsync().ConfigureAwait(false);

                if (connectedResult.ReasonCode == ConnAckReasonCode.Success)
                {
                    _logger.LogInformation("successfully connected to MQTT broker.");
                    
                    // set topic to subscribe to 
                    await _client.SubscribeAsync(_options.Topic).ConfigureAwait(false);   
                    _logger.LogInformation("subscribed to topic: {topic}", _options.Topic);
                    
                    _client.OnMessageReceived += OnMessageReceived;
                }
                else
                {
                    _logger.LogError("failed to connect to MQTT broker: {reason}", connectedResult.ReasonCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        private void OnMessageReceived(object? sender, OnMessageReceivedEventArgs args)
        {
            var payload = args.PublishMessage.Payload;
            var payloadString = args.PublishMessage.PayloadAsString;
            
            _logger.LogInformation("received message: {payload}", payloadString);
        }
        
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _client.UnsubscribeAsync("topic/test");
                await _client.DisconnectAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }
    }
}

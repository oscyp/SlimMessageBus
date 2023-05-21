namespace SlimMessageBus.Host;

public delegate byte[] MessagePayloadProvider<T>(T transportMessage);

/// <summary>
/// The <see cref="IMessageProcessor{TMessage}"/> implementation that processes the responses arriving to the bus.
/// </summary>
/// <typeparam name="TMessage"></typeparam>
public class ResponseMessageProcessor<TMessage> : IMessageProcessor<TMessage>
{
    private readonly ILogger<ResponseMessageProcessor<TMessage>> _logger;
    private readonly RequestResponseSettings _requestResponseSettings;
    private readonly IReadOnlyCollection<AbstractConsumerSettings> _consumerSettings;
    private readonly MessageBusBase _messageBus;
    private readonly MessagePayloadProvider<TMessage> _messagePayloadProvider;

    public ResponseMessageProcessor(RequestResponseSettings requestResponseSettings, MessageBusBase messageBus, MessagePayloadProvider<TMessage> messagePayloadProvider)
    {
        if (messageBus is null) throw new ArgumentNullException(nameof(messageBus));

        _logger = messageBus.LoggerFactory.CreateLogger<ResponseMessageProcessor<TMessage>>();
        _requestResponseSettings = requestResponseSettings ?? throw new ArgumentNullException(nameof(requestResponseSettings));
        _consumerSettings = new List<AbstractConsumerSettings> { _requestResponseSettings };
        _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
        _messagePayloadProvider = messagePayloadProvider ?? throw new ArgumentNullException(nameof(messagePayloadProvider));
    }

    public IReadOnlyCollection<AbstractConsumerSettings> ConsumerSettings => _consumerSettings;

    public async Task<(Exception Exception, AbstractConsumerSettings ConsumerSettings, object Response, object Message)> ProcessMessage(TMessage message, IReadOnlyDictionary<string, object> messageHeaders, CancellationToken cancellationToken, IServiceProvider currentServiceProvider = null)
    {
        try
        {
            var messagePayload = _messagePayloadProvider(message);
            var exception = await _messageBus.OnResponseArrived(messagePayload, _requestResponseSettings.Path, messageHeaders);
            return (exception, _requestResponseSettings, null, message);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error occured while consuming response message, {Message}", message);

            // We can only continue and process all messages in the lease    
            return (e, _requestResponseSettings, null, message);
        }
    }
}
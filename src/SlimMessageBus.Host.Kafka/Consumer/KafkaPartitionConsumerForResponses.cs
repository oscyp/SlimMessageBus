namespace SlimMessageBus.Host.Kafka;

using ConsumeResult = ConsumeResult<Ignore, byte[]>;

/// <summary>
/// Processor for incomming response messages in the request-response patterns. 
/// See also <see cref="IKafkaPartitionConsumer"/>.
/// </summary>
public class KafkaPartitionConsumerForResponses : KafkaPartitionConsumer
{
    private readonly IResponseConsumer _responseConsumer;

    public KafkaPartitionConsumerForResponses(ILoggerFactory loggerFactory, RequestResponseSettings requestResponseSettings, string group, TopicPartition topicPartition, IKafkaCommitController commitController, IResponseConsumer responseConsumer, IMessageSerializer headerSerializer)
        : base(loggerFactory, new[] { requestResponseSettings }, group, topicPartition, commitController, headerSerializer)
    {
        _responseConsumer = responseConsumer;
    }

    protected override IMessageProcessor<ConsumeResult<Ignore, byte[]>> CreateMessageProcessor()
        => new ResponseMessageProcessor<ConsumeResult>(LoggerFactory, (RequestResponseSettings)ConsumerSettings[0], _responseConsumer, m => m.Message.Value);
}
namespace SlimMessageBus.Host.Kafka;

using ConsumeResult = ConsumeResult<Ignore, byte[]>;

/// <summary>
/// Processor for regular consumers. 
/// See also <see cref="IKafkaPartitionConsumer"/>.
/// </summary>
public class KafkaPartitionConsumerForConsumers : KafkaPartitionConsumer
{
    private readonly MessageBusBase _messageBus;

    public KafkaPartitionConsumerForConsumers(ILoggerFactory loggerFactory, ConsumerSettings[] consumerSettings, string group, TopicPartition topicPartition, IKafkaCommitController commitController, IMessageSerializer headerSerializer, MessageBusBase messageBus)
        : base(loggerFactory, consumerSettings, group, topicPartition, commitController, headerSerializer)
    {
        _messageBus = messageBus;
    }

    protected override IMessageProcessor<ConsumeResult<Ignore, byte[]>> CreateMessageProcessor()
        => new MessageProcessor<ConsumeResult>(ConsumerSettings, _messageBus, messageProvider: GetMessageFromTransportMessage, path: TopicPartition.Topic, responseProducer: _messageBus, (m, ctx) => ctx.SetTransportMessage(m));

    private object GetMessageFromTransportMessage(Type messageType, ConsumeResult transportMessage)
        => _messageBus.Serializer.Deserialize(messageType, transportMessage.Message.Value);
}
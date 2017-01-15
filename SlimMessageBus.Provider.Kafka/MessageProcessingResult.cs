using System.Threading.Tasks;
using RdKafka;

namespace SlimMessageBus.Provider.Kafka
{
    public class MessageProcessingResult
    {
        public readonly Task Task;
        public readonly Message Message;

        public MessageProcessingResult(Task task, Message message)
        {
            Task = task;
            Message = message;
        }
    }
}
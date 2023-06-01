namespace SlimMessageBus.Host.AzureEventHub;

internal static class MessageBusSettingsExtensions
{
    internal static bool IsAnyConsumerDeclared(this MessageBusSettings settings) => settings.Consumers.Count > 0 || settings.RequestResponse != null;
}

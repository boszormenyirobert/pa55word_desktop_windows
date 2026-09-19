namespace TrayApp.Services
{
    internal interface INativeMessageProcessor
    {
        Task<string> ProcessRequestAsync(string requestJson);
    }
}

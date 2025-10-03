namespace GmailInboxForwarder.Services
{
    public interface IInboxService
    {
        Task Resend(string webSocketConnectionId);
        Task Pause(string webSocketConnectionId);
        Task ClearCache(string webSocketConnectionId);
    }
}

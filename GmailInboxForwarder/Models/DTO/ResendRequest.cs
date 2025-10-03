namespace GmailInboxForwarder.Models.DTO
{
    public enum ResendAction
    {
        Start,
        Stop,
        ClearCache
    }

    public class ResendRequest
    {
        public string WebSocketConnectionId { get; set; }
        public ResendAction Action { get; set; }
    }
}
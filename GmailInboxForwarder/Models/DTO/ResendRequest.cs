using System.Text.Json.Serialization;

namespace GmailInboxForwarder.Models.DTO
{
    public class ResendRequest
    {
        public string WebSocketConnectionId { get; set; }
    }
}

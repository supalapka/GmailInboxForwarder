using Microsoft.AspNetCore.SignalR;

namespace GmailInboxForwarder.Hubs
{
    public class ActivityHub : Hub
    {
        public string GetConnectionId()
        {
            return Context.ConnectionId;
        }
    }
}

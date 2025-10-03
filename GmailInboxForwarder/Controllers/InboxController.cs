using GmailInboxForwarder.Models.DTO;
using GmailInboxForwarder.Services;
using Microsoft.AspNetCore.Mvc;

namespace GmailInboxForwarder.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class InboxController : Controller
    {
        private readonly IInboxService _inboxService;

        public InboxController(IInboxService inboxService)
        {
            _inboxService = inboxService;
        }

        [HttpPost("resend")]
        public async Task<IActionResult> ResendAll([FromBody] ResendRequest request)
        {
            switch (request.Action)
            {
                case ResendAction.Start:
                    await _inboxService.Resend(request.WebSocketConnectionId);
                    break;

                case ResendAction.Stop:
                    await _inboxService.Pause(request.WebSocketConnectionId);
                    break;

                case ResendAction.ClearCache:
                    await _inboxService.ClearCache(request.WebSocketConnectionId);
                    break;
            }

            return Ok();

        }
    }
}

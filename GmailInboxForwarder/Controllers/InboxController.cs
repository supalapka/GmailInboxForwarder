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
            await _inboxService.Resend(request.WebSocketConnectionId);
            return Ok();
        }
    }
}

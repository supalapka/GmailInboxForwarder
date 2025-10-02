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
        public async Task<IActionResult> ResendAllEmails()
        {
            try
            {
                await _inboxService.Resend();
                return Ok(new { message = "Resend triggered" });

            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}

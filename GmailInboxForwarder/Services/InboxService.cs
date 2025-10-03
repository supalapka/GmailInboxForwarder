using GmailInboxForwarder.Hubs;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using Microsoft.AspNetCore.SignalR;
using MimeKit;

namespace GmailInboxForwarder.Services
{
    public class InboxService : IInboxService
    {
        private readonly string _sourceInbox;
        private readonly string _destinationInbox;
        private readonly string _sourceAppPassword;

        private readonly string _imapHost;
        private readonly string _smtpHost;
        private readonly int _imapPort;
        private readonly int _smtpPort;

        private readonly string _sourceName;
        private readonly string _destinationName;

        private readonly IHubContext<ActivityHub> _hubContext;

        private static Dictionary<string, InboxMailProgress> _inboxCache = new Dictionary<string, InboxMailProgress>();

        public InboxService(IConfiguration configuration, IHubContext<ActivityHub> hubContext)
        {
            _imapHost = configuration["IMAP_HOST"] ?? throw new ArgumentException("IMAP_HOST not set");
            _smtpHost = configuration["SMTP_HOST"] ?? throw new ArgumentException("SMTP_HOST not set");

            var imapPortString = configuration["IMAP_PORT"];
            if (!int.TryParse(imapPortString, out _imapPort))
                throw new ArgumentException("IMAP_PORT is not a valid integer");

            var smtpPortString = configuration["SMTP_PORT"];
            if (!int.TryParse(smtpPortString, out _smtpPort))
                throw new ArgumentException("SMTP_PORT is not a valid integer");


            _sourceInbox = configuration["SOURCE_INBOX"] ?? throw new ArgumentException("SOURCE_INBOX not set");
            _destinationInbox = configuration["DESTINATION_INBOX"] ?? throw new ArgumentException("DESTINATION_INBOX not set");
            _sourceAppPassword = configuration["IMAP_PASSWORD"] ?? throw new ArgumentException("IMAP_PASSWORD not set");

            _sourceName = configuration["IMAP_USERNAME"] ?? throw new ArgumentException("IMAP_USERNAME not set");
            _destinationName = configuration["SMTP_USERNAME"] ?? throw new ArgumentException("SMTP_USERNAME not set");

            _hubContext = hubContext;
        }

        public Task ClearCache(string webSocketConnectionId)
        {
            if (_inboxCache.ContainsKey(webSocketConnectionId))
                _inboxCache.Remove(webSocketConnectionId);

            return Task.CompletedTask;
        }

        public async Task Pause(string webSocketConnectionId)
        {
            if (_inboxCache.TryGetValue(webSocketConnectionId, out var progress))
            {
                progress.IsPaused = true;
                await _hubContext.Clients.Client(webSocketConnectionId).SendAsync("NewActivity", "Resend paused");
            }
        }


        public async Task Resend(string webSocketConnectionId)
        {
            using var imap = new ImapClient();
            await imap.ConnectAsync(_imapHost, _imapPort, true);
            await imap.AuthenticateAsync(_sourceInbox, _sourceAppPassword);

            var inbox = imap.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly);

            if (inbox.Count == 0)
            {
                Console.WriteLine("Inbox is empty");
                await imap.DisconnectAsync(true);
                return;
            }

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_smtpHost, _smtpPort, MailKit.Security.SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_sourceInbox, _sourceAppPassword);


            // get progress from cache
            if (!_inboxCache.TryGetValue(webSocketConnectionId, out var progress))
            {
                progress = new InboxMailProgress { CurrentIndex = 0, IsPaused = false };
                _inboxCache[webSocketConnectionId] = progress;
            }

            progress.IsPaused = false;
            await _hubContext.Clients.Client(webSocketConnectionId).SendAsync("NewActivity", "Resend started");

            for (; progress.CurrentIndex < inbox.Count; progress.CurrentIndex++)
            {
                if (progress.IsPaused)
                    break;

                var message = await inbox.GetMessageAsync(progress.CurrentIndex);

                await _hubContext.Clients.Client(webSocketConnectionId)
                    .SendAsync("NewActivity", $"Fetched message {message.MessageId}");

                var forwardMessage = BuildForwardMessage(message);

                await smtp.SendAsync(forwardMessage);

                await _hubContext.Clients.Client(webSocketConnectionId)
                .SendAsync("NewActivity", $"Forwarded to {_destinationInbox}");
            }

            if(progress.CurrentIndex == inbox.Count) // means all mesages processed
                await ClearCache(webSocketConnectionId);

            await smtp.DisconnectAsync(true);
            await imap.DisconnectAsync(true);
        }

        private MimeMessage BuildForwardMessage(MimeMessage originalMessage)
        {
            var forwardMessage = new MimeMessage();
            forwardMessage.From.Add(new MailboxAddress(_sourceName, _sourceInbox));
            forwardMessage.To.Add(new MailboxAddress(_destinationName, _destinationInbox));

            // copy cc/bcc
            foreach (var cc in originalMessage.Cc)
                forwardMessage.Cc.Add(cc);
            foreach (var bcc in originalMessage.Bcc)
                forwardMessage.Bcc.Add(bcc);

            forwardMessage.Subject = originalMessage.Subject;

            // copy body
            if (!string.IsNullOrEmpty(originalMessage.HtmlBody) && !string.IsNullOrEmpty(originalMessage.TextBody))
            {
                forwardMessage.Body = new MultipartAlternative
                {
                    new TextPart("plain") { Text = originalMessage.TextBody },
                    new TextPart("html") { Text = originalMessage.HtmlBody }
                };
            }
            else if (!string.IsNullOrEmpty(originalMessage.HtmlBody))
            {
                forwardMessage.Body = new TextPart("html") { Text = originalMessage.HtmlBody };
            }
            else if (!string.IsNullOrEmpty(originalMessage.TextBody))
            {
                forwardMessage.Body = new TextPart("plain") { Text = originalMessage.TextBody };
            }

            // copy attachments
            if (originalMessage.Attachments != null)
            {
                var multipart = new Multipart("mixed");
                multipart.Add(forwardMessage.Body);
                foreach (var attachment in originalMessage.Attachments)
                    multipart.Add(attachment);
                forwardMessage.Body = multipart;
            }

            forwardMessage.Date = originalMessage.Date;

            return forwardMessage;
        }

    }

    public class InboxMailProgress
    {
        public int CurrentIndex { get; set; }
        public bool IsPaused { get; set; }
    }
}

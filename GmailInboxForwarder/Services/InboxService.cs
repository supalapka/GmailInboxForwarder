using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
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

        public InboxService(IConfiguration configuration)
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
        }


        public async Task Resend()
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

            for (int i = 0; i < inbox.Count; i++)
            {
                var message = await inbox.GetMessageAsync(i);

                var forwardMessage = new MimeMessage();
                forwardMessage.From.Add(new MailboxAddress(_sourceName, _sourceInbox));
                forwardMessage.To.Add(new MailboxAddress(_destinationName, _destinationInbox));

                foreach (var cc in message.Cc)
                    forwardMessage.Cc.Add(cc);
                foreach (var bcc in message.Bcc)
                    forwardMessage.Bcc.Add(bcc);

                forwardMessage.Subject = message.Subject;

                if (!string.IsNullOrEmpty(message.HtmlBody) && !string.IsNullOrEmpty(message.TextBody))
                {
                    forwardMessage.Body = new MultipartAlternative
                    {
                        new TextPart("plain") { Text = message.TextBody },
                        new TextPart("html") { Text = message.HtmlBody }
                    };
                }
                else if (!string.IsNullOrEmpty(message.HtmlBody))
                {
                    forwardMessage.Body = new TextPart("html") { Text = message.HtmlBody };
                }
                else if (!string.IsNullOrEmpty(message.TextBody))
                {
                    forwardMessage.Body = new TextPart("plain") { Text = message.TextBody };
                }

                if (message.Attachments != null)
                {
                    var multipart = new Multipart("mixed");
                    multipart.Add(forwardMessage.Body);
                    foreach (var attachment in message.Attachments)
                        multipart.Add(attachment);
                    forwardMessage.Body = multipart;
                }

                forwardMessage.Date = message.Date;

                await smtp.SendAsync(forwardMessage);
            }

            await smtp.DisconnectAsync(true);
            await imap.DisconnectAsync(true);
        }

    }
}

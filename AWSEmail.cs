
namespace Core
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    using Amazon;
    using Amazon.SimpleEmail;
    using Amazon.SimpleEmail.Model;
    using MimeKit;
	
	public class ConnectionEmail
	{
		 public string KeyId { get; set; }
		 public string Secret { get; set; }
	}

    /// <summary>
    /// Sends an email asynchronously using Amazon Simple Email Service (SES).
    /// </summary>
    public class AWSEmail 
    {
        private ConnectionEmail connectionEmail;

        /// <summary>
        /// Sets up the connection to AWS SES using the provided credentials.
        /// </summary>
        /// <param name="connectionEmail">The connection email DTO containing the necessary credentials.</param>
        public void Connection(ConnectionEmail connectionEmail)
        {
            this.connectionEmail = connectionEmail;
        }

        /// <summary>
        /// Envía un correo electrónico utilizando AWS Simple Email Service (SES).
        /// </summary>
        /// <param name="sender">Dirección de correo electrónico del remitente.</param>
        /// <param name="recipients">Lista de direcciones de correo electrónico de los destinatarios.</param>
        /// <param name="subject">Asunto del correo electrónico.</param>
        /// <param name="htmlBody">Cuerpo del correo electrónico en formato HTML.</param>
        /// <param name="attachmentName">Nombre del archivo adjunto (opcional).</param>
        /// <param name="attachmentBase64">Cadena en base64 que representa el archivo adjunto (opcional).</param>
        /// <returns>Una tarea que representa la operación asincrónica.</returns>
        public async Task SendEmailAsync(
            string sender,
            List<string> recipients,
            string subject,
            string htmlBody,
            string attachmentName = null,
            string attachmentBase64 = null)
        {
            this.ConfigureSecurityProtocol();

            using (var client = this.CreateEmailServiceClient())
            {
                var message = this.CreateMimeMessage(sender, recipients, subject, htmlBody, attachmentName, attachmentBase64);
                await this.SendRawEmailAsync(client, sender, recipients, message);
            }
        }

        /// <summary>
        /// Configura los protocolos de seguridad y la devolución de llamada de validación del certificado del servidor.
        /// </summary>
        private void ConfigureSecurityProtocol()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
        }

        /// <summary>
        /// Crea y devuelve una instancia de AmazonSimpleEmailServiceClient utilizando las credenciales de AWS y la región especificada.
        /// </summary>
        /// <returns>Devuelve una instancia de AmazonSimpleEmailServiceClient.</returns>
        private AmazonSimpleEmailServiceClient CreateEmailServiceClient()
        {
            return new AmazonSimpleEmailServiceClient(
                this.connectionEmail.KeyId,
                this.connectionEmail.Secret,
                RegionEndpoint.USEast1);
        }

        /// <summary>
        /// Crea un objeto MimeMessage para un correo electrónico, incluyendo opcionalmente un archivo adjunto.
        /// </summary>
        /// <param name="sender">Dirección de correo electrónico del remitente.</param>
        /// <param name="recipients">Lista de direcciones de correo electrónico de los destinatarios.</param>
        /// <param name="subject">Asunto del correo electrónico.</param>
        /// <param name="htmlBody">Cuerpo del correo electrónico en formato HTML.</param>
        /// <param name="attachmentName">Nombre del archivo adjunto.</param>
        /// <param name="attachmentBase64">Cadena en base64 que representa el archivo adjunto.</param>
        /// <returns>Devuelve el objeto MimeMessage construido.</returns>
        private MimeMessage CreateMimeMessage(
            string sender,
            List<string> recipients,
            string subject,
            string htmlBody,
            string attachmentName,
            string attachmentBase64)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(string.Empty, sender));
            message.Subject = subject;

            this.AddRecipients(message, recipients);

            var bodyBuilder = this.CreateBodyBuilder(htmlBody, attachmentName, attachmentBase64);
            message.Body = bodyBuilder.ToMessageBody();

            return message;
        }

        /// <summary>
        /// Agrega destinatarios a un objeto MimeMessage.
        /// </summary>
        /// <param name="message">El objeto MimeMessage al que se agregarán los destinatarios.</param>
        /// <param name="recipients">Lista de direcciones de correo electrónico de los destinatarios.</param>
        private void AddRecipients(MimeMessage message, List<string> recipients)
        {
            foreach (var recipient in recipients)
            {
                message.To.Add(new MailboxAddress(string.Empty, recipient));
            }
        }

        /// <summary>
        /// Crea un objeto BodyBuilder para un correo electrónico, agregando opcionalmente un archivo adjunto si se especifica.
        /// </summary>
        /// <param name="htmlBody">Cuerpo del correo electrónico en formato HTML.</param>
        /// <param name="attachmentName">Nombre del archivo adjunto.</param>
        /// <param name="attachmentBase64">Cadena en base64 que representa el archivo adjunto.</param>
        /// <returns>Devuelve el objeto BodyBuilder construido.</returns>
        private BodyBuilder CreateBodyBuilder(string htmlBody, string attachmentName, string attachmentBase64)
        {
            var builder = new BodyBuilder { HtmlBody = htmlBody };

            if (this.ShouldAddAttachment(attachmentName, attachmentBase64))
            {
                this.AddAttachment(builder, attachmentName, attachmentBase64);
            }

            return builder;
        }

        /// <summary>
        /// Verifica si se debe agregar un archivo adjunto basado en el nombre del archivo y la cadena en base64.
        /// </summary>
        /// <param name="attachmentName">Nombre del archivo adjunto.</param>
        /// <param name="attachmentBase64">Cadena en base64 que representa el archivo adjunto.</param>
        /// <returns>Devuelve true si ambos parámetros no son nulos ni están vacíos; de lo contrario, devuelve false.</returns>
        private bool ShouldAddAttachment(string attachmentName, string attachmentBase64)
        {
            return !string.IsNullOrEmpty(attachmentName) && !string.IsNullOrEmpty(attachmentBase64);
        }

        /// <summary>
        /// Agrega un archivo adjunto a un correo electrónico utilizando un BodyBuilder.
        /// </summary>
        /// <param name="builder">El BodyBuilder utilizado para construir el cuerpo del correo electrónico.</param>
        /// <param name="attachmentName">Nombre del archivo adjunto.</param>
        /// <param name="attachmentBase64">Cadena en base64 que representa el archivo adjunto.</param>
        private void AddAttachment(BodyBuilder builder, string attachmentName, string attachmentBase64)
        {
            var bytes = Convert.FromBase64String(attachmentBase64);
            builder.Attachments.Add(attachmentName, bytes);
        }

        /// <summary>
        /// Envía un correo electrónico en formato bruto utilizando AWS Simple Email Service (SES).
        /// </summary>
        /// <param name="client">Cliente de AmazonSimpleEmailService utilizado para enviar el correo electrónico.</param>
        /// <param name="sender">Dirección de correo electrónico del remitente.</param>
        /// <param name="recipients">Lista de direcciones de correo electrónico de los destinatarios.</param>
        /// <param name="message">Mensaje MIME que contiene el contenido del correo electrónico.</param>
        /// <returns>Una tarea que representa la operación asincrónica. El valor de la tarea contiene la respuesta del envío del correo electrónico en formato bruto.</returns>
        private async Task<SendRawEmailResponse> SendRawEmailAsync(
            AmazonSimpleEmailServiceClient client,
            string sender,
            List<string> recipients,
            MimeMessage message)
        {
            using (var memoryStream = new MemoryStream())
            {
                await message.WriteToAsync(memoryStream);
                var rawMessage = new RawMessage { Data = memoryStream };

                var sendRequest = new SendRawEmailRequest
                {
                    Source = sender,
                    Destinations = recipients,
                    RawMessage = rawMessage,
                };

                return await client.SendRawEmailAsync(sendRequest);
            }
        }
    }
}

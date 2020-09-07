// © 2016 Sitecore Corporation A/S. All rights reserved.

using System;
using Sitecore.Diagnostics;
using Sitecore.ExM.Framework.Diagnostics;
using Sitecore.Modules.EmailCampaign.Core.Contacts;
using Sitecore.EmailCampaign.XConnect.Web;
using Sitecore.XConnect;
using Sitecore.XConnect.Client;
using Sitecore.XConnect.Collection.Model;
using static System.FormattableString;
using Sitecore.EmailCampaign.Cm.Pipelines.EmailOpened;

namespace Sitecore.Support.EmailCampaign.Cm.Pipelines.EmailOpened
{
    /// <summary>
    /// A processor for the email opened pipeline resetting the email bounce counter of the currently identified contact.
    /// </summary>
    public class ResetContactEmailBounceCount
    {
        private readonly ILogger _logger;
        private readonly IContactService _contactService;
        private readonly XConnectRetry _xConnectRetry;

        /// <summary>
        /// The delay between each retry
        /// </summary>
        public double Delay { get; set; }

        /// <summary>
        /// The number of retry attempts to be made
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResetContactEmailBounceCount"/> class with a custom factory.
        /// </summary>
        public ResetContactEmailBounceCount([NotNull] ILogger logger, [NotNull] IContactService contactService, [NotNull] XConnectRetry xConnectRetry)
        {
            Assert.ArgumentNotNull(logger, "logger");
            Assert.ArgumentNotNull(contactService, nameof(contactService));
            Assert.ArgumentNotNull(xConnectRetry, nameof(xConnectRetry));

            _logger = logger;
            _contactService = contactService;
            _xConnectRetry = xConnectRetry;
        }

        /// <summary>
        /// Locates the <see cref="EmailAddressList"/> facet of the currently identified contact
        /// and resets the BounceCount member of the email marked as preferred.
        /// </summary>
        /// <param name="args">The pipeline argument.</param>
        public void Process([NotNull] EmailOpenedPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");

            try
            {
                _xConnectRetry.RequestWithRetry(client =>
                {
                    Contact contact = _contactService.GetContactWithRetry(args.EmailOpen.ContactIdentifier, Delay, RetryCount, EmailAddressList.DefaultFacetKey);
                    if (contact == null)
                    {
                        _logger.LogDebug(Invariant($"Email bounce count not reset, as contact not found for {args}"));
                        return;
                    }

                    var emailAddresses = contact.Emails();

                    if (string.IsNullOrEmpty(emailAddresses?.PreferredEmail?.SmtpAddress))
                    {
                        _logger.LogDebug(Invariant($"Email bounce count not reset, as preferred email could not be found for contact {args}"));
                        return;
                    }

                    EmailAddress preferredEmailAddress = emailAddresses.PreferredEmail;
                    preferredEmailAddress.BounceCount = 0;

                    client.SetEmails(contact, emailAddresses);
                    client.Submit();
                });
            }
            catch (AggregateException ex)
            {
                _logger.LogError(Invariant($"Email bounce count not reset. Details: {args}"), ex);
                throw;
            }
        }
    }
}

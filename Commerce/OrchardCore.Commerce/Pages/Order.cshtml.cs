using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrchardCore.Commerce.Models;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Email;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace OrchardCore.Commerce.Pages
{
    public class OrderModel : PageModel
    {
        private readonly IHtmlLocalizer H;
        private readonly INotifier _notifier;
        private readonly IContentManager _contentManager;
        private readonly ISmtpService _emailService;
        private readonly ISiteService _siteService;
        private readonly IContentItemDisplayManager _contentItemDisplayManager;
        private readonly IUpdateModelAccessor _updateModelAccessor;
        public dynamic Shape { get; set; }

        public OrderModel(IHtmlLocalizer<OrderModel> htmlLocalizer,
            IContentManager contentManager, INotifier notifier,
            IUpdateModelAccessor uma, IContentItemDisplayManager contentItemDisplayManager, ISmtpService emailService, ISiteService siteService)
        {
            _notifier = notifier;
            H = htmlLocalizer;
            _contentItemDisplayManager = contentItemDisplayManager;
            _updateModelAccessor = uma;
            _contentManager = contentManager;
            _emailService = emailService;
            _siteService = siteService;
        }
        public async Task<IActionResult> OnGetAsync(string contentItemId = null)
        {
            if (contentItemId == null)
            {
                return RedirectToAction("Index", "ShoppingCart");
            }
            var order = await _contentManager.GetAsync(contentItemId, VersionOptions.Latest);

            if (order == null)
            {
                return RedirectToAction("Index", "ShoppingCart");
            }
            else
            {
                Shape = await _contentItemDisplayManager.BuildEditorAsync(order, _updateModelAccessor.ModelUpdater, true);
            }
            return Page();
        }

        public static string GetText(ContentItem contentItem, string field)
        {
            if (contentItem.ContentType == null) return null;
            var part = contentItem.Content[contentItem.ContentType];
            if (part != null)
            {
                var fieldElement = part[field];
                if (fieldElement != null)
                {
                    return fieldElement.Text;
                }
            }
            return null;
        }

        public async Task<IActionResult> OnPostAsync(string contentItemId)
        {
            var order = await _contentManager.GetAsync(contentItemId, VersionOptions.Latest);
            Shape = await _contentItemDisplayManager.UpdateEditorAsync(order, _updateModelAccessor.ModelUpdater, true);
            var part = order.As<Order>();
            var contacts = (await _siteService.GetSiteSettingsAsync()).As<ContentItem>("GeneralSettings");
            var email = GetText(contacts, "Email");
            if (ModelState.IsValid)
            {
                await _contentManager.UpdateAsync(order);
                var result = await _contentManager.ValidateAsync(order);

                if (result.Succeeded)
                {
                    await _contentManager.PublishAsync(order);
                    await _notifier.SuccessAsync(H["Order updated successful"]);
                    await _emailService.SendAsync(new MailMessage
                    {
                        Subject = H["Thank you! Your order has been received"].Value,
                        To = part.Email?.Text,
                        Body = string.Format(H["<p>Your order link is the following {0}</p><p>Thank you!<p><p>formAdria team</p>"].Value, Request.GetDisplayUrl()),
                        IsHtmlBody = true

                    });
                    await _emailService.SendAsync(new MailMessage
                    {
                        Subject = H["New order!"].Value,
                        To = email,
                        Body = string.Format(H["<p>Order link is the following {0}</p>"].Value, Request.GetDisplayUrl()),
                        IsHtmlBody = true
                    });
                }
            }
            return Page();
        }

    }
}

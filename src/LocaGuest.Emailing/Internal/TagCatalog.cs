using System.Collections.Generic;
using System.Linq;
using LocaGuest.Emailing.Abstractions;

namespace LocaGuest.Emailing.Internal;

internal static class TagCatalog
{
    private static readonly IReadOnlyDictionary<EmailUseCaseTags, string> Map = new Dictionary<EmailUseCaseTags, string>
    {
        // Auth
        [EmailUseCaseTags.AuthResetPassword] = "auth-reset-password",
        [EmailUseCaseTags.AuthConfirmEmail] = "auth-confirm-email",
        [EmailUseCaseTags.AuthPasswordChanged] = "auth-password-changed",

        // Access
        [EmailUseCaseTags.AccessInviteUser] = "access-invite-user",

        // Billing
        [EmailUseCaseTags.BillingInvoiceSent] = "billing-invoice-sent",
        [EmailUseCaseTags.BillingReceiptSent] = "billing-receipt-sent",
        [EmailUseCaseTags.BillingPaymentReminder] = "billing-payment-reminder",
        [EmailUseCaseTags.BillingPaymentFailed] = "billing-payment-failed",

        // Rental
        [EmailUseCaseTags.RentalRentReceiptSent] = "rental-rent-receipt-sent",
        [EmailUseCaseTags.RentalContractSent] = "rental-contract-sent",
        [EmailUseCaseTags.RentalContractUpdated] = "rental-contract-updated",
        [EmailUseCaseTags.RentalSignatureReminder] = "rental-signature-reminder",

        // Inventory
        [EmailUseCaseTags.InventoryReportSent] = "inventory-report-sent",
        [EmailUseCaseTags.InventoryReportSignatureReminder] = "inventory-report-signature-reminder",
        [EmailUseCaseTags.InventoryReportUpdated] = "inventory-report-updated",

        // Generic
        [EmailUseCaseTags.NotificationWelcome] = "notification-welcome",
        [EmailUseCaseTags.NotificationSystem] = "notification-system"
    };

    internal static List<string> ResolveUseCaseTags(EmailUseCaseTags tags)
    {
        if (tags == EmailUseCaseTags.None) return new List<string>();

        var result = new List<string>();
        foreach (var kvp in Map)
        {
            if (tags.HasFlag(kvp.Key))
                result.Add(kvp.Value);
        }

        return result.Distinct(System.StringComparer.OrdinalIgnoreCase).ToList();
    }

    internal static List<string> ParseCsv(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return new List<string>();

        return csv.Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries)
                  .Where(x => !string.IsNullOrWhiteSpace(x))
                  .Distinct(System.StringComparer.OrdinalIgnoreCase)
                  .ToList();
    }
}

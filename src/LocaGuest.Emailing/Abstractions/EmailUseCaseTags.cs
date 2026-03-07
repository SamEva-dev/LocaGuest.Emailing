using System;

namespace LocaGuest.Emailing.Abstractions;

/// <summary>
/// Level-2 tags: use-case tags passed by the caller.
/// Combine with '|' (Flags) to attach multiple use-cases if needed.
/// </summary>
[Flags]
public enum EmailUseCaseTags : long
{
    None = 0,

    // ------------------
    // Auth / Security
    // ------------------
    AuthResetPassword = 1L << 0,
    AuthConfirmEmail = 1L << 1,
    AuthPasswordChanged = 1L << 2,

    // ------------------
    // Access / invitations
    // ------------------
    AccessInviteUser = 1L << 10,

    // ------------------
    // Billing / payments
    // ------------------
    BillingInvoiceSent = 1L << 20,
    BillingReceiptSent = 1L << 21,
    BillingPaymentReminder = 1L << 22,
    BillingPaymentFailed = 1L << 23,

    // ------------------
    // Rental / contracts
    // ------------------
    RentalRentReceiptSent = 1L << 30,
    RentalContractSent = 1L << 31,
    RentalContractUpdated = 1L << 32,
    RentalSignatureReminder = 1L << 33,

    // ------------------
    // Inventory / état des lieux
    // ------------------
    InventoryReportSent = 1L << 40,
    InventoryReportSignatureReminder = 1L << 41,
    InventoryReportUpdated = 1L << 42,

    // ------------------
    // Generic notifications
    // ------------------
    NotificationWelcome = 1L << 50,
    NotificationSystem = 1L << 51
}

namespace LocaGuest.Emailing.Abstractions;

public enum EmailStatus
{
    Queued = 0,
    Sending = 5,
    Sent = 10,
    Deferred = 20,
    Delivered = 30,
    Failed = 90,
    SpamComplaint = 100
}

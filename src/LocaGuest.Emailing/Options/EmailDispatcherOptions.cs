namespace LocaGuest.Emailing.Options;

public sealed class EmailDispatcherOptions
{
    public int PollSeconds { get; set; } = 5;
    public int BatchSize { get; set; } = 10;
    public int LockMinutes { get; set; } = 2;
}

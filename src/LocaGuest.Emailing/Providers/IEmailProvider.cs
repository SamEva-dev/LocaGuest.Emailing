using System.Threading;
using System.Threading.Tasks;
using LocaGuest.Emailing.Providers.Models;

namespace LocaGuest.Emailing.Providers;

internal interface IEmailProvider
{
    Task<SendResult> SendAsync(SendRequest request, CancellationToken ct);
}

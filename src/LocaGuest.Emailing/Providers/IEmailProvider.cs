using System.Threading;
using System.Threading.Tasks;
using Emailing.Providers.Models;

namespace Emailing.Providers;

internal interface IEmailProvider
{
    Task<SendResult> SendAsync(SendRequest request, CancellationToken ct);
}

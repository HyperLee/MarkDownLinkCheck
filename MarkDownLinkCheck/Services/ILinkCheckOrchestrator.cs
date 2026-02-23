using MarkDownLinkCheck.Models;

namespace MarkDownLinkCheck.Services;

/// <summary>
/// Orchestrator service that coordinates the entire link checking process.
/// </summary>
public interface ILinkCheckOrchestrator
{
    /// <summary>
    /// Executes the link checking process and streams progress events.
    /// </summary>
    /// <param name="request">Check request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of progress events</returns>
    IAsyncEnumerable<CheckProgress> ExecuteAsync(CheckRequest request, CancellationToken cancellationToken);
}

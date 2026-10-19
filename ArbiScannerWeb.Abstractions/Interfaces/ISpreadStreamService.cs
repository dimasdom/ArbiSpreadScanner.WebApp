using ArbiScannerWeb.Domain.Models.DTOs;
using FluentResults;

namespace ArbiScannerWeb.Abstractions.Interfaces;

public interface ISpreadStreamService
{
    /// <summary>
    /// Opens the live event stream of one spread for the current user. Fails with Forbidden (no active subscription) or NotFound
    /// (unknown spread); otherwise the returned sequence yields events until the spread closes, the lifetime cap passes or
    /// <paramref name="cancellationToken"/> fires. The live subscription starts when enumeration starts, before any replay, so
    /// no event published in between can be missed.
    /// </summary>
    /// <param name="lastEventId">The <c>Last-Event-ID</c> cursor (Unix milliseconds of the last ticker the client has). When
    /// set, tickers recorded after it are replayed first, without order books. When <c>null</c> nothing is replayed.</param>
    Task<Result<IAsyncEnumerable<SpreadStreamEvent>>> OpenAsync(Guid spreadId, long? lastEventId, CancellationToken cancellationToken);
}

using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.API.Streaming;
using ArbiScannerWeb.Infrastructure.Extensions;
using FluentResults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ArbiScannerWeb.API.Controllers
{
    /// <summary>Server-Sent Events stream of one spread's live tickers. Replaces the SignalR hub: the spread is in the route (so
    /// there is no client-chosen group to join) and the subscription check runs on every connect.</summary>
    [ApiController]
    [Authorize]
    [Route("api/TradeOpportunity")]
    public class SpreadStreamController(ISpreadStreamService streams, ILogger<SpreadStreamController> logger) : ControllerBase
    {
        private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(3);

        /// <summary>Send <c>Last-Event-ID</c> (Unix ms of the newest ticker the client already has) to receive the tickers it
        /// missed, without order books, before the live ones.</summary>
        [HttpGet("Stream/{id:guid}")]
        [DisableRateLimiting] // a held-open stream is one request; reconnects after the lifetime cap must not eat the 60/min budget
        public async Task<IActionResult> Stream(Guid id, CancellationToken cancellationToken)
        {
            var result = await streams.OpenAsync(id, ParseLastEventId(Request.Headers["Last-Event-ID"].ToString()), cancellationToken);
            if (result.IsFailed)
            {
                return StatusCode(StatusCodeOf(result));
            }

            Response.Headers.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache, no-transform";
            Response.Headers["X-Accel-Buffering"] = "no"; // tells nginx not to buffer this response

            try
            {
                await Response.WriteAsync(SseFormatter.Preamble(ReconnectDelay), cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);

                await foreach (var streamEvent in result.Value.WithCancellation(cancellationToken))
                {
                    await Response.WriteAsync(SseFormatter.Format(streamEvent), cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The client went away; nothing to report.
            }
            catch (Exception ex)
            {
                // Headers are already sent, so the error middleware can no longer help. Ending the stream makes the client
                // reconnect and replay from its last event id.
                logger.LogError(ex, "Spread stream {SpreadId} failed", id);
            }

            return new EmptyResult();
        }

        private static long? ParseLastEventId(string header) =>
            long.TryParse(header, out var value) && value >= 0 ? value : null;

        private static int StatusCodeOf(ResultBase result) =>
            result.Errors.FirstOrDefault()?.Metadata.GetValueOrDefault(TypedErrors.HttpStatusCodeMetadataKey) is int code
                ? code
                : StatusCodes.Status500InternalServerError;
    }
}

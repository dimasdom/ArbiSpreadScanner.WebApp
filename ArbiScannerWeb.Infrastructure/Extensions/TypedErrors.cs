using System.Net;
using FluentResults;

namespace ArbiScannerWeb.Infrastructure.Extensions
{
    public static class TypedErrors
    {
        public const string HttpStatusCodeMetadataKey = "HttpStatusCode";
        public const string ErrorCodeMetadataKey = "ErrorCode";

        public static Error NotFound(string message) => Build(message, HttpStatusCode.NotFound, ErrorCodes.NotFound);

        public static Error Validation(string message) => Build(message, HttpStatusCode.BadRequest, ErrorCodes.Validation);

        public static Error Unauthorized(string message) => Build(message, HttpStatusCode.Unauthorized, ErrorCodes.Unauthorized);

        public static Error Forbidden(string message) => Build(message, HttpStatusCode.Forbidden, ErrorCodes.Forbidden);

        public static Error Conflict(string message) => Build(message, HttpStatusCode.Conflict, ErrorCodes.Conflict);

        private static Error Build(string message, HttpStatusCode statusCode, string errorCode)
        {
            return new Error(message)
                .WithMetadata(HttpStatusCodeMetadataKey, (int)statusCode)
                .WithMetadata(ErrorCodeMetadataKey, errorCode);
        }
    }
}

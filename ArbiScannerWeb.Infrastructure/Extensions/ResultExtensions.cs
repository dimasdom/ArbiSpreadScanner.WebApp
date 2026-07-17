using FluentResults;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArbiScannerWeb.Infrastructure.Extensions
{
    public class SerializableResult<T> : Result<T>
    {
        public new bool IsSuccess { get; set; }
        public new bool IsFailed => !IsSuccess;
        public new T? Value { get; set; }
        public new List<FluentResults.IReason> Reasons { get; set; } = new();
        public string? ErrorCode { get; set; }
        public string Message { get; set; } = string.Empty;

        public static SerializableResult<T> FromResult(Result<T> result)
        {
            var firstReason = result.Reasons.FirstOrDefault();
            return new SerializableResult<T>
            {
                IsSuccess = result.IsSuccess,
                Value = result.IsSuccess ? result.Value : default,
                Reasons = result.Reasons.ToList(),
                ErrorCode = result.IsSuccess ? null : ResultStatusCode.GetErrorCode(firstReason),
                Message = result.IsSuccess ? string.Empty : (firstReason?.Message ?? string.Empty)
            };
        }
    }

    public class SerializableResult : Result
    {
        public new bool IsSuccess { get; set; }
        public new bool IsFailed => !IsSuccess;
        public new List<FluentResults.IReason> Reasons { get; set; } = new();
        public string? ErrorCode { get; set; }
        public string Message { get; set; } = string.Empty;

        public static SerializableResult FromResult(Result result)
        {
            var firstReason = result.Reasons.FirstOrDefault();
            return new SerializableResult
            {
                IsSuccess = result.IsSuccess,
                Reasons = result.Reasons.ToList(),
                ErrorCode = result.IsSuccess ? null : ResultStatusCode.GetErrorCode(firstReason),
                Message = result.IsSuccess ? string.Empty : (firstReason?.Message ?? string.Empty)
            };
        }
    }

    public static class ResultStatusCode
    {
        public static string GetErrorCode(FluentResults.IReason? reason)
        {
            if (reason is not null && reason.Metadata.TryGetValue(TypedErrors.ErrorCodeMetadataKey, out var code) && code is string codeString)
            {
                return codeString;
            }

            return ErrorCodes.BadRequest;
        }

        public static int GetHttpStatusCode(FluentResults.IReason? reason)
        {
            if (reason is not null && reason.Metadata.TryGetValue(TypedErrors.HttpStatusCodeMetadataKey, out var statusCode) && statusCode is int statusCodeInt)
            {
                return statusCodeInt;
            }

            return 400;
        }
    }

    public static class ResultExtensions
    {
        public static SerializableResult<T> ToSerializable<T>(this Result<T> result)
        {
            return SerializableResult<T>.FromResult(result);
        }

        public static SerializableResult ToSerializable(this Result result)
        {
            return SerializableResult.FromResult(result);
        }
    }
}

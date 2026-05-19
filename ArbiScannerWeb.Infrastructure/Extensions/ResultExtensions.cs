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

        public static SerializableResult<T> FromResult(Result<T> result)
        {
            return new SerializableResult<T>
            {
                IsSuccess = result.IsSuccess,
                Value = result.IsSuccess ? result.Value : default,
                Reasons = result.Reasons.ToList()
            };
        }
    }
    public static class ResultExtensions
    {
        public static SerializableResult<T> ToSerializable<T>(this Result<T> result)
        {
            return SerializableResult<T>.FromResult(result);
        }
    }
}

using FluentResults;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArbiScannerWeb.API.Extensions
{
    public class SerializableResult<T>
    {
        public bool IsSuccess { get; set; }
        public bool IsFailed => !IsSuccess;
        public T? Value { get; set; }
        public List<FluentResults.IReason> Reasons { get; set; } = new();

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

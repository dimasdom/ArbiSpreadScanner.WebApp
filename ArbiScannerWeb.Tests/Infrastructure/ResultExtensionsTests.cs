using ArbiScannerWeb.Infrastructure.Extensions;
using FluentAssertions;
using FluentResults;

namespace ArbiScannerWeb.Tests.Infrastructure;

public class ResultExtensionsTests
{
    [Fact]
    public void ToSerializable_GenericSuccess_CopiesValueAndClearsError()
    {
        var result = Result.Ok("payload");

        var serializable = result.ToSerializable();

        serializable.IsSuccess.Should().BeTrue();
        serializable.Value.Should().Be("payload");
        serializable.ErrorCode.Should().BeNull();
        serializable.Message.Should().BeEmpty();
    }

    [Fact]
    public void ToSerializable_GenericFailure_SetsErrorCodeAndMessage()
    {
        var result = Result.Fail<string>(TypedErrors.NotFound("missing"));

        var serializable = result.ToSerializable();

        serializable.IsSuccess.Should().BeFalse();
        serializable.Value.Should().BeNull();
        serializable.ErrorCode.Should().Be(ErrorCodes.NotFound);
        serializable.Message.Should().Be("missing");
    }

    [Fact]
    public void ToSerializable_NonGenericSuccess_ClearsError()
    {
        var result = Result.Ok();

        var serializable = result.ToSerializable();

        serializable.IsSuccess.Should().BeTrue();
        serializable.ErrorCode.Should().BeNull();
        serializable.Message.Should().BeEmpty();
    }

    [Fact]
    public void ToSerializable_NonGenericFailure_SetsErrorCodeAndMessage()
    {
        var result = Result.Fail(TypedErrors.Validation("bad input"));

        var serializable = result.ToSerializable();

        serializable.IsSuccess.Should().BeFalse();
        serializable.ErrorCode.Should().Be(ErrorCodes.Validation);
        serializable.Message.Should().Be("bad input");
    }

    [Fact]
    public void GetErrorCode_NullReason_ReturnsBadRequest()
    {
        ResultStatusCode.GetErrorCode(null).Should().Be(ErrorCodes.BadRequest);
    }

    [Fact]
    public void GetErrorCode_ReasonWithoutMetadata_ReturnsBadRequest()
    {
        var reason = new Error("plain");

        ResultStatusCode.GetErrorCode(reason).Should().Be(ErrorCodes.BadRequest);
    }

    [Fact]
    public void GetErrorCode_ReasonWithNonStringMetadata_ReturnsBadRequest()
    {
        var reason = new Error("weird").WithMetadata(TypedErrors.ErrorCodeMetadataKey, 123);

        ResultStatusCode.GetErrorCode(reason).Should().Be(ErrorCodes.BadRequest);
    }

    [Fact]
    public void GetErrorCode_ReasonWithStringMetadata_ReturnsThatCode()
    {
        var reason = new Error("conflict").WithMetadata(TypedErrors.ErrorCodeMetadataKey, ErrorCodes.Conflict);

        ResultStatusCode.GetErrorCode(reason).Should().Be(ErrorCodes.Conflict);
    }

    [Fact]
    public void GetHttpStatusCode_NullReason_Returns400()
    {
        ResultStatusCode.GetHttpStatusCode(null).Should().Be(400);
    }

    [Fact]
    public void GetHttpStatusCode_ReasonWithoutMetadata_Returns400()
    {
        var reason = new Error("plain");

        ResultStatusCode.GetHttpStatusCode(reason).Should().Be(400);
    }

    [Fact]
    public void GetHttpStatusCode_ReasonWithNonIntMetadata_Returns400()
    {
        var reason = new Error("weird").WithMetadata(TypedErrors.HttpStatusCodeMetadataKey, "not-an-int");

        ResultStatusCode.GetHttpStatusCode(reason).Should().Be(400);
    }

    [Fact]
    public void GetHttpStatusCode_ReasonWithIntMetadata_ReturnsThatCode()
    {
        var reason = TypedErrors.Forbidden("nope");

        ResultStatusCode.GetHttpStatusCode(reason).Should().Be(403);
    }
}

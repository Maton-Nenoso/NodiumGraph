using NodiumGraph;
using Xunit;

namespace NodiumGraph.Tests;

public class ResultTests
{
    [Fact]
    public void Success_returns_successful_result()
    {
        var result = Result.Success();
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_returns_failed_result_with_error()
    {
        var error = new Error("Something went wrong");
        var result = Result.Failure(error);
        Assert.False(result.IsSuccess);
        Assert.Equal("Something went wrong", result.Error!.Message);
    }

    [Fact]
    public void Failure_with_code_preserves_code()
    {
        var error = new Error("Bad input", "INVALID_INPUT");
        Assert.Equal("INVALID_INPUT", error.Code);
    }

    [Fact]
    public void Implicit_conversion_from_error_to_result()
    {
        var error = new Error("fail");
        Result result = error;
        Assert.False(result.IsSuccess);
        Assert.Equal("fail", result.Error!.Message);
    }

    [Fact]
    public void Failure_with_null_error_throws()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Failure(null!));
    }
}

public class ResultOfTTests
{
    [Fact]
    public void Implicit_conversion_from_value_creates_success()
    {
        Result<int> result = 42;
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Implicit_conversion_from_error_creates_failure()
    {
        Result<int> result = new Error("nope");
        Assert.False(result.IsSuccess);
        Assert.Equal(default, result.Value);
    }

    [Fact]
    public void Success_result_has_no_error()
    {
        Result<string> result = "hello";
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failed_result_has_default_value()
    {
        Result<string> result = new Error("fail");
        Assert.Null(result.Value);
    }
}

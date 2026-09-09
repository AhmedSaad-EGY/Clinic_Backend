using Clinic.Application.Common;

namespace Clinic.Application.UnitTests.Common;

public sealed class ResultTests
{
    [Fact]
    public void SuccessWithValueExposesValue()
    {
        Result<int> result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Equal(ResultError.None, result.Error);
    }

    [Fact]
    public void FailureWithValueThrowsWhenValueIsRead()
    {
        ResultError error = new("patients.not_found", "The patient was not found.");
        Result<int> result = Result.Failure<int>(error);

        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }
}

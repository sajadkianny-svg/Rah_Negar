using Rah_Negar.Foundation.Errors;

namespace Rah_Negar.Tests.Foundation;

public sealed class ResultTests
{
    [Fact]
    public void Success_exposes_value_without_error()
    {
        Result<int> result = Result<int>.Success(42);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(42, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_exposes_error_and_rejects_value_access()
    {
        ApplicationError error = ApplicationError.Create("foundation.failure", "Failure");
        Result<int> result = Result<int>.Failure(error);

        Assert.True(result.IsFailure);
        Assert.Same(error, result.Error);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }
}

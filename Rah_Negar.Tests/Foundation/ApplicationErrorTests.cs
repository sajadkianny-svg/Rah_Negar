using Rah_Negar.Foundation.Errors;

namespace Rah_Negar.Tests.Foundation;

public sealed class ApplicationErrorTests
{
    [Fact]
    public void Create_preserves_safe_error_fields()
    {
        ApplicationError error = ApplicationError.Create(
            "foundation.invalid",
            "The input is invalid.",
            "field=sample");

        Assert.Equal("foundation.invalid", error.Code);
        Assert.Equal("The input is invalid.", error.Message);
        Assert.Equal("field=sample", error.Detail);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_rejects_empty_code(string code)
    {
        Assert.Throws<ArgumentException>(() => ApplicationError.Create(code, "Message"));
    }
}

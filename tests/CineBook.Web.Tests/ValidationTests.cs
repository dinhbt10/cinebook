using System.ComponentModel.DataAnnotations;
using CineBook.Web.Models;
using Xunit;

namespace CineBook.Web.Tests;

public class ValidationTests
{
    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Login_requires_email_and_password()
    {
        var errors = Validate(new LoginViewModel());
        Assert.Contains(errors, e => e.ErrorMessage == "Vui lòng nhập email.");
        Assert.Contains(errors, e => e.ErrorMessage == "Vui lòng nhập mật khẩu.");
    }

    [Fact]
    public void Login_rejects_invalid_email()
    {
        var errors = Validate(new LoginViewModel { Email = "khong-phai-email", Password = "123456" });
        Assert.Single(errors);
        Assert.Equal("Email không đúng định dạng.", errors[0].ErrorMessage);
    }

    [Fact]
    public void Register_requires_password_of_at_least_six_characters()
    {
        var model = new RegisterViewModel { FullName = "A", Email = "a@b.vn", Password = "123" };
        var errors = Validate(model);
        Assert.Contains(errors, e => e.ErrorMessage == "Mật khẩu phải có ít nhất 6 ký tự.");
    }

    [Fact]
    public void Register_accepts_valid_data_without_phone()
    {
        var model = new RegisterViewModel { FullName = "Nguyễn Văn A", Email = "a@b.vn", Password = "123456" };
        Assert.Empty(Validate(model));
    }
}

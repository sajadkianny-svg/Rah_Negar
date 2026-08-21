using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Data.Sqlite;
using Rah_Negar.Data;
using Rah_Negar.Models;
using Rah_Negar.Utils;

namespace Rah_Negar.Services;

/// <summary>
/// مدیریت عملیات مربوط به رمز عبور
/// </summary>
public static class PasswordManagementService
{
    /// <summary>
    /// بررسی صحت رمز فعلی
    /// </summary>
    public static bool VerifyCurrentPassword(string enteredPassword)
    {
        if (string.IsNullOrWhiteSpace(enteredPassword))
            return false;

        AppSettingsModel? settings = AppSettingsService.GetSettings();

        if (settings == null)
            return false;

        return PasswordHelper.VerifyPassword(
            enteredPassword,
            settings.UserResetPasswordSalt,
            settings.UserResetPasswordHash);
    }


    /// <summary>
    /// اعتبارسنجی اولیه رمز جدید
    /// </summary>
    public static bool ValidateNewPassword(string password, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(password))
        {
            errorMessage = "رمز جدید خالی است";
            return false;
        }

        password = password.Trim();

        if (password.Length < 4)
        {
            errorMessage = "رمز جدید باید حداقل 4 کاراکتر باشد";
            return false;
        }

        if (password.Contains(' '))
        {
            errorMessage = "رمز جدید نباید فاصله داشته باشد";
            return false;
        }

        if (password.All(char.IsDigit))
        {
            errorMessage = "رمز جدید باید شامل عدد و حروف باشد";
            return false;
        }

        string[] weakPasswords =
        {
        "123456",
        "12345678",
        "111111",
        "000000",
        "abcdef",
        "aaaaaa",
        "password",
        "qwerty"
    };

        if (weakPasswords.Contains(password, StringComparer.OrdinalIgnoreCase))
        {
            errorMessage = "رمز انتخاب شده بسیار ضعیف است";
            return false;
        }

        return true;
    }


    /// <summary>
    /// بروزرسانی رمز عبور
    /// </summary>
    public static void UpdatePassword(string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
            throw new ArgumentException("Password is empty");

        string salt = PasswordHelper.CreateSalt();
        string hash = PasswordHelper.HashPassword(newPassword, salt);

        AppSettingsService.UpdatePassword(hash, salt);
    }
}

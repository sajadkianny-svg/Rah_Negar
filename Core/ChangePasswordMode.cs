using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rah_Negar.Core;

/// <summary>
/// تعیین می‌کند فرم تغییر رمز در چه حالتی باز شده است:
/// - Normal: تغییر رمز عادی با نیاز به رمز فعلی
/// - Recovery: تغییر رمز بعد از بازیابی، بدون نیاز به رمز فعلی
/// </summary>
public enum ChangePasswordMode
{
    Normal = 1,
    Recovery = 2
}

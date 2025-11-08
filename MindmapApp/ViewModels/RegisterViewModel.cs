using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using MindmapApp.Commands;
using MindmapApp.Services;

namespace MindmapApp.ViewModels;

public class RegisterViewModel : BaseViewModel
{
    private readonly UserService _userService;

    private string _email = string.Empty;
    private string _displayName = string.Empty;
    private string _errorMessage = string.Empty;
    private string _successMessage = string.Empty;
    private bool _isBusy;

    // === Regex email tối giản cho client ===
    private static readonly Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    public RegisterViewModel(UserService userService)
    {
        _userService = userService;
        RegisterCommand = new AsyncRelayCommand(ExecuteRegisterAsync, _ => !IsBusy);
        BackCommand = new RelayCommand(_ => BackRequested?.Invoke(this, EventArgs.Empty));
    }

    public event EventHandler? BackRequested;
    public event EventHandler? RegisteredSuccessfully;

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, (value ?? string.Empty).Trim());
    }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value ?? string.Empty);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public string SuccessMessage
    {
        get => _successMessage;
        private set => SetProperty(ref _successMessage, value);
    }

    public bool IsBusy
    {
        //biến lưu tỉnh trạng xử lý: true: đang xử lý và ngược lại 
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
                (RegisterCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public ICommand RegisterCommand { get; }
    public ICommand BackCommand { get; }

    private async Task ExecuteRegisterAsync(object? parameter)
    {
        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            SuccessMessage = string.Empty;

            if (parameter is not Tuple<string, string> passwords)
            {
                ErrorMessage = "Không lấy được mật khẩu";
                return;
            }

            var (password, confirm) = passwords;

            // === VALIDATE TẠI ĐÂY ===
            // 1) Email
            var emailErr = ValidateEmail(Email);
            if (emailErr is not null) { ErrorMessage = emailErr; return; }

            // 2) DisplayName
            var nameErr = ValidateDisplayName(DisplayName);
            if (nameErr is not null) { ErrorMessage = nameErr; return; }

            // 3) Password + Confirm
            var pwdErr = ValidatePassword(password, Email, DisplayName);
            if (pwdErr is not null) { ErrorMessage = pwdErr; return; }

            if (!string.Equals(password, confirm, StringComparison.Ordinal))
            {
                ErrorMessage = "Mật khẩu xác nhận không khớp";
                return;
            }

            // (Tùy chọn) blacklist các mật khẩu quá phổ biến
            if (IsCommonPassword(password))
            {
                ErrorMessage = "Mật khẩu quá phổ biến, vui lòng chọn mật khẩu mạnh hơn";
                return;
            }

            // 4) Gọi service: kiểm tra trùng & đăng ký
            var success = await _userService.RegisterAsync(Email, password, DisplayName);
            if (!success)
            {
                ErrorMessage = "Email đã được sử dụng";
                return;
            }

            SuccessMessage = "Đăng ký thành công! Bạn có thể đăng nhập ngay bây giờ.";
            RegisteredSuccessfully?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ======================
    // Các hàm validate cục bộ
    // ======================

    private string? ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "Vui lòng nhập Email";
        if (!EmailRegex.IsMatch(email.Trim())) return "Email không hợp lệ";
        return null;
    }

    private string? ValidateDisplayName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Vui lòng nhập tên hiển thị";
        var t = name.Trim();
        if (t.Length < 2 || t.Length > 40) return "Tên hiển thị 2–40 ký tự";

        // (Khuyến nghị) Chỉ cho chữ cái/số/khoảng trắng
        if (!Regex.IsMatch(t, @"^[\p{L}\p{N} ]+$"))
            return "Tên chỉ được chứa chữ cái, số và khoảng trắng";

        // (Khuyến nghị) Cấm từ nhạy cảm
        if (Regex.IsMatch(t, @"\b(admin|root|system)\b", RegexOptions.IgnoreCase))
            return "Tên này không được phép sử dụng";

        return null;
    }

    private string? ValidatePassword(string pwd, string email, string displayName)
    {
        if (string.IsNullOrEmpty(pwd)) return "Vui lòng nhập mật khẩu";
        if (pwd.Length < 10) return "Mật khẩu tối thiểu 10 ký tự";

        int classes = 0;
        if (pwd.Any(char.IsLower)) classes++;
        if (pwd.Any(char.IsUpper)) classes++;
        if (pwd.Any(char.IsDigit)) classes++;
        if (pwd.Any(ch => !char.IsLetterOrDigit(ch))) classes++;
        if (classes < 3) return "Mật khẩu cần ít nhất 3/4 nhóm: a-z, A-Z, 0-9, ký tự đặc biệt";

        var low = pwd.ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(email) && low.Contains(email.ToLowerInvariant()))
            return "Mật khẩu không được chứa Email";
        if (!string.IsNullOrWhiteSpace(displayName) && low.Contains(displayName.ToLowerInvariant()))
            return "Mật khẩu không được chứa tên hiển thị";

        // (Khuyến nghị) Không có khoảng trắng
        if (pwd.Any(char.IsWhiteSpace)) return "Mật khẩu không được chứa khoảng trắng";

        return null;
    }

    private bool IsCommonPassword(string pwd)
    {
        // Danh sách mẫu, có thể mở rộng theo nhu cầu
        string[] common = { "password", "123456", "123456789", "qwerty", "letmein", "admin" };
        return common.Any(p => string.Equals(p, pwd, StringComparison.OrdinalIgnoreCase));
    }
}

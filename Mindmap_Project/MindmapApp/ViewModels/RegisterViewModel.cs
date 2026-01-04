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

    // Optional: public wrapper to call registration directly from code-behind
    public Task RegisterAsync(string password, string confirm) => ExecuteRegisterAsync(Tuple.Create(password, confirm));

    private async Task ExecuteRegisterAsync(object? parameter)
    {
        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            SuccessMessage = string.Empty;

            // Support several parameter shapes from different UI code-behinds
            string? password = null;
            string? confirm = null;

            if (parameter is Tuple<string, string> two)
            {
                password = two.Item1;
                confirm = two.Item2;
            }
            else if (parameter is Tuple<string, string, string> three)
            {
                password = three.Item1;
                confirm = three.Item2;
            }
            else if (parameter is object[] arr && arr.Length >= 2 && arr[0] is string p0 && arr[1] is string p1)
            {
                password = p0;
                confirm = p1;
            }
            else if (parameter is ValueTuple<string, string> vtuple)
            {
                password = vtuple.Item1;
                confirm = vtuple.Item2;
            }
            else if (parameter is string single)
            {
                // If caller passed only password (unlikely for register), treat as both
                password = single;
                confirm = single;
            }

            password = password?.Trim();
            confirm = confirm?.Trim();

            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(confirm))
            {
                ErrorMessage = "Không lấy được mật khẩu";
                return;
            }

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
        return null;
    }

    private string? ValidatePassword(string pwd, string email, string displayName)
    {
        if (string.IsNullOrEmpty(pwd)) return "Vui lòng nhập mật khẩu";

        // Logic mới: Tối thiểu 8 ký tự, 1 hoa, 1 thường, 1 ký tự đặc biệt
        if (pwd.Length < 8) return "Mật khẩu tối thiểu 8 ký tự";
        if (!Regex.IsMatch(pwd, @"[a-z]")) return "Mật khẩu cần ít nhất 1 chữ thường";
        if (!Regex.IsMatch(pwd, @"[A-Z]")) return "Mật khẩu cần ít nhất 1 chữ hoa";
        if (!Regex.IsMatch(pwd, @"[\W_]")) return "Mật khẩu cần ít nhất 1 ký tự đặc biệt";

        return null;
    }

    private bool IsCommonPassword(string pwd) => false;
}
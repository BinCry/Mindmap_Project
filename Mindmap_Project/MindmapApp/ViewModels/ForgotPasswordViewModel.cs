using System;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using MindmapApp.Commands;
using MindmapApp.Services;

namespace MindmapApp.ViewModels;

public class ForgotPasswordViewModel : BaseViewModel
{
    private readonly UserService _userService;
    private readonly EmailService _emailService;
    private string _email = string.Empty;
    private string _otp = string.Empty;
    private string _errorMessage = string.Empty;
    private string _successMessage = string.Empty;
    private bool _isBusy;
    private bool _isOtpSent;

    // In-memory OTP
    private string? _generatedOtp;
    private DateTime? _otpExpiration;

    // Dev flag to show OTP in logs for testing only (default false)
    public static bool DevShowOtp { get; set; } = false;

    public ForgotPasswordViewModel(UserService userService, EmailService emailService)
    {
        _userService = userService;
        _emailService = emailService;

        SendOtpCommand = new AsyncRelayCommand(ExecuteSendOtpAsync, _ => !IsBusy);
        ResetPasswordCommand = new AsyncRelayCommand(ExecuteResetPasswordAsync, _ => !IsBusy && IsOtpSent);
        BackCommand = new RelayCommand(_ => BackRequested?.Invoke(this, EventArgs.Empty));
    }

    public event EventHandler? BackRequested;
    public event EventHandler? PasswordResetSuccessfully;

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string Otp
    {
        get => _otp;
        set => SetProperty(ref _otp, value);
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
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                (SendOtpCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                (ResetPasswordCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsOtpSent
    {
        get => _isOtpSent;
        private set
        {
            if (SetProperty(ref _isOtpSent, value))
            {
                (ResetPasswordCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public ICommand SendOtpCommand { get; }
    public ICommand ResetPasswordCommand { get; }
    public ICommand BackCommand { get; }

    private async Task ExecuteSendOtpAsync(object? parameter)
    {
        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            SuccessMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(Email))
            {
                ErrorMessage = "Vui lòng nhập Email";
                return;
            }

            if (!await _userService.CheckEmailExistsAsync(Email))
            {
                ErrorMessage = "Email này không tồn tại trong hệ thống. Vui lòng kiểm tra lại.";
                return;
            }

            var random = new Random();
            var code = random.Next(100000, 999999).ToString(CultureInfo.InvariantCulture);
            var tempExpiry = DateTime.UtcNow.AddMinutes(10);

            try
            {
                await _emailService.SendOtpAsync(Email, code);

                _generatedOtp = code;
                _otpExpiration = tempExpiry;
                IsOtpSent = true;
                SuccessMessage = $"Đã gửi OTP về Email '{Email}'. Mã có hiệu lực đến {_otpExpiration.Value.ToLocalTime():HH:mm:ss}.";

                if (DevShowOtp)
                {
                    Debug.WriteLine($"DEV OTP for {Email}: {code}");
                }
            }
            catch (InvalidOperationException)
            {
                ErrorMessage = "SMTP chưa cấu hình. Vui lòng sao chép 'settings.example.json' thành settings.json tại %APPDATA%\\MindmapApp và điền cấu hình SMTP (SenderEmail, SenderPassword...).";
                IsOtpSent = false;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Không thể gửi email: {ex.Message}";
                IsOtpSent = false;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Lỗi gửi OTP: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteResetPasswordAsync(object? parameter)
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

            // Validate mật khẩu mới theo chuẩn bảo mật
            var pwdErr = ValidatePassword(password);
            if (pwdErr is not null)
            {
                ErrorMessage = pwdErr;
                return;
            }

            if (string.IsNullOrWhiteSpace(confirm))
            {
                ErrorMessage = "Vui lòng xác nhận mật khẩu.";
                return;
            }

            if (!string.Equals(password, confirm, StringComparison.Ordinal))
            {
                ErrorMessage = "Mật khẩu xác nhận không khớp";
                return;
            }

            if (string.IsNullOrWhiteSpace(_generatedOtp) || _otpExpiration == null)
            {
                ErrorMessage = "Vui lòng bấm 'Gửi OTP' trước.";
                return;
            }

            if (_otpExpiration.Value <= DateTime.UtcNow)
            {
                ErrorMessage = "Mã OTP đã hết hạn. Vui lòng gửi lại.";
                return;
            }

            // Kiểm tra OTP tại Client trước để đỡ tốn request (Optional)
            if (!string.Equals(Otp.Trim(), _generatedOtp.Trim(), StringComparison.Ordinal))
            {
                ErrorMessage = "OTP không chính xác.";
                return;
            }

            // 🔥 SỬA ĐỔI QUAN TRỌNG: Gọi ResetPasswordAsync (Gửi OTP lên Server xác thực)
            // Thay vì UpdatePasswordAsync chỉ chạy local
            var success = await _userService.ResetPasswordAsync(Email, Otp, password);

            if (!success)
            {
                ErrorMessage = "Đổi mật khẩu thất bại. Kiểm tra lại kết nối mạng hoặc OTP.";
                return;
            }

            _generatedOtp = null;
            _otpExpiration = null;

            SuccessMessage = "Cập nhật mật khẩu thành công!";
            PasswordResetSuccessfully?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Lỗi cập nhật mật khẩu: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private string? ValidatePassword(string pwd)
    {
        if (string.IsNullOrEmpty(pwd)) return "Vui lòng nhập mật khẩu mới";
        if (pwd.Length < 8) return "Mật khẩu tối thiểu 8 ký tự";
        if (!Regex.IsMatch(pwd, @"[a-z]")) return "Mật khẩu cần ít nhất 1 chữ thường";
        if (!Regex.IsMatch(pwd, @"[A-Z]")) return "Mật khẩu cần ít nhất 1 chữ hoa";
        if (!Regex.IsMatch(pwd, @"[\W_]")) return "Mật khẩu cần ít nhất 1 ký tự đặc biệt";
        return null;
    }
}

using System;
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

            var code = await _userService.CreateOtpAsync(Email, TimeSpan.FromMinutes(10));
            if (string.IsNullOrWhiteSpace(code))
            {
                ErrorMessage = "Không thể tạo mã OTP. Vui lòng thử lại";
                return;
            }

            await _emailService.SendOtpAsync(Email, code);
            IsOtpSent = true;
            SuccessMessage = "Đã gửi OTP về Email của bạn. Vui lòng kiểm tra hộp thư";
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
            if (!string.Equals(password, confirm, StringComparison.Ordinal))
            {
                ErrorMessage = "Mật khẩu xác nhận không khớp";
                return;
            }

            if (!await _userService.ValidateOtpAsync(Email, Otp))
            {
                ErrorMessage = "OTP không hợp lệ hoặc đã hết hạn";
                return;
            }

            var success = await _userService.UpdatePasswordAsync(Email, password);
            if (!success)
            {
                ErrorMessage = "Không thể cập nhật mật khẩu";
                return;
            }

            SuccessMessage = "Cập nhật mật khẩu thành công!";
            PasswordResetSuccessfully?.Invoke(this, EventArgs.Empty);
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
}

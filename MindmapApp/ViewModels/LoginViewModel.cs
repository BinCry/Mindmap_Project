using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using MindmapApp.Commands;
using MindmapApp.Models;
using MindmapApp.Services;

namespace MindmapApp.ViewModels;

public class LoginViewModel : BaseViewModel
{
    private readonly UserService _userService;
    private string _email = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;

    // (Optional) khóa tạm sau nhiều lần sai
    private int _failedCount = 0;
    private DateTime _lockUntil = DateTime.MinValue;

    private static readonly Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    public LoginViewModel(UserService userService)
    {
        _userService = userService;
        LoginCommand = new AsyncRelayCommand(ExecuteLoginAsync, _ => !IsBusy);
        OpenRegisterCommand = new RelayCommand(_ => RegisterRequested?.Invoke(this, EventArgs.Empty));
        OpenForgotPasswordCommand = new RelayCommand(_ => ForgotPasswordRequested?.Invoke(this, EventArgs.Empty));
    }

    public event EventHandler<UserAccount>? LoginSucceeded;
    public event EventHandler? RegisterRequested;
    public event EventHandler? ForgotPasswordRequested;

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, (value ?? string.Empty).Trim());
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
                (LoginCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public ICommand LoginCommand { get; }
    public ICommand OpenRegisterCommand { get; }
    public ICommand OpenForgotPasswordCommand { get; }

    private async Task ExecuteLoginAsync(object? parameter)
    {
        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;

            // (Optional) chống brute-force tối giản
            if (DateTime.UtcNow < _lockUntil)
            {
                var wait = (_lockUntil - DateTime.UtcNow).Seconds;
                ErrorMessage = $"Bạn thử sai quá nhiều lần. Vui lòng thử lại sau {Math.Max(wait, 1)} giây.";
                return;
            }

            var password = parameter as string ?? string.Empty;

            // Check tối thiểu
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(password))
            {
                ErrorMessage = "Vui lòng nhập Email và Mật khẩu";
                return;
            }
            if (!EmailRegex.IsMatch(Email))
            {
                ErrorMessage = "Email không hợp lệ";
                return;
            }

            var account = await _userService.AuthenticateAsync(Email, password);
            if (account == null)
            {
                // (Optional) đếm số lần sai
                _failedCount++;
                if (_failedCount >= 5)
                {
                    _lockUntil = DateTime.UtcNow.AddSeconds(45); // khoá tạm 45s
                    _failedCount = 0;
                }

                ErrorMessage = "Email hoặc Mật khẩu chưa đúng";
                return;
            }

            // Thành công
            _failedCount = 0;
            _lockUntil = DateTime.MinValue;
            LoginSucceeded?.Invoke(this, account);
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

using System;
using System.Windows;
using MindmapApp.ViewModels;

namespace MindmapApp.Views;

public partial class ForgotPasswordWindow : Window
{
    private readonly ForgotPasswordViewModel _viewModel;

    public ForgotPasswordWindow()
    {
        InitializeComponent();
        _viewModel = new ForgotPasswordViewModel(App.UserService, App.EmailService);
        DataContext = _viewModel;
        _viewModel.BackRequested += (_, _) => Close();
        _viewModel.PasswordResetSuccessfully += (_, _) =>
        {
            MessageBox.Show(this, "Đã cập nhật mật khẩu thành công", "MindmapApp", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        };
    }

    private void SendOtpButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.SendOtpCommand.Execute(null);
    }

    private void ResetPasswordButton_OnClick(object sender, RoutedEventArgs e)
    {
        var passwords = Tuple.Create(NewPasswordBox.Password, ConfirmNewPasswordBox.Password);
        _viewModel.ResetPasswordCommand.Execute(passwords);
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

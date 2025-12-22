using System;
using System.Windows;
using System.Windows.Controls;
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

    // --- LOGIC ẨN/HIỆN MẬT KHẨU ---
    private void ShowNewPassword_Checked(object sender, RoutedEventArgs e)
    {
        VisibleNewPasswordBox.Text = NewPasswordBox.Password;
        VisibleNewPasswordBox.Visibility = Visibility.Visible;
        NewPasswordBox.Visibility = Visibility.Collapsed;
    }

    private void ShowNewPassword_Unchecked(object sender, RoutedEventArgs e)
    {
        NewPasswordBox.Password = VisibleNewPasswordBox.Text;
        VisibleNewPasswordBox.Visibility = Visibility.Collapsed;
        NewPasswordBox.Visibility = Visibility.Visible;
    }

    private void ShowConfirmNewPassword_Checked(object sender, RoutedEventArgs e)
    {
        VisibleConfirmNewPasswordBox.Text = ConfirmNewPasswordBox.Password;
        VisibleConfirmNewPasswordBox.Visibility = Visibility.Visible;
        ConfirmNewPasswordBox.Visibility = Visibility.Collapsed;
    }

    private void ShowConfirmNewPassword_Unchecked(object sender, RoutedEventArgs e)
    {
        ConfirmNewPasswordBox.Password = VisibleConfirmNewPasswordBox.Text;
        VisibleConfirmNewPasswordBox.Visibility = Visibility.Collapsed;
        ConfirmNewPasswordBox.Visibility = Visibility.Visible;
    }
    // ------------------------------

    private void SendOtpButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.SendOtpCommand.Execute(null);
    }

    private void ResetPasswordButton_OnClick(object sender, RoutedEventArgs e)
    {
        // Lấy dữ liệu từ ô visible tương ứng
        var newPassword = VisibleNewPasswordBox.Visibility == Visibility.Visible
            ? VisibleNewPasswordBox.Text
            : NewPasswordBox.Password;

        var confirmPassword = VisibleConfirmNewPasswordBox.Visibility == Visibility.Visible
            ? VisibleConfirmNewPasswordBox.Text
            : ConfirmNewPasswordBox.Password;

        var passwords = Tuple.Create(newPassword, confirmPassword);
        _viewModel.ResetPasswordCommand.Execute(passwords);
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
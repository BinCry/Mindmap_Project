using System;
using System.Windows;
using System.Windows.Controls; // Thêm để dùng PasswordBox, TextBox
using MindmapApp.ViewModels;

namespace MindmapApp.Views;

public partial class RegisterWindow : Window
{
    private readonly RegisterViewModel _viewModel;

    public RegisterWindow()
    {
        InitializeComponent();
        _viewModel = new RegisterViewModel(App.UserService);
        DataContext = _viewModel;
        _viewModel.BackRequested += (_, _) => Close();
        _viewModel.RegisteredSuccessfully += OnRegisteredSuccessfully;
    }

    // --- LOGIC ẨN/HIỆN MẬT KHẨU ---
    private void ShowPassword_Checked(object sender, RoutedEventArgs e)
    {
        VisiblePasswordBox.Text = PasswordBox.Password;
        VisiblePasswordBox.Visibility = Visibility.Visible;
        PasswordBox.Visibility = Visibility.Collapsed;
    }

    private void ShowPassword_Unchecked(object sender, RoutedEventArgs e)
    {
        PasswordBox.Password = VisiblePasswordBox.Text;
        VisiblePasswordBox.Visibility = Visibility.Collapsed;
        PasswordBox.Visibility = Visibility.Visible;
    }

    private void ShowConfirmPassword_Checked(object sender, RoutedEventArgs e)
    {
        VisibleConfirmPasswordBox.Text = ConfirmPasswordBox.Password;
        VisibleConfirmPasswordBox.Visibility = Visibility.Visible;
        ConfirmPasswordBox.Visibility = Visibility.Collapsed;
    }

    private void ShowConfirmPassword_Unchecked(object sender, RoutedEventArgs e)
    {
        ConfirmPasswordBox.Password = VisibleConfirmPasswordBox.Text;
        VisibleConfirmPasswordBox.Visibility = Visibility.Collapsed;
        ConfirmPasswordBox.Visibility = Visibility.Visible;
    }
    // ------------------------------

    private void RegisterButton_OnClick(object sender, RoutedEventArgs e)
    {
        // Lấy mật khẩu từ ô visible tương ứng
        var password = VisiblePasswordBox.Visibility == Visibility.Visible
            ? VisiblePasswordBox.Text
            : PasswordBox.Password;

        var confirmPassword = VisibleConfirmPasswordBox.Visibility == Visibility.Visible
            ? VisibleConfirmPasswordBox.Text
            : ConfirmPasswordBox.Password;

        var registerData = Tuple.Create(password, confirmPassword);
        _viewModel.RegisterCommand.Execute(registerData);
    }

    private void BackButton_OnClick(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private void OnRegisteredSuccessfully(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            MessageBox.Show(this, _viewModel.SuccessMessage ?? "Đăng ký thành công", "MindmapApp", MessageBoxButton.OK, MessageBoxImage.Information);
            this.Close();
        });
    }
}
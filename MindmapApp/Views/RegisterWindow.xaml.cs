using System;
using System.Windows;
using MindmapApp.ViewModels;
using MindmapApp.Views; // Đảm bảo đã import namespace này nếu LoginWindow ở đây

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

    private void RegisterButton_OnClick(object sender, RoutedEventArgs e)
    {
        // Lấy dữ liệu mật khẩu từ PasswordBox
        var password = PasswordBox.Password;
        var confirmPassword = ConfirmPasswordBox.Password;

        // RegisterViewModel mong đợi Tuple<string, string> (password, confirm)
        var registerData = Tuple.Create(password, confirmPassword);

        // Thực thi RegisterCommand với dữ liệu này (bất đồng bộ)
        _viewModel.RegisterCommand.Execute(registerData);

        // Không kiểm tra SuccessMessage tại đây vì lệnh chạy bất đồng bộ.
        // Kết quả sẽ được xử lý trong OnRegisteredSuccessfully
    }

    private void BackButton_OnClick(object sender, RoutedEventArgs e)
    {
        // Quay lại cửa sổ Login
        var loginWindow = new LoginWindow();
        loginWindow.Show();
        this.Close(); // Đóng cửa sổ đăng ký
    }

    private void OnRegisteredSuccessfully(object? sender, EventArgs e)
    {
        // Đảm bảo chạy trên UI thread
        Dispatcher.Invoke(() =>
        {
            MessageBox.Show(this, _viewModel.SuccessMessage ?? "Đăng ký thành công", "MindmapApp", MessageBoxButton.OK, MessageBoxImage.Information);

            var loginWindow = new LoginWindow();
            loginWindow.Show();
            Close();
        });
    }
}
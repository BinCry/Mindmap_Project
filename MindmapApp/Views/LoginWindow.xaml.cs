using System.Windows;
using System.Windows.Input;
using MindmapApp.Models;
using MindmapApp.ViewModels;

namespace MindmapApp.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;

    public LoginWindow()
    {
        InitializeComponent();
        _viewModel = new LoginViewModel(App.UserService);
        DataContext = _viewModel;
        _viewModel.LoginSucceeded += OnLoginSucceeded;
        _viewModel.RegisterRequested += (_, _) => ShowRegister();
        _viewModel.ForgotPasswordRequested += (_, _) => ShowForgotPassword();
    }

    private void OnLoginSucceeded(object? sender, UserAccount account)
    {
        var window = new MainWindow(account);
        window.Show();
        Close();
    }

    private void ShowRegister()
    {
        var window = new RegisterWindow();
        window.Owner = this;
        window.ShowDialog();
    }

    private void ShowForgotPassword()
    {
        var window = new ForgotPasswordWindow();
        window.Owner = this;
        window.ShowDialog();
    }

    private void LoginButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.LoginCommand.Execute(PasswordBox.Password);
    }

    private void RegisterButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.OpenRegisterCommand.Execute(null);
    }

    private void ForgotButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.OpenForgotPasswordCommand.Execute(null);
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            LoginButton_OnClick((object)sender, e);
    }
}

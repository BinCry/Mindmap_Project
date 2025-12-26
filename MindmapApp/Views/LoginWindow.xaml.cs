using System.Windows;
using System.Windows.Input;
using MindmapApp.Models;
using MindmapApp.ViewModels;

namespace MindmapApp.Views;

public partial class LoginWindow : Window
{
    #region Fields
    private readonly LoginViewModel _viewModel;
    #endregion

    #region Constructor
    public LoginWindow()
    {
        InitializeComponent();
        _viewModel = new LoginViewModel(App.UserService);
        DataContext = _viewModel;
        _viewModel.LoginSucceeded += OnLoginSucceeded;
        _viewModel.RegisterRequested += (_, _) => ShowRegister();
        _viewModel.ForgotPasswordRequested += (_, _) => ShowForgotPassword();
    }
    #endregion

    #region Private Methods
    private void OnLoginSucceeded(object? sender, UserAccount account)
    {
        var window = new MainWindow(account);
        window.Show();
        Close();
    }

    private void ShowRegister()
    {
        this.Hide();
        var window = new RegisterWindow();
        window.Owner = this;
        window.ShowDialog();
        this.Show();
    }

    private void ShowForgotPassword()
    {
        this.Hide();
        var window = new ForgotPasswordWindow();
        window.Owner = this;
        window.ShowDialog();
        this.Show();
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
    // ------------------------------
    #endregion

    #region UI Event Handlers
    private void LoginButton_OnClick(object sender, RoutedEventArgs e)
    {
        // Lấy mật khẩu từ ô đang hiển thị
        var password = VisiblePasswordBox.Visibility == Visibility.Visible 
            ? VisiblePasswordBox.Text 
            : PasswordBox.Password;

        _viewModel.LoginCommand.Execute(password);
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
    #endregion

    #region Window Control Handlers 
    private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            this.DragMove();
        }
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        this.WindowState = WindowState.Minimized;
    }

    private void Button_Click_1(object sender, RoutedEventArgs e)
    {
        this.Close();
    }
    #endregion
}
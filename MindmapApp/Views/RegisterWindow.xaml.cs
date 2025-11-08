
using System;
using System.Windows;
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
        _viewModel.RegisteredSuccessfully += (_, _) => { };
    }

    private void RegisterButton_OnClick(object sender, RoutedEventArgs e)
    {
        var passwords = Tuple.Create(PasswordBox.Password, ConfirmPasswordBox.Password);
        _viewModel.RegisterCommand.Execute(passwords);
        if (!string.IsNullOrWhiteSpace(_viewModel.SuccessMessage))
        {
            MessageBox.Show(this, _viewModel.SuccessMessage, "MindmapApp", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
    }

    private void BackButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.BackCommand.Execute(null);
    }
}

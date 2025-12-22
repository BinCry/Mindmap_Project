using MindmapApp.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace MindmapApp.Views
{
    public partial class ProfileDialog : UserControl
    {
        public ProfileDialog()
        {
            InitializeComponent();
        }

        // --- 1. LOGIC ẨN/HIỆN MẬT KHẨU ---
        private void ShowCurrentPass_Checked(object sender, RoutedEventArgs e)
        {
            VisiblePbCurrentPass.Text = PbCurrentPass.Password;
            VisiblePbCurrentPass.Visibility = Visibility.Visible;
            PbCurrentPass.Visibility = Visibility.Collapsed;
        }
        private void ShowCurrentPass_Unchecked(object sender, RoutedEventArgs e)
        {
            PbCurrentPass.Password = VisiblePbCurrentPass.Text;
            VisiblePbCurrentPass.Visibility = Visibility.Collapsed;
            PbCurrentPass.Visibility = Visibility.Visible;
        }

        private void ShowNewPass_Checked(object sender, RoutedEventArgs e)
        {
            VisiblePbNewPass.Text = PbNewPass.Password;
            VisiblePbNewPass.Visibility = Visibility.Visible;
            PbNewPass.Visibility = Visibility.Collapsed;
        }
        private void ShowNewPass_Unchecked(object sender, RoutedEventArgs e)
        {
            PbNewPass.Password = VisiblePbNewPass.Text;
            VisiblePbNewPass.Visibility = Visibility.Collapsed;
            PbNewPass.Visibility = Visibility.Visible;
        }

        private void ShowConfirmPass_Checked(object sender, RoutedEventArgs e)
        {
            VisiblePbConfirmPass.Text = PbConfirmPass.Password;
            VisiblePbConfirmPass.Visibility = Visibility.Visible;
            PbConfirmPass.Visibility = Visibility.Collapsed;
        }
        private void ShowConfirmPass_Unchecked(object sender, RoutedEventArgs e)
        {
            PbConfirmPass.Password = VisiblePbConfirmPass.Text;
            VisiblePbConfirmPass.Visibility = Visibility.Collapsed;
            PbConfirmPass.Visibility = Visibility.Visible;
        }

        // --- 2. LOGIC LƯU PROFILE ---
        private async void BtnSaveProfile_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                string current = VisiblePbCurrentPass.Visibility == Visibility.Visible ? VisiblePbCurrentPass.Text : PbCurrentPass.Password;
                string newP = VisiblePbNewPass.Visibility == Visibility.Visible ? VisiblePbNewPass.Text : PbNewPass.Password;
                string confirm = VisiblePbConfirmPass.Visibility == Visibility.Visible ? VisiblePbConfirmPass.Text : PbConfirmPass.Password;

                await vm.SaveProfileAsync(current, newP, confirm);

                if (!vm.IsProfileDialogOpen)
                {
                    PbCurrentPass.Password = ""; PbNewPass.Password = ""; PbConfirmPass.Password = "";
                    VisiblePbCurrentPass.Text = ""; VisiblePbNewPass.Text = ""; VisiblePbConfirmPass.Text = "";
                }
            }
        }
    }
}
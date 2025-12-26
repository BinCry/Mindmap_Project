using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MindmapApp.Views
{
    public partial class PdfExportOptionsWindow : Window
    {
        public string? Password { get; private set; }
        public bool IncludeWatermark { get; private set; }

        public PdfExportOptionsWindow()
        {
            InitializeComponent();
            UpdateStrengthVisuals(string.Empty);
        }

        // --- LOGIC ẨN/HIỆN MẬT KHẨU ---
        private void ShowPassword_Checked(object sender, RoutedEventArgs e)
        {
            VisiblePassword.Text = TxtPassword.Password;
            VisiblePassword.Visibility = Visibility.Visible;
            TxtPassword.Visibility = Visibility.Collapsed;
            UpdateStrengthVisuals(VisiblePassword.Text);
        }

        private void ShowPassword_Unchecked(object sender, RoutedEventArgs e)
        {
            TxtPassword.Password = VisiblePassword.Text;
            VisiblePassword.Visibility = Visibility.Collapsed;
            TxtPassword.Visibility = Visibility.Visible;
            UpdateStrengthVisuals(TxtPassword.Password);
        }

        private void VisiblePassword_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateStrengthVisuals(VisiblePassword.Text);
        }

        private void TxtPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            UpdateStrengthVisuals(TxtPassword.Password);
        }

        private void UpdateStrengthVisuals(string password)
        {
            var defaultBrush = TryFindResource("BorderBrush") as Brush ?? Brushes.LightGray;

            BarWeak.Background = defaultBrush;
            BarMedium.Background = defaultBrush;
            BarStrong.Background = defaultBrush;

            if (string.IsNullOrEmpty(password))
            {
                LblStrength.Text = "Chưa nhập mật khẩu";
                if (TryFindResource("TextBrush") is Brush textBrush)
                    LblStrength.Foreground = textBrush;
                LblStrength.Opacity = 0.5;
                return;
            }

            LblStrength.Opacity = 1.0;
            // Logic đánh giá độ mạnh (giữ nguyên như cũ)
            int score = 0;
            if (password.Length >= 4) score++;
            if (password.Length >= 8) score++;
            if (Regex.IsMatch(password, @"[A-Za-z]") && Regex.IsMatch(password, @"[0-9]")) score++;
            if (Regex.IsMatch(password, @"[!@#$%^&*(),.?""{}|<>]")) score++;

            if (password.Length < 6)
            {
                var dangerBrush = TryFindResource("DangerBrush") as Brush ?? Brushes.Red;
                BarWeak.Background = dangerBrush;
                LblStrength.Text = "Yếu";
                LblStrength.Foreground = dangerBrush;
            }
            else if (score < 3)
            {
                var accentBrush = TryFindResource("AccentBrush") as Brush ?? Brushes.Orange;
                BarWeak.Background = accentBrush;
                BarMedium.Background = accentBrush;
                LblStrength.Text = "Trung bình";
                LblStrength.Foreground = accentBrush;
            }
            else
            {
                var strongBrush = new SolidColorBrush(Colors.MediumSeaGreen);
                BarWeak.Background = strongBrush;
                BarMedium.Background = strongBrush;
                BarStrong.Background = strongBrush;
                LblStrength.Text = "Mạnh";
                LblStrength.Foreground = strongBrush;
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            // --- FIX: ĐỒNG BỘ MẬT KHẨU CUỐI CÙNG ---
            // Đảm bảo lấy đúng giá trị từ ô đang hiển thị
            if (VisiblePassword.Visibility == Visibility.Visible)
                Password = VisiblePassword.Text;
            else
                Password = TxtPassword.Password;

            // Nếu mật khẩu chỉ toàn khoảng trắng hoặc rỗng -> coi như không đặt
            if (string.IsNullOrWhiteSpace(Password)) Password = null;

            IncludeWatermark = ChkWatermark.IsChecked == true;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
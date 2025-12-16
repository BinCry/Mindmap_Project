using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MindmapApp.Views;

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
        // Cập nhật đánh giá độ mạnh dựa trên text hiện tại
        UpdateStrengthVisuals(VisiblePassword.Text);
    }

    private void ShowPassword_Unchecked(object sender, RoutedEventArgs e)
    {
        TxtPassword.Password = VisiblePassword.Text;
        VisiblePassword.Visibility = Visibility.Collapsed;
        TxtPassword.Visibility = Visibility.Visible;
        // Cập nhật đánh giá độ mạnh
        UpdateStrengthVisuals(TxtPassword.Password);
    }

    private void VisiblePassword_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Khi nhập liệu ở ô hiện, cũng cần update thanh đánh giá
        UpdateStrengthVisuals(VisiblePassword.Text);
    }
    // ------------------------------

    private void TxtPassword_PasswordChanged(object sender, RoutedEventArgs e)
    {
        UpdateStrengthVisuals(TxtPassword.Password);
    }

    private void UpdateStrengthVisuals(string password)
    {
        // 1. Lấy màu nền mặc định từ BorderBrush trong Theme
        var defaultBrush = TryFindResource("BorderBrush") as Brush ?? Brushes.LightGray;

        // Reset màu các thanh
        BarWeak.Background = defaultBrush;
        BarMedium.Background = defaultBrush;
        BarStrong.Background = defaultBrush;

        if (string.IsNullOrEmpty(password))
        {
            LblStrength.Text = "Chưa nhập mật khẩu";
            // Dùng TextBrush từ theme
            if (TryFindResource("TextBrush") is Brush textBrush)
                LblStrength.Foreground = textBrush;

            LblStrength.Opacity = 0.5;
            return;
        }

        LblStrength.Opacity = 1.0;

        int score = 0;
        if (password.Length >= 4) score++;
        if (password.Length >= 8) score++;
        if (Regex.IsMatch(password, @"[A-Za-z]") && Regex.IsMatch(password, @"[0-9]")) score++;
        if (Regex.IsMatch(password, @"[!@#$%^&*(),.?""{}|<>]")) score++;

        // Logic đánh giá
        if (password.Length < 6)
        {
            // Mức Yếu: Dùng DangerBrush (Màu đỏ trong Theme.xaml)
            var dangerBrush = TryFindResource("DangerBrush") as Brush ?? Brushes.Red;
            BarWeak.Background = dangerBrush;
            LblStrength.Text = "Yếu";
            LblStrength.Foreground = dangerBrush;
        }
        else if (score < 3)
        {
            // Mức Trung bình: Dùng AccentBrush (Màu cam trong Theme.xaml)
            var accentBrush = TryFindResource("AccentBrush") as Brush ?? Brushes.Orange;
            BarWeak.Background = accentBrush;
            BarMedium.Background = accentBrush;
            LblStrength.Text = "Trung bình";
            LblStrength.Foreground = accentBrush;
        }
        else
        {
            // Mức Mạnh: Màu xanh lá
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
        // Lấy mật khẩu từ ô đang hiển thị
        Password = VisiblePassword.Visibility == Visibility.Visible
            ? VisiblePassword.Text
            : TxtPassword.Password;

        IncludeWatermark = ChkWatermark.IsChecked == true;
        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
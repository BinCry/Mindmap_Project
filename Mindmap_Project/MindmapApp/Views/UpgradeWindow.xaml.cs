using System;
using System.Windows;
using System.Windows.Media.Imaging;
using MindmapApp.Services;

namespace MindmapApp.Views
{
    public partial class UpgradeWindow : Window
    {
        private readonly PaymentService _paymentService = new();
        private readonly UserService _userService;
        private readonly Guid _userId;
        private readonly string _transContent;

        public bool IsSuccess { get; private set; } = false;

        public UpgradeWindow(Guid userId, UserService userService)
        {
            InitializeComponent();
            _userId = userId;
            _userService = userService;

            // 1. Tạo nội dung chuyển khoản duy nhất (VD: UP X8A1B2)
            // Lấy 6 ký tự đầu của GUID để mã ngắn gọn dễ nhập nếu cần
            _transContent = $"UP {userId.ToString().Substring(0, 6)}".ToUpper();

            // 2. Tạo QR Code
            LoadQrCode();
        }

        private void LoadQrCode()
        {
            try
            {
                string url = _paymentService.GenerateQrUrl(_transContent);
                QrImage.Source = new BitmapImage(new Uri(url));
            }
            catch (Exception)
            {
                TxtStatus.Text = "Không thể tải mã QR. Vui lòng kiểm tra kết nối mạng.";
            }
        }

        private async void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            // Hiển thị màn hình chờ
            LoadingGrid.Visibility = Visibility.Visible;
            BtnConfirm.IsEnabled = false;
            TxtStatus.Text = "";

            try
            {
                // Gọi Service kiểm tra tiền
                bool paid = await _paymentService.CheckPaymentStatusAsync(_transContent);

                if (paid)
                {
                    // Update DB lên Pro
                    await _userService.UpgradeToProAsync(_userId);

                    IsSuccess = true;
                    MessageBox.Show("Xác nhận thành công!\nCảm ơn bạn đã ủng hộ Mindmap Pro.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                }
                else
                {
                    TxtStatus.Text = "Hệ thống chưa nhận được tiền.\nVui lòng chờ thêm vài giây rồi thử lại nhé!";
                }
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Lỗi: {ex.Message}";
            }
            finally
            {
                // Ẩn màn hình chờ
                LoadingGrid.Visibility = Visibility.Collapsed;
                BtnConfirm.IsEnabled = true;
            }
        }
    }
}
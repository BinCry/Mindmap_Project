using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json; // Sử dụng thư viện có sẵn của .NET để đọc JSON
using System.Threading.Tasks;

namespace MindmapApp.Services;

public class PaymentService
{
    // =========================================================================
    // ⚠️ CẤU HÌNH THÔNG TIN THẬT CỦA BẠN Ở ĐÂY
    // =========================================================================

    // 1. Thông tin ngân hàng để tạo mã QR
    private const string BANK_ID = "MB";            // Mã ngân hàng (VD: MB, VCB, ACB, TPB...)
    private const string ACCOUNT_NO = "0985512831"; // Số tài khoản NHẬN TIỀN
    private const string ACCOUNT_NAME = "PHAM MINH QUAN"; // Tên chủ tài khoản (viết hoa, không dấu)
    private const int AMOUNT = 10000;               // Số tiền cần thanh toán (10k)

    // 2. API Token lấy từ trang quản trị https://my.sepay.vn
    // Nếu chưa có, hãy đăng ký và copy token dán vào bên dưới
    private const string SEPAY_API_TOKEN = "R7BFIJQFKSQZ3EWBG0MGK4TFEC9AUZNOPHVZYPDG4WYJLE92UQMDHMNOHAMXCK6U\t";

    // =========================================================================

    // Hàm tạo link QR Code (VietQR)
    public string GenerateQrUrl(string contentInfo)
    {
        // Tạo QR chuẩn VietQR
        // addInfo: Chính là nội dung chuyển khoản (VD: UPGRADE 12345)
        return $"https://img.vietqr.io/image/{BANK_ID}-{ACCOUNT_NO}-compact.png?amount={AMOUNT}&addInfo={contentInfo}&accountName={Uri.EscapeDataString(ACCOUNT_NAME)}";
    }

    // Hàm kiểm tra thanh toán thực tế qua API SePay
    public async Task<bool> CheckPaymentStatusAsync(string contentInfo)
    {
        // contentInfo chính là "Nội dung chuyển khoản" duy nhất mà app đã tạo ra
        // Ví dụ: "UP X8D9S1"

        try
        {
            using var client = new HttpClient();

            // Thêm API Token vào Header để xác thực
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", SEPAY_API_TOKEN);

            // Gọi API lấy danh sách giao dịch mới nhất từ SePay
            var response = await client.GetAsync("https://my.sepay.vn/userapi/transactions/list");

            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();

                // Phân tích JSON trả về
                using var doc = JsonDocument.Parse(jsonString);
                var root = doc.RootElement;

                // Kiểm tra xem JSON có chứa danh sách "transactions" không
                if (root.TryGetProperty("transactions", out var transactions))
                {
                    // Duyệt qua từng giao dịch
                    foreach (var trans in transactions.EnumerateArray())
                    {
                        // Lấy nội dung CK và số tiền thực nhận
                        string transactionContent = trans.GetProperty("transaction_content").GetString() ?? "";

                        // Lấy số tiền (Xử lý trường hợp API trả về chuỗi hoặc số)
                        decimal amountIn = 0;
                        if (trans.TryGetProperty("amount_in", out var amtProp))
                        {
                            if (amtProp.ValueKind == JsonValueKind.String)
                                decimal.TryParse(amtProp.GetString(), out amountIn);
                            else if (amtProp.ValueKind == JsonValueKind.Number)
                                amountIn = amtProp.GetDecimal();
                        }

                        // --- LOGIC KIỂM TRA ---
                        // 1. Nội dung CK phải chứa mã code của user (VD: chứ "UP X8D9S1")
                        // 2. Số tiền phải lớn hơn hoặc bằng giá gói (>= 10000)
                        bool isContentMatch = transactionContent.Contains(contentInfo, StringComparison.OrdinalIgnoreCase);
                        bool isAmountEnough = amountIn >= AMOUNT;

                        if (isContentMatch && isAmountEnough)
                        {
                            return true; // Đã tìm thấy thanh toán hợp lệ!
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Ghi log lỗi nếu cần thiết (Debug)
            System.Diagnostics.Debug.WriteLine($"Lỗi check thanh toán: {ex.Message}");
        }

        // Nếu chạy hết vòng lặp mà không tìm thấy, hoặc lỗi mạng -> Trả về chưa thanh toán
        return false;
    }
}
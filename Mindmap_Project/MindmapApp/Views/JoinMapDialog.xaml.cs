using System.Windows;
using System.Windows.Input;

namespace MindmapApp.Views
{
    public partial class JoinMapDialog : Window
    {
        public JoinMapDialog()
        {
            InitializeComponent();
            CodeTextBox.Focus();
        }

        public string ShareCode => CodeTextBox.Text.Trim();

        private void OnJoinClick(object sender, RoutedEventArgs e)
        {
            TrySubmit();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void CodeTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TrySubmit();
            }
        }

        private void TrySubmit()
        {
            var trimmed = ShareCode;
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                ErrorText.Text = "Vui lòng nhập mã Share.";
                ErrorText.Visibility = Visibility.Visible;
                CodeTextBox.Focus();
                return;
            }

            ErrorText.Visibility = Visibility.Collapsed;
            DialogResult = true;
        }
    }
}

using MindmapApp.Models;
using MindmapApp.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace MindmapApp.Views
{
    public partial class RecentMapsWindow : Window
    {
        public RecentMapsWindow(UserAccount account)
        {
            InitializeComponent();
            var viewModel = new RecentMapsViewModel(App.MindmapStorageService, account);
            DataContext = viewModel;
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MindmapApp.Models;
using MindmapApp.Services;
using MindmapApp.Views;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Linq;

namespace MindmapApp.ViewModels
{
    public partial class RecentMapsViewModel : ObservableObject
    {
        private readonly MindmapStorageService _storageService;
        private readonly UserAccount _currentUser;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        private readonly MindmapThumbnailService _thumbnailService = new();

        public ObservableCollection<RecentMapItemViewModel> RecentMaps { get; } = new();

        public RecentMapsViewModel(MindmapStorageService storageService, UserAccount currentUser)
        {
            _storageService = storageService;
            _currentUser = currentUser;
            LoadMapsCommand.Execute(null);
        }

        [RelayCommand]
        private async Task LoadMaps()
        {
            IsLoading = true;
            try
            {
                RecentMaps.Clear();
                RecentMaps.Add(new RecentMapItemViewModel(new MindmapDocument { Id = Guid.Empty, Title = "New Map" }, null, true));

                var maps = await _storageService.GetAllMapsHeaderAsync(_currentUser.Id);
                foreach (var map in maps)
                {
                    var thumbnail = _thumbnailService.CreateThumbnail(map, 220, 120);
                    RecentMaps.Add(new RecentMapItemViewModel(map, thumbnail, false));
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Lỗi tải lịch sử: " + ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task DeleteMap(RecentMapItemViewModel map)
        {
            if (map == null || map.IsPlaceholder || map.Id == Guid.Empty) return; // Không cho xóa nút "New Map"

            // Hỏi xác nhận trước khi xóa
            var result = MessageBox.Show(
                $"Bạn có chắc chắn muốn xóa vĩnh viễn mindmap:\n'{map.Title}' không?",
                "Xác nhận xóa",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // 1. Gọi Service để xóa trong Database
                    await _storageService.DeleteMapAsync(map.Document.Id);

                    RecentMaps.Remove(map);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi khi xóa: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void OpenNewMap()
        {
            var mainWindow = new MainWindow(_currentUser, null); // Pass null for new map
            mainWindow.Show();
            CloseWindow();
        }

        [RelayCommand]
        private void OpenMap(RecentMapItemViewModel map)
        {
            if (map == null) return;
            if (map.IsPlaceholder || map.Id == Guid.Empty)
            {
                OpenNewMap();
                return;
            }
            var mainWindow = new MainWindow(_currentUser, map.Document.Id); // Pass ID to load
            mainWindow.Show();
            CloseWindow();
        }

        [RelayCommand]
        private void Logout()
        {
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            CloseWindow();
        }

        private void CloseWindow()
        {
            foreach (Window window in Application.Current.Windows)
            {
                if (window.DataContext == this)
                {
                    window.Close();
                    break;
                }
            }
        }
    }
}

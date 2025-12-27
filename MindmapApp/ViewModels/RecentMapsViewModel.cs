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

        public ObservableCollection<MindmapDocument> RecentMaps { get; } = new();

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
                // Add "New Map" placeholder
                RecentMaps.Add(new MindmapDocument { Id = Guid.Empty, Title = "New Map" });

                var maps = await _storageService.GetAllMapsHeaderAsync(_currentUser.Id);
                foreach (var map in maps)
                {
                    RecentMaps.Add(map);
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
        private void OpenNewMap()
        {
            var mainWindow = new MainWindow(_currentUser, null); // Pass null for new map
            mainWindow.Show();
            CloseWindow();
        }

        [RelayCommand]
        private void OpenMap(MindmapDocument map)
        {
            if (map == null) return;
            if (map.Id == Guid.Empty)
            {
                OpenNewMap();
                return;
            }
            var mainWindow = new MainWindow(_currentUser, map.Id); // Pass ID to load
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

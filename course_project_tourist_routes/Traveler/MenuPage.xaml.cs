using course_project_tourist_routes.Common;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace course_project_tourist_routes.Traveler
{
    public partial class MenuPage : Page
    {
        private readonly int _userId;

        public MenuPage(int userId)
        {
            InitializeComponent();
            _userId = userId;
        }

        public void ToggleSettingsMenuFramesVisibility()
        {
            for (DependencyObject parent = this; parent != null; parent = VisualTreeHelper.GetParent(parent))
                if (parent is TravelerPage travelerPage)
                {
                    travelerPage.SettingsFrame.Visibility = Visibility.Collapsed;
                    travelerPage.MenuFrame.Visibility = Visibility.Collapsed;
                    break;
                }
        }

        private void TravelEvensButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleSettingsMenuFramesVisibility();

            if (Window.GetWindow(this) is TravelerWindow travelerWindow)
            {
                travelerWindow.NavigationService.Navigate(new TravelEventsPage(_userId));
            }
        }

        private void FindRouteButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleSettingsMenuFramesVisibility();

            if (Window.GetWindow(this) is TravelerWindow travelerWindow)
            {
                travelerWindow.NavigationService.Navigate(new RoutesPage(_userId));
            }
        }

        private void FavoritesButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleSettingsMenuFramesVisibility();

            if (Window.GetWindow(this) is TravelerWindow travelerWindow)
            {
                travelerWindow.NavigationService.Navigate(new FavouritesPage(_userId));
            }
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(
                "Вы действительно хотите выйти из аккаунта?",
                "Подтверждение выхода",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _ = CloudStorage.ClearRoutePhotosDirectoryAsync();
                _ = CloudStorage.ClearProfilePhotosDirectoryAsync();
                AutorizWindow mainWindow = new AutorizWindow();
                mainWindow.Show();
                Window.GetWindow(this).Close();
            }
        }
    }
}

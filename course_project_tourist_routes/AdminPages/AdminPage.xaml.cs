using course_project_tourist_routes.CommonPages;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace course_project_tourist_routes.AdminPages
{
    public partial class AdminPage : Page
    {
        private readonly int _userId;
        private string _userName;
        private string _userStatus;

        public AdminPage(int userId, string userName, string userStatus, AdminWindow adminWindow, Users user)
        {
            Resources.Add("User", user);
            InitializeComponent();
            StartClock();

            InitializeAsync(user);

            _userId = userId;
            _userName = userName;
            _userStatus = userStatus;

            AdminLogin.Text = userName;
            AdminStatus.Text = userStatus;
        }

        private async void InitializeAsync(Users user)
        {
            try
            {
                await CloudStorage.DownloadCurrentUserPhotoAsync(user.ProfilePhoto);

                var bitmapImage = await CloudStorage.GetBitmapImageAsync(CloudStorage.CurrentUserPhotoPath, true);

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SharedResources.ProfileImageBrush.ImageSource = bitmapImage;
                    Resources["imagebrush"] = SharedResources.ProfileImageBrush;
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при загрузке фото профиля: {ex.Message}");

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SharedResources.ProfileImageBrush.ImageSource =
                        new BitmapImage(new Uri("pack://application:,,,/Resources/profile_photo.jpg"));
                    Resources["imagebrush"] = SharedResources.ProfileImageBrush;
                });
            }
        }

        public void UpdateUserInfo(string newLogin, string newStatus)
        {
            AdminLogin.Text = newLogin;
            AdminStatus.Text = newStatus;

            _userName = newLogin;
            _userStatus = newStatus;
        }

        private void StartClock()
        {
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            DateTimeTextBlock.Text = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Вы действительно хотите выйти из аккаунта?",
                "Подтверждение выхода",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                CloudStorage.ClearRoutePhotosDirectoryAsync();
                CloudStorage.ClearProfilePhotosDirectoryAsync();
                new AutorizWindow().Show();
                Window.GetWindow(this)?.Close();
            }
        }

        private void ProfilePhoto_Click(Object sender, RoutedEventArgs e)
        {
            SupRect.Visibility = Visibility.Visible;
            FullScreenAvatar.Visibility = Visibility.Visible;
            BackButton.Visibility = Visibility.Visible;
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            if (SettingsFrame.Visibility == Visibility.Collapsed)
            {
                SettingsFrame.Visibility = Visibility.Visible;

                SettingsPage settingsPage = new SettingsPage(_userId, _userName, _userStatus, this, (Users)FindResource("User"));
                settingsPage.ProfilePhoto.Click += ProfilePhoto_Click;
                SettingsFrame.Navigate(settingsPage);
            }
            else
            {
                SettingsFrame.Visibility = Visibility.Collapsed;
            }
        }

        public void ToggleSettingsFrameVisibility()
        {
            if (SettingsFrame.Visibility == Visibility.Visible)
            {
                SettingsFrame.Visibility = Visibility.Collapsed;
            }
        }

        private void UsersButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleSettingsFrameVisibility();
            NavigationService.Navigate(new UsersPage(_userId));
        }

        private void AddRouteButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleSettingsFrameVisibility();
            NavigationService.Navigate(new AddRoutePage(_userId));
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            SupRect.Visibility = Visibility.Collapsed;
            FullScreenAvatar.Visibility = Visibility.Collapsed;
            BackButton.Visibility = Visibility.Collapsed;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ToggleSettingsFrameVisibility();
            NavigationService.Navigate(new RoutesPage(_userId));
        }

        private void RoutePointsButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ReportsButton_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}

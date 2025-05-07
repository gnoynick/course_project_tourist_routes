using course_project_tourist_routes.Common;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using static course_project_tourist_routes.Common.PointsPage;

namespace course_project_tourist_routes.Admin
{
    public partial class AdminPage : Page
    {
        private readonly int _userId;
        private string _userName;
        private string _userStatus;
        private bool _isAnimating;

        private void SetButtonsEnabled(bool isEnabled)
        {
            UsersButton.IsEnabled = isEnabled;
            Settings.IsEnabled = isEnabled;
            RoutePointsButton.IsEnabled = isEnabled;
            RoutesButton.IsEnabled = isEnabled;
            AddRouteButton.IsEnabled = isEnabled;
            ReportsButton.IsEnabled = isEnabled;
            ExitButton.IsEnabled = isEnabled;
        }

        public AdminPage(int userId, string userName, string userStatus, AdminWindow adminWindow, Users user)
        {
            Resources.Add("User", user);
            InitializeComponent();
            StartClock();

            ProfilePhotoProgress.Visibility = Visibility.Visible;
            ProfilePhoto.Visibility = Visibility.Collapsed;

            InitializeAsync(user);

            _userId = userId;
            _userName = userName;
            _userStatus = userStatus;

            AdminLogin.Text = userName;
            AdminStatus.Text = userStatus;
        }

        private async void InitializeAsync(Users user)
        {
            SetButtonsEnabled(false);
            try
            {
                await CloudStorage.DownloadCurrentUserPhotoAsync(user.ProfilePhoto);
                var bitmapImage = await CloudStorage.GetBitmapImageAsync(CloudStorage.CurrentUserPhotoPath, true);

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SharedResources.ProfileImageBrush.ImageSource = bitmapImage;
                    Resources["imagebrush"] = SharedResources.ProfileImageBrush;
                    ProfilePhotoProgress.Visibility = Visibility.Collapsed;
                    ProfilePhoto.Visibility = Visibility.Visible;
                    SetButtonsEnabled(true);
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
                    ProfilePhotoProgress.Visibility = Visibility.Collapsed;
                    ProfilePhoto.Visibility = Visibility.Visible;
                    SetButtonsEnabled(true);
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
            DispatcherTimer timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            timer.Tick += Timer_Tick;
            timer.Start();
            Timer_Tick(null, null);
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            DateTimeTextBlock.Text = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (SettingsFrame.Visibility == Visibility.Visible)
                {
                    _ = AnimateFrameOut(SettingsFrame, "SettingsFrameSlideOut");
                    e.Handled = true;
                }
                else if (FullScreenAvatar.Visibility == Visibility.Visible)
                {
                    BackButton_Click(null, null);
                    e.Handled = true;
                }
            }
            base.OnKeyDown(e);
        }

        private async Task AnimateFrameIn(FrameworkElement frame, string animationKey)
        {
            if (_isAnimating) return;

            _isAnimating = true;
            try
            {
                var storyboard = (Storyboard)FindResource(animationKey);
                storyboard.Begin(frame);
                await Task.Delay(300);
            }
            finally
            {
                _isAnimating = false;
            }
        }

        private async Task AnimateFrameOut(FrameworkElement frame, string animationKey)
        {
            if (_isAnimating) return;

            _isAnimating = true;
            try
            {
                var storyboard = (Storyboard)FindResource(animationKey);
                storyboard.Begin(frame);
                await Task.Delay(300);
                frame.Visibility = Visibility.Collapsed;
            }
            finally
            {
                _isAnimating = false;
            }
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Вы действительно хотите выйти из аккаунта?",
                "Подтверждение выхода",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _ = CloudStorage.ClearRoutePhotosDirectoryAsync();
                _ = CloudStorage.ClearProfilePhotosDirectoryAsync();
                new AutorizWindow().Show();
                Window.GetWindow(this)?.Close();
            }
        }

        private void ProfilePhoto_Click(object sender, RoutedEventArgs e)
        {
            SupRect.Visibility = Visibility.Visible;
            FullScreenAvatar.Visibility = Visibility.Visible;
            BackButton.Visibility = Visibility.Visible;
        }

        private async void Settings_Click(object sender, RoutedEventArgs e)
        {
            if (SettingsFrame.Visibility == Visibility.Collapsed)
            {
                SettingsFrame.Visibility = Visibility.Visible;
                SettingsPage settingsPage = new SettingsPage(_userId, _userName, _userStatus, this, (Users)FindResource("User"));
                settingsPage.ProfilePhoto.Click += ProfilePhoto_Click;
                SettingsFrame.Navigate(settingsPage);
                await AnimateFrameIn(SettingsFrame, "SettingsFrameSlideIn");
            }
            else
            {
                await AnimateFrameOut(SettingsFrame, "SettingsFrameSlideOut");
            }
        }

        public async void ToggleSettingsFrameVisibility()
        {
            if (SettingsFrame.Visibility == Visibility.Visible)
            {
                await AnimateFrameOut(SettingsFrame, "SettingsFrameSlideOut");
            }
        }

        private void UsersButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleSettingsFrameVisibility();
            NavigationService.Navigate(new UsersPage(_userId));
        }

        private void TravelEvensButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleSettingsFrameVisibility();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            SupRect.Visibility = Visibility.Collapsed;
            FullScreenAvatar.Visibility = Visibility.Collapsed;
            BackButton.Visibility = Visibility.Collapsed;
        }

        private void RoutesButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleSettingsFrameVisibility();
            NavigationService.Navigate(new RoutesPage(_userId));
        }

        private void RoutePointsButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleSettingsFrameVisibility();
            NavigationService.Navigate(new PointsPage(_userId, PointsPageMode.Admin));
        }

        private void ReportsButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleSettingsFrameVisibility();
            NavigationService.Navigate(new ReportsPage());
        }
    }
}
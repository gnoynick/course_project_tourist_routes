using course_project_tourist_routes.TravelerPages;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace course_project_tourist_routes.AdminPages
{
    public partial class TravelerPage : Page
    {
        private int _userId;
        private string _userName;
        private string _userStatus;

        public TravelerPage(int userId, string userName, string userStatus, TravelerWindow travelerWindow, Users user)
        {
            Resources.Add("User", user);
            InitializeComponent();

            InitializeAsync(user);

            _userId = userId;
            _userName = userName;
            _userStatus = userStatus;

            UserLogin.Text = _userName;
            UserStatus.Text = _userStatus;

            LoadRoutes();
            StartClock();
        }

        private void TravelerPage_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Visibility == Visibility.Visible)
            {
                using (var context = new TouristRoutesEntities())
                {
                    context.ChangeTracker.Entries().ToList().ForEach(x => x.Reload());
                }

                LoadRoutes();
            }
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

        public void LoadRoutes()
        {
            try
            {
                using (var db = new TouristRoutesEntities())
                {
                    var routes = db.Routes
                        .Where(r => r.IdUser == _userId && r.Categories.NameCategory == "Пользовательские")
                        .OrderByDescending(r => r.IdRoute)
                        .Take(3)
                        .Select(r => new
                        {
                            r.TitleRoute,
                            DescriptionRoute = r.DescriptionRoute.Length > 30 ? r.DescriptionRoute.Substring(0, 30) + "..." : r.DescriptionRoute
                        })
                        .ToList();

                    ListViewRoutes.ItemsSource = routes;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при загрузке маршрутов: " + ex.Message);
            }
        }


        public void UpdateUserInfo(string newLogin, string newStatus)
        {
            UserLogin.Text = newLogin;
            UserStatus.Text = newStatus;

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

        private void Menu_Click(object sender, RoutedEventArgs e)
        {
            if (MenuFrame.Visibility == Visibility.Collapsed)
            {
                MenuFrame.Visibility = Visibility.Visible;

                MenuPage menuPage = new MenuPage(_userId);
                MenuFrame.Navigate(menuPage);
            }
            else
            {
                MenuFrame.Visibility = Visibility.Collapsed;
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

        public void ToggleSettingsMenuFramesVisibility()
        {
            if (SettingsFrame.Visibility == Visibility.Visible)
            {
                SettingsFrame.Visibility = Visibility.Collapsed;
            }

            if (MenuFrame.Visibility == Visibility.Visible)
            {
                MenuFrame.Visibility = Visibility.Collapsed;
            }
        }

        private void MyRoutesButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleSettingsMenuFramesVisibility();

            if (!(NavigationService?.Content is MyRoutesPage))
            {
                NavigationService?.Navigate(new MyRoutesPage(_userId));
            }
        }

        private void ListViewRoutes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListViewRoutes.SelectedItem != null)
            {
                dynamic selectedItem = ListViewRoutes.SelectedItem;
                string titleRoute = selectedItem.TitleRoute;

                using (var db = new TouristRoutesEntities())
                {
                    var route = db.Routes.FirstOrDefault(r => r.TitleRoute == titleRoute);
                    if (route != null)
                    {
                        NavigationService?.Navigate(new OpenRoutePage(route.IdRoute, _userId));
                    }
                }

                ListViewRoutes.SelectedItem = null;
            }
        }
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            SupRect.Visibility = Visibility.Collapsed;
            FullScreenAvatar.Visibility = Visibility.Collapsed;
            BackButton.Visibility = Visibility.Collapsed;
        }
    }
}

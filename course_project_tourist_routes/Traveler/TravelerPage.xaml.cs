using course_project_tourist_routes.Common;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace course_project_tourist_routes.Traveler
{
    public partial class TravelerPage : Page
    {
        private int _userId;
        private string _userName;
        private string _userStatus;
        private bool _isAnimating;

        private Storyboard _menuFrameSlideIn;
        private Storyboard _menuFrameSlideOut;
        private Storyboard _settingsFrameSlideIn;
        private Storyboard _settingsFrameSlideOut;

        private void SetButtonsEnabled(bool isEnabled)
        {
            MenuButton.IsEnabled = isEnabled;
            Settings.IsEnabled = isEnabled;
            MyRoutesButton.IsEnabled = isEnabled;
        }

        public TravelerPage(int userId, string userName, string userStatus, TravelerWindow travelerWindow, Users user)
        {
            Resources.Add("User", user);
            InitializeComponent();

            InitializeAnimations();

            ProfilePhotoProgress.Visibility = Visibility.Visible;
            ProfilePhoto.Visibility = Visibility.Collapsed;

            InitializeAsync(user);

            _userId = userId;
            _userName = userName;
            _userStatus = userStatus;

            UserLogin.Text = _userName;
            UserStatus.Text = _userStatus;

            LoadRoutes();
            StartClock();
        }

        private void InitializeAnimations()
        {
            _menuFrameSlideIn = new Storyboard();
            var menuSlideInAnimation = new DoubleAnimation
            {
                From = -400,
                To = 0,
                Duration = TimeSpan.FromSeconds(0.3),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTargetProperty(menuSlideInAnimation, new PropertyPath("RenderTransform.(TranslateTransform.X)"));
            _menuFrameSlideIn.Children.Add(menuSlideInAnimation);

            var menuFadeInAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(0.2)
            };
            Storyboard.SetTargetProperty(menuFadeInAnimation, new PropertyPath("Opacity"));
            _menuFrameSlideIn.Children.Add(menuFadeInAnimation);

            _menuFrameSlideOut = new Storyboard();
            var menuSlideOutAnimation = new DoubleAnimation
            {
                From = 0,
                To = -400,
                Duration = TimeSpan.FromSeconds(0.3),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTargetProperty(menuSlideOutAnimation, new PropertyPath("RenderTransform.(TranslateTransform.X)"));
            _menuFrameSlideOut.Children.Add(menuSlideOutAnimation);

            var menuFadeOutAnimation = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromSeconds(0.2)
            };
            Storyboard.SetTargetProperty(menuFadeOutAnimation, new PropertyPath("Opacity"));
            _menuFrameSlideOut.Children.Add(menuFadeOutAnimation);

            _settingsFrameSlideIn = new Storyboard();
            var settingsSlideInAnimation = new DoubleAnimation
            {
                From = 400,
                To = 0,
                Duration = TimeSpan.FromSeconds(0.3),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTargetProperty(settingsSlideInAnimation, new PropertyPath("RenderTransform.(TranslateTransform.X)"));
            _settingsFrameSlideIn.Children.Add(settingsSlideInAnimation);

            var settingsFadeInAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(0.2)
            };
            Storyboard.SetTargetProperty(settingsFadeInAnimation, new PropertyPath("Opacity"));
            _settingsFrameSlideIn.Children.Add(settingsFadeInAnimation);

            _settingsFrameSlideOut = new Storyboard();
            var settingsSlideOutAnimation = new DoubleAnimation
            {
                From = 0,
                To = 400,
                Duration = TimeSpan.FromSeconds(0.3),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTargetProperty(settingsSlideOutAnimation, new PropertyPath("RenderTransform.(TranslateTransform.X)"));
            _settingsFrameSlideOut.Children.Add(settingsSlideOutAnimation);

            var settingsFadeOutAnimation = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromSeconds(0.2)
            };
            Storyboard.SetTargetProperty(settingsFadeOutAnimation, new PropertyPath("Opacity"));
            _settingsFrameSlideOut.Children.Add(settingsFadeOutAnimation);
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
                            DescriptionRoute = r.DescriptionRoute.Length > 30 ?
                                r.DescriptionRoute.Substring(0, 30) + "..." :
                                r.DescriptionRoute
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
                if (MenuFrame.Visibility == Visibility.Visible)
                {
                    _ = AnimateFrameOut(MenuFrame, _menuFrameSlideOut);
                    e.Handled = true;
                }
                else if (SettingsFrame.Visibility == Visibility.Visible)
                {
                    _ = AnimateFrameOut(SettingsFrame, _settingsFrameSlideOut);
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


        private async void Menu_Click(object sender, RoutedEventArgs e)
        {
            if (MenuFrame.Visibility == Visibility.Collapsed)
            {
                if (SettingsFrame.Visibility == Visibility.Visible)
                {
                    await AnimateFrameOut(SettingsFrame, _settingsFrameSlideOut);
                }

                MenuFrame.Visibility = Visibility.Visible;
                MenuPage menuPage = new MenuPage(_userId);
                MenuFrame.Navigate(menuPage);
                await AnimateFrameIn(MenuFrame, _menuFrameSlideIn);
            }
            else
            {
                await AnimateFrameOut(MenuFrame, _menuFrameSlideOut);
            }
        }

        private async void Settings_Click(object sender, RoutedEventArgs e)
        {
            if (SettingsFrame.Visibility == Visibility.Collapsed)
            {
                if (MenuFrame.Visibility == Visibility.Visible)
                {
                    await AnimateFrameOut(MenuFrame, _menuFrameSlideOut);
                }

                SettingsFrame.Visibility = Visibility.Visible;
                SettingsPage settingsPage = new SettingsPage(_userId, _userName, _userStatus, this, (Users)FindResource("User"));
                settingsPage.ProfilePhoto.Click += ProfilePhoto_Click;
                SettingsFrame.Navigate(settingsPage);
                await AnimateFrameIn(SettingsFrame, _settingsFrameSlideIn);
            }
            else
            {
                await AnimateFrameOut(SettingsFrame, _settingsFrameSlideOut);
            }
        }

        private async Task AnimateFrameIn(FrameworkElement frame, Storyboard animation)
        {
            if (_isAnimating) return;

            _isAnimating = true;
            try
            {
                animation.Begin(frame);
                await Task.Delay(300);
            }
            finally
            {
                _isAnimating = false;
            }
        }

        private async Task AnimateFrameOut(FrameworkElement frame, Storyboard animation)
        {
            if (_isAnimating) return;

            _isAnimating = true;
            try
            {
                animation.Begin(frame);
                await Task.Delay(300);
                frame.Visibility = Visibility.Collapsed;
            }
            finally
            {
                _isAnimating = false;
            }
        }

        private void ProfilePhoto_Click(object sender, RoutedEventArgs e)
        {
            SupRect.Visibility = Visibility.Visible;
            FullScreenAvatar.Visibility = Visibility.Visible;
            BackButton.Visibility = Visibility.Visible;
        }

        public async void ToggleSettingsMenuFramesVisibility()
        {
            if (SettingsFrame.Visibility == Visibility.Visible)
            {
                await AnimateFrameOut(SettingsFrame, _settingsFrameSlideOut);
            }

            if (MenuFrame.Visibility == Visibility.Visible)
            {
                await AnimateFrameOut(MenuFrame, _menuFrameSlideOut);
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
            if (!MenuButton.IsEnabled || _isAnimating) return;

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
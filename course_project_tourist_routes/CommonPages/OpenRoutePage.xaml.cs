using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data.Entity;
using System.Windows.Media;
using System.Diagnostics;

namespace course_project_tourist_routes.AdminPages
{
    public partial class OpenRoutePage : Page
    {
        private int _routeId;
        private int _userId;
        private bool _isFavorite;
        private int _routeAuthorId;
        private int _currentUserRoleId;
        private bool _isCurrentUserAdmin;
        private List<string> _tempPhotoPaths = new List<string>();

        public OpenRoutePage(int routeId, int userId)
        {
            InitializeComponent();
            _routeId = routeId;
            _userId = userId;

            LoadRouteData();
        }

        private async void LoadRouteData()
        {
            try
            {
                using (var db = new TouristRoutesEntities())
                {
                    var currentUser = await db.Users
                        .Include(u => u.Roles)
                        .FirstOrDefaultAsync(u => u.IdUser == _userId);

                    if (currentUser != null)
                    {
                        _currentUserRoleId = (int)currentUser.IdRole;
                        _isCurrentUserAdmin = currentUser.Roles.NameRole == "Admin";
                    }

                    CheckFavoriteStatus();

                    var route = await db.Routes
                        .Include(r => r.Categories)
                        .Include(r => r.Users)
                        .Include(r => r.RoutePoints.Select(p => p.PointTypes))
                        .Include(r => r.Photos)
                        .FirstOrDefaultAsync(r => r.IdRoute == _routeId);

                    if (route == null) return;

                    _routeAuthorId = (int)route.IdUser;
                    RouteTitleText.Text = route.TitleRoute;
                    CategoryText.Text = route.Categories?.NameCategory ?? "Без категории";

                    if (route.Categories?.NameCategory == "Пользовательские" && route.Users != null)
                    {
                        AuthorBorder.Visibility = Visibility.Visible;
                        AuthorText.Text = $"{route.Users.UserName}";
                    }

                    DescriptionText.Text = route.DescriptionRoute ?? "Описание отсутствует";
                    LengthText.Text = $"Длина: {route.LengthPoint?.ToString("0.00") ?? "0"} км";
                    StepsText.Text = $"Шаги: {route.StepsCount?.ToString() ?? "0"}";
                    PointsListView.ItemsSource = route.RoutePoints?.ToList() ?? new List<RoutePoints>();

                    await LoadRoutePhotos(route.Photos?.ToList() ?? new List<Photos>());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных маршрута: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadRoutePhotos(List<Photos> photos)
        {
            try
            {
                Photo1Progress.Visibility = Visibility.Visible;
                Photo2Progress.Visibility = Visibility.Visible;
                Photo3Progress.Visibility = Visibility.Visible;

                if (photos == null || !photos.Any())
                {
                    HideAllLoaders();
                    return;
                }

                CloudStorage.ClearRoutePhotosDirectory();
                string dir = Path.Combine(Directory.GetParent(Environment.CurrentDirectory).Parent.FullName, "temp", "route_photos");

                for (int i = 0; i < Math.Min(photos.Count, 3); i++)
                {
                    var photo = photos[i];
                    if (string.IsNullOrEmpty(photo.Photo)) continue;

                    try
                    {
                        string path = Path.Combine(dir, $"route_{_routeId}_photo_{i}.jpg");
                        _tempPhotoPaths.Add(path);

                        await Task.WhenAll(
                            Task.Run(() => CloudStorage.DownloadRoutePhoto(photo.Photo, path))
                        );

                        await Dispatcher.InvokeAsync(() => ShowPhoto(i, path));
                    }
                    catch (Exception ex)
                    {
                        await Dispatcher.InvokeAsync(() =>
                            MessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка",
                                MessageBoxButton.OK, MessageBoxImage.Error));
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                HideAllLoaders();
            }
        }

        private void ShowPhoto(int index, string path)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();

            var imageBrush = new ImageBrush(bitmap);
            Resources[$"photo{index + 1}brush"] = imageBrush;

            switch (index)
            {
                case 0:
                    Photo1Progress.Visibility = Visibility.Collapsed;
                    Photo1Button.Visibility = Visibility.Visible;
                    break;
                case 1:
                    Photo2Progress.Visibility = Visibility.Collapsed;
                    Photo2Button.Visibility = Visibility.Visible;
                    break;
                case 2:
                    Photo3Progress.Visibility = Visibility.Collapsed;
                    Photo3Button.Visibility = Visibility.Visible;
                    break;
            }
        }

        private void HideAllLoaders()
        {
            Photo1Progress.Visibility = Visibility.Collapsed;
            Photo2Progress.Visibility = Visibility.Collapsed;
            Photo3Progress.Visibility = Visibility.Collapsed;
        }

        private void CheckFavoriteStatus()
        {
            if (_userId == 0) return;

            try
            {
                using (var db = new TouristRoutesEntities())
                {
                    bool isTraveler = !_isCurrentUserAdmin && _userId != _routeAuthorId;

                    if (isTraveler)
                    {
                        FavoriteButton.Visibility = Visibility.Visible;
                        _isFavorite = db.Favorites.Any(f => f.IdUser == _userId && f.IdRoute == _routeId);
                        FavoriteIcon.Kind = _isFavorite ?
                            MaterialDesignThemes.Wpf.PackIconKind.Star :
                            MaterialDesignThemes.Wpf.PackIconKind.StarOutline;
                    }
                    else
                    {
                        FavoriteButton.Visibility = Visibility.Collapsed;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при проверке статуса избранного: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            CloudStorage.ClearRoutePhotosDirectory();
            NavigationService?.GoBack();
        }

        private void FavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_isFavorite)
                {
                    RemoveFromFavorites();
                }
                else
                {
                    AddToFavorites();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении избранного: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddToFavorites()
        {
            try
            {
                using (var db = new TouristRoutesEntities())
                {
                    var favorite = new Favorites
                    {
                        IdUser = _userId,
                        IdRoute = _routeId
                    };
                    db.Favorites.Add(favorite);
                    db.SaveChanges();
                    FavoriteIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.Star;
                    _isFavorite = true;
                    MessageBox.Show("Маршрут добавлен в избранное", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении в избранное: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemoveFromFavorites()
        {
            try
            {
                using (var db = new TouristRoutesEntities())
                {
                    var favorite = db.Favorites.FirstOrDefault(f => f.IdUser == _userId && f.IdRoute == _routeId);
                    if (favorite != null)
                    {
                        db.Favorites.Remove(favorite);
                        db.SaveChanges();
                        FavoriteIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.StarOutline;
                        _isFavorite = false;
                        MessageBox.Show("Маршрут удален из избранного", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении из избранного: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PhotoButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            if (button.Name == "Photo1Button")
            {
                FullScreenPhoto.Fill = (ImageBrush)Resources["photo1brush"];
            }
            else if (button.Name == "Photo2Button")
            {
                FullScreenPhoto.Fill = (ImageBrush)Resources["photo2brush"];
            }
            else if (button.Name == "Photo3Button")
            {
                FullScreenPhoto.Fill = (ImageBrush)Resources["photo3brush"];
            }

            SupRect.Visibility = Visibility.Visible;
            FullScreenPhoto.Visibility = Visibility.Visible;
            PhotoBackButton.Visibility = Visibility.Visible;
        }

        private void PhotoBackButton_Click(object sender, RoutedEventArgs e)
        {
            SupRect.Visibility = Visibility.Collapsed;
            FullScreenPhoto.Visibility = Visibility.Collapsed;
            PhotoBackButton.Visibility = Visibility.Collapsed;
        }
    }
}
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data.Entity;
using System.Windows.Media;

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
        private int _currentPhotoIndex = 0;
        private int _totalPhotos = 0;

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
                        _isCurrentUserAdmin = currentUser.Roles.NameRole == "Администратор";
                    }

                    var route = await db.Routes.FirstOrDefaultAsync(r => r.IdRoute == _routeId);
                    if (route != null)
                    {
                        bool isAdminOrAuthor = _isCurrentUserAdmin || _userId == route.IdUser;

                        if (!isAdminOrAuthor)
                        {
                            route.ViewsCount = (route.ViewsCount ?? 0) + 1;
                            await db.SaveChangesAsync();
                        }
                        ViewsText.Text = route.ViewsCount?.ToString() ?? "0";
                    }

                    CheckFavoriteStatus();

                    route = await db.Routes
                        .Include(r => r.Categories)
                        .Include(r => r.Users)
                        .Include(r => r.RoutePoints.Select(p => p.PointTypes))
                        .Include(r => r.Photos)
                        .FirstOrDefaultAsync(r => r.IdRoute == _routeId);

                    if (route == null) return;

                    _routeAuthorId = (int)route.IdUser;
                    RouteTitleText.Text = route.TitleRoute;
                    CategoryText.Text = route.Categories?.NameCategory ?? "Без категории";

                    bool isAuthor = _userId == _routeAuthorId;

                    bool showFavoriteButton = !_isCurrentUserAdmin && !isAuthor;
                    FavoriteButton.Visibility = showFavoriteButton ? Visibility.Visible : Visibility.Collapsed;

                    if (showFavoriteButton)
                    {
                        CheckFavoriteStatus();
                    }

                    if (route.Categories?.NameCategory == "Пользовательские" && route.Users != null)
                    {
                        AuthorBorder.Visibility = Visibility.Visible;
                        AuthorText.Text = isAuthor ? "Вы" : $"{route.Users.UserName}";
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
                MessageBox.Show($"Ошибка при загрузке данных маршрута: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadRoutePhotos(List<Photos> photos)
        {
            try
            {
                HideAllLoadersAndButtons();

                if (photos == null || !photos.Any())
                {
                    return;
                }

                for (int i = 0; i < Math.Min(photos.Count, 3); i++)
                {
                    switch (i)
                    {
                        case 0:
                            Photo1Progress.Visibility = Visibility.Visible;
                            break;
                        case 1:
                            Photo2Progress.Visibility = Visibility.Visible;
                            break;
                        case 2:
                            Photo3Progress.Visibility = Visibility.Visible;
                            break;
                    }
                }

                await CloudStorage.ClearRoutePhotosDirectoryAsync();
                string dir = CloudStorage.RoutePhotosDirectoryPath;

                for (int i = 0; i < Math.Min(photos.Count, 3); i++)
                {
                    var photo = photos[i];
                    if (string.IsNullOrEmpty(photo.Photo)) continue;

                    try
                    {
                        string path = Path.Combine(dir, $"route_{_routeId}_photo_{i}.jpg");
                        _tempPhotoPaths.Add(path);

                        await Task.WhenAll(
                            Task.Run(() => CloudStorage.DownloadRoutePhotoAsync(photo.Photo, path))
                        );

                        await Dispatcher.InvokeAsync(() => ShowPhoto(i, path));
                    }
                    catch (Exception ex)
                    {
                        await Dispatcher.InvokeAsync(() =>
                            MessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error));
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private void HideAllLoadersAndButtons()
        {
            Photo1Progress.Visibility = Visibility.Collapsed;
            Photo2Progress.Visibility = Visibility.Collapsed;
            Photo3Progress.Visibility = Visibility.Collapsed;
            Photo1Button.Visibility = Visibility.Collapsed;
            Photo2Button.Visibility = Visibility.Collapsed;
            Photo3Button.Visibility = Visibility.Collapsed;
        }

        private void CheckFavoriteStatus()
        {
            try
            {
                using (var db = new TouristRoutesEntities())
                {
                    _isFavorite = db.Favorites.Any(f => f.IdUser == _userId && f.IdRoute == _routeId);
                    FavoriteIcon.Kind = _isFavorite ?
                        MaterialDesignThemes.Wpf.PackIconKind.Star :
                        MaterialDesignThemes.Wpf.PackIconKind.StarOutline;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при проверке статуса избранного: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public event Action<int> RouteViewed;

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
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
                MessageBox.Show($"Ошибка при обновлении избранного: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        IdRoute = _routeId,
                        DateAddedFavorite = DateTime.Now
                    };
                    db.Favorites.Add(favorite);
                    db.SaveChanges();
                    FavoriteIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.Star;
                    _isFavorite = true;
                    MessageBox.Show("Маршрут добавлен в избранное", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении в избранное: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        MessageBox.Show("Маршрут удален из избранного", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении из избранного: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PhotoButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            _totalPhotos = 0;
            if (Photo1Button.Visibility == Visibility.Visible) _totalPhotos++;
            if (Photo2Button.Visibility == Visibility.Visible) _totalPhotos++;
            if (Photo3Button.Visibility == Visibility.Visible) _totalPhotos++;

            if (button.Name == "Photo1Button")
            {
                _currentPhotoIndex = 0;
                FullScreenPhoto.Fill = (ImageBrush)Resources["photo1brush"];
            }
            else if (button.Name == "Photo2Button")
            {
                _currentPhotoIndex = 1;
                FullScreenPhoto.Fill = (ImageBrush)Resources["photo2brush"];
            }
            else if (button.Name == "Photo3Button")
            {
                _currentPhotoIndex = 2;
                FullScreenPhoto.Fill = (ImageBrush)Resources["photo3brush"];
            }

            UpdateNavigationButtons();
            SupRect.Visibility = Visibility.Visible;
            FullScreenPhotoGrid.Visibility = Visibility.Visible;
            BackButton.IsCancel = false;
        }

        private void UpdateNavigationButtons()
        {
            ClosePhotoButton.Visibility = Visibility.Visible;

            PrevPhotoButton.Visibility = (_totalPhotos > 1 && _currentPhotoIndex > 0) ? Visibility.Visible : Visibility.Collapsed;
            NextPhotoButton.Visibility = (_totalPhotos > 1 && _currentPhotoIndex < _totalPhotos - 1) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ClosePhotoButton_Click(object sender, RoutedEventArgs e)
        {
            SupRect.Visibility = Visibility.Collapsed;
            FullScreenPhotoGrid.Visibility = Visibility.Collapsed;
            BackButton.IsCancel = true;
        }

        private void PrevPhotoButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPhotoIndex > 0)
            {
                _currentPhotoIndex--;
                ShowPhotoAtCurrentIndex();
            }
        }

        private void NextPhotoButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPhotoIndex < _totalPhotos - 1)
            {
                _currentPhotoIndex++;
                ShowPhotoAtCurrentIndex();
            }
        }

        private void ShowPhotoAtCurrentIndex()
        {
            switch (_currentPhotoIndex)
            {
                case 0:
                    FullScreenPhoto.Fill = (ImageBrush)Resources["photo1brush"];
                    break;
                case 1:
                    FullScreenPhoto.Fill = (ImageBrush)Resources["photo2brush"];
                    break;
                case 2:
                    FullScreenPhoto.Fill = (ImageBrush)Resources["photo3brush"];
                    break;
            }
            UpdateNavigationButtons();
        }
    }
}
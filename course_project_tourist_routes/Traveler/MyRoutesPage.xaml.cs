using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Diagnostics;
using System.Data.Entity;
using course_project_tourist_routes.Common;
using System.Collections.Generic;
using System.Windows.Media;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Media.Imaging;

namespace course_project_tourist_routes.Traveler
{
    public partial class MyRoutesPage : Page
    {
        private readonly int _userId;
        private readonly Dictionary<int, ImageBrush> _routePhotosCache = new Dictionary<int, ImageBrush>();

        public MyRoutesPage(int userId)
        {
            InitializeComponent();
            _userId = userId;

            LoadRoutes();
        }

        private async void LoadRoutes(string searchText = null)
        {
            try
            {
                LoadingGrid.Visibility = Visibility.Visible;
                RoutesListViewBorder.Visibility = Visibility.Collapsed;
                using (var context = new TouristRoutesEntities())
                {
                    IQueryable<Routes> routesQuery = context.Routes
                        .Include(r => r.Categories)
                        .Include(r => r.Photos)
                        .Where(r => r.IdUser == _userId && r.Categories.NameCategory == "Пользовательские");

                    if (!string.IsNullOrWhiteSpace(searchText))
                    {
                        routesQuery = routesQuery.Where(r => r.TitleRoute.Contains(searchText));
                    }

                    var routesList = await routesQuery
                        .OrderByDescending(r => r.IdRoute)
                        .ToListAsync();

                    var newRoutes = routesList.Where(r => !_routePhotosCache.ContainsKey(r.IdRoute)).ToList();
                    await Task.WhenAll(newRoutes.Select(async route =>
                    {
                        var photoBrush = await LoadRoutePhotoAsync(route.IdRoute);
                        _routePhotosCache[route.IdRoute] = photoBrush;
                    }));

                    var routes = routesList.Select(r => new
                    {
                        r.IdRoute,
                        r.TitleRoute,
                        DescriptionRoute = r.DescriptionRoute.Length > 30 ?
                                r.DescriptionRoute.Substring(0, 30) + "..." :
                                r.DescriptionRoute,
                        r.DateAddedRoute,
                        RoutePhotoBrush = _routePhotosCache.TryGetValue(r.IdRoute, out var brush) ?
                            brush : CreateDefaultRouteImageBrush()
                    });

                    RoutesListView.ItemsSource = routes;
                    LoadingGrid.Visibility = Visibility.Collapsed;
                    RoutesListViewBorder.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                LoadingGrid.Visibility = Visibility.Collapsed;
                RoutesListViewBorder.Visibility = Visibility.Visible;
                MessageBox.Show($"Ошибка при загрузке маршрутов: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task<ImageBrush> LoadRoutePhotoAsync(int routeId)
        {
            string defaultPhotoPath = "pack://application:,,,/Resources/route_placeholder.jpg";
            var defaultBrush = CreateImageBrush(defaultPhotoPath);

            try
            {
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                string appFolder = Path.Combine(appDataPath, "TouristRoutes");
                string routePhotosDir = Path.Combine(appFolder, "route_photos");
                Directory.CreateDirectory(routePhotosDir);

                string fileName = $"Route_{routeId}_Photo_1.jpg";
                string photoPath = Path.Combine(routePhotosDir, fileName);

                if (File.Exists(photoPath))
                {
                    var brush = CreateImageBrush(photoPath);
                    if (brush.ImageSource != null)
                        return brush;
                }

                using (var context = new TouristRoutesEntities())
                {
                    var photo = await context.Photos
                        .Where(p => p.IdRoute == routeId)
                        .OrderBy(p => p.IdPhoto)
                        .FirstOrDefaultAsync();

                    if (photo != null && !string.IsNullOrEmpty(photo.Photo))
                    {
                        await CloudStorage.DownloadRoutePhotoAsync(photo.Photo, fileName);

                        if (File.Exists(photoPath))
                        {
                            var brush = CreateImageBrush(photoPath);
                            if (brush.ImageSource != null)
                                return brush;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки фото маршрута: {ex.Message}");
            }

            return defaultBrush;
        }

        private ImageBrush CreateImageBrush(string imagePath)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(imagePath, UriKind.RelativeOrAbsolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bitmap.EndInit();

                if (bitmap.CanFreeze)
                    bitmap.Freeze();

                return new ImageBrush
                {
                    ImageSource = bitmap
                };
            }
            catch
            {
                return CreateDefaultRouteImageBrush();
            }
        }

        private ImageBrush CreateDefaultRouteImageBrush()
        {
            return new ImageBrush
            {
                ImageSource = new BitmapImage(
                    new Uri("pack://application:,,,/Resources/route_placeholder.jpg")),
                Stretch = Stretch.UniformToFill
            };
        }

        private void RoutePhotoButton_Click(object sender, RoutedEventArgs e)
        {
            BackButton.IsCancel = false;
            if (sender is Button button && button.Background is ImageBrush brush)
            {
                FullScreenPhoto.Fill = brush;
                SupRect.Visibility = Visibility.Visible;
                FullScreenPhoto.Visibility = Visibility.Visible;
                PhotoBackButton.Visibility = Visibility.Visible;
            }
        }

        private void PhotoBackButton_Click(object sender, RoutedEventArgs e)
        {
            BackButton.IsCancel = true;
            SupRect.Visibility = Visibility.Collapsed;
            FullScreenPhoto.Visibility = Visibility.Collapsed;
            PhotoBackButton.Visibility = Visibility.Collapsed;
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            LoadRoutes(SearchTextBox.Text);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.GoBack();
        }

        private void RoutesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RoutesListView.SelectedItem != null)
            {
                dynamic selectedItem = RoutesListView.SelectedItem;
                int routeId = selectedItem.IdRoute;
                NavigationService?.Navigate(new OpenRoutePage(routeId, _userId));
                RoutesListView.SelectedItem = null;
            }
        }

        private void EditRouteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is int routeId))
            {
                MessageBox.Show("Ошибка открытия маршрута для редактирования");
                return;
            }
            NavigationService?.Navigate(new EditRoutePage(_userId, routeId));
        }

        private void DeleteRouteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is int routeId))
            {
                MessageBox.Show("Ошибка удаления маршрута");
                return;
            }

            var result = MessageBox.Show("Вы уверены, что хотите удалить этот маршрут?", "Подтверждение удаления",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using (var context = new TouristRoutesEntities())
                    {
                        var routeToDelete = context.Routes
                            .Include(r => r.Photos)
                            .Include(r => r.RoutePoints)
                            .Include(r => r.Favorites)
                            .Include(r => r.TravelEvents)
                            .FirstOrDefault(r => r.IdRoute == routeId);

                        if (routeToDelete != null)
                        {
                            foreach (var photo in routeToDelete.Photos.ToList())
                            {
                                if (!string.IsNullOrEmpty(photo.Photo))
                                {
                                    try
                                    {
                                        _ = CloudStorage.DeleteFileAsync(photo.Photo);
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine($"Ошибка при удалении фото из Google Drive: {ex.Message}");
                                    }
                                }
                                context.Photos.Remove(photo);
                            }

                            foreach (var point in routeToDelete.RoutePoints.ToList())
                            {
                                point.Routes.Remove(routeToDelete);
                            }

                            foreach (var favorite in routeToDelete.Favorites.ToList())
                            {
                                context.Favorites.Remove(favorite);
                            }

                            foreach (var tour in routeToDelete.TravelEvents.ToList())
                            {
                                context.TravelEvents.Remove(tour);
                            }

                            context.Routes.Remove(routeToDelete);
                            context.SaveChanges();

                            LoadRoutes(SearchTextBox.Text);
                            MessageBox.Show("Маршрут успешно удален", "Успех",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении маршрута: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
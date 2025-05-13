using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Collections.Generic;
using System.Diagnostics;
using System.Data.Entity;
using System.Threading.Tasks;
using System.Windows.Media;
using System.IO;
using System.Windows.Media.Imaging;
using course_project_tourist_routes.Traveler;

namespace course_project_tourist_routes.Common
{
    public partial class RoutesPage : Page
    {
        private readonly int _userId;
        private List<Categories> _categories = new List<Categories>();
        private List<string> _countries = new List<string>();
        private List<string> _cities = new List<string>();
        private readonly Dictionary<int, ImageBrush> _routePhotosCache = new Dictionary<int, ImageBrush>();
        public bool IsSelectionMode { get; set; }

        public RoutesPage(int userId, bool isSelectionMode = false)
        {
            InitializeComponent();
            _userId = userId;
            IsSelectionMode = isSelectionMode;

            if (IsSelectionMode)
            {
                AddRouteButton.Visibility = Visibility.Collapsed;
            }

            _ = LoadCategoriesAsync();
            _ = LoadCountriesAndCitiesAsync();
            _ = LoadRoutesAsync();
        }

        public event Action<dynamic> RouteSelected;

        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is int routeId))
            {
                return;
            }

            var selectedRoute = RoutesListView.Items
                .Cast<dynamic>()
                .FirstOrDefault(r => r.IdRoute == routeId);

            if (selectedRoute != null)
            {
                RouteSelected?.Invoke(selectedRoute);
                NavigationService.GoBack();
            }
        }

        private void RoutesPage_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Visibility == Visibility.Visible)
            {
                using (var context = new TouristRoutesEntities())
                {
                    context.ChangeTracker.Entries().ToList().ForEach(x => x.Reload());
                }

                _ = LoadRoutesAsync();
            }
        }

        private async Task LoadCountriesAndCitiesAsync()
        {
            try
            {
                using (var context = new TouristRoutesEntities())
                {
                    _countries = await context.RoutePoints
                        .Where(p => !string.IsNullOrEmpty(p.Country))
                        .Select(p => p.Country)
                        .Distinct()
                        .OrderBy(c => c)
                        .ToListAsync();

                    _cities = await context.RoutePoints
                        .Where(p => !string.IsNullOrEmpty(p.City))
                        .Select(p => p.City)
                        .Distinct()
                        .OrderBy(c => c)
                        .ToListAsync();

                    CountryComboBox.ItemsSource = new List<string> { "Все страны" }.Concat(_countries);
                    CityComboBox.ItemsSource = new List<string> { "Все города" }.Concat(_cities);

                    CountryComboBox.SelectedIndex = 0;
                    CityComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке стран и городов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateCitiesList(string selectedCountry)
        {
            try
            {
                if (selectedCountry == "Все страны" || string.IsNullOrEmpty(selectedCountry))
                {
                    CityComboBox.ItemsSource = new List<string> { "Все города" }.Concat(_cities);
                }
                else
                {
                    using (var context = new TouristRoutesEntities())
                    {
                        var filteredCities = context.RoutePoints
                            .Where(p => p.Country == selectedCountry && !string.IsNullOrEmpty(p.City))
                            .Select(p => p.City)
                            .Distinct()
                            .OrderBy(c => c)
                            .ToList();

                        CityComboBox.ItemsSource = new List<string> { "Все города" }.Concat(filteredCities);
                    }
                }
                CityComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении списка городов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private int GetUserRoleId()
        {
            try
            {
                using (var context = new TouristRoutesEntities())
                {
                    return (int)context.Users.Where(u => u.IdUser == _userId).Select(u => u.IdRole).FirstOrDefault();
                }
            }
            catch
            {
                return 0;
            }
        }

        private async Task LoadCategoriesAsync()
        {
            try
            {
                using (var context = new TouristRoutesEntities())
                {
                    _categories = await context.Categories.OrderBy(c => c.IdCategory).ToListAsync();

                    var allItem = new Categories { IdCategory = 0, NameCategory = "Все" };
                    _categories.Insert(0, allItem);

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        CategoryComboBox.ItemsSource = _categories;
                        CategoryComboBox.SelectedValuePath = "IdCategory";
                        CategoryComboBox.SelectedIndex = 0;
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке категорий: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadRoutesAsync(int? categoryId = null, string country = null, string city = null, string searchText = null)
        {
            LoadingGrid.Visibility = Visibility.Visible;
            RoutesListViewBorder.Visibility = Visibility.Collapsed;

            try
            {
                using (var context = new TouristRoutesEntities())
                {
                    int currentUserRoleId = GetUserRoleId();

                    IQueryable<Routes> routesQuery = context.Routes
                        .Include(r => r.Categories)
                        .Include(r => r.Users)
                        .Include(r => r.Users.Roles)
                        .Include(r => r.RoutePoints);

                    if (currentUserRoleId == 2 && !IsSelectionMode)
                    {
                        routesQuery = routesQuery.Where(r => r.IdUser != _userId);
                    }

                    if (categoryId.HasValue && categoryId.Value > 0)
                    {
                        routesQuery = routesQuery.Where(r => r.IdCategory == categoryId.Value);
                    }

                    if (!string.IsNullOrEmpty(country) && country != "Все страны")
                    {
                        routesQuery = routesQuery.Where(r => r.RoutePoints.Any(p => p.Country == country));
                    }

                    if (!string.IsNullOrEmpty(city) && city != "Все города")
                    {
                        routesQuery = routesQuery.Where(r => r.RoutePoints.Any(p => p.City == city));
                    }

                    if (!string.IsNullOrEmpty(searchText))
                    {
                        routesQuery = routesQuery.Where(r => r.TitleRoute.Contains(searchText));
                    }

                    var routesList = await routesQuery
                        .OrderByDescending(r => r.IdRoute)
                        .ToListAsync();

                    var loadPhotoTasks = routesList.Select(async route =>
                    {
                        var photoBrush = await LoadRoutePhotoAsync(route.IdRoute);
                        _routePhotosCache[route.IdRoute] = photoBrush;
                    }).ToList();

                    await Task.WhenAll(loadPhotoTasks);

                    var routes = routesList
                        .Select(r => new
                        {
                            r.IdRoute,
                            r.TitleRoute,
                            DescriptionRoute = r.DescriptionRoute.Length > 30 ?
                                r.DescriptionRoute.Substring(0, 30) + "..." :
                                r.DescriptionRoute,
                            CategoryName = r.Categories.NameCategory,
                            AuthorInfo = r.Categories.NameCategory == "Пользовательские" ? $"Автор: {r.Users.UserName}" : "",
                            Countries = string.Join(", ", r.RoutePoints.Select(p => p.Country).Distinct()),
                            Cities = string.Join(", ", r.RoutePoints.Select(p => p.City).Distinct()),
                            ViewsCount = $"Просмотров: {r.ViewsCount ?? 0}",
                            RoutePhotoBrush = _routePhotosCache.TryGetValue(r.IdRoute, out var brush) ?
                                brush : CreateDefaultRouteImageBrush(),
                            r.IdCategory,
                            r.IdUser,
                            AuthorRole = r.Users.Roles.IdRole,
                            CanEdit = currentUserRoleId == 1 && r.Categories.NameCategory != "Пользовательские",
                            CanDelete = currentUserRoleId == 1
                        }).ToList();

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        RoutesListView.ItemsSource = routes;
                        LoadingGrid.Visibility = Visibility.Collapsed;
                        RoutesListViewBorder.Visibility = Visibility.Visible;
                    });
                }
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    LoadingGrid.Visibility = Visibility.Collapsed;
                    RoutesListViewBorder.Visibility = Visibility.Visible;
                });
                MessageBox.Show($"Ошибка при загрузке маршрутов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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

                string fileId = await GetRoutePhotoFileId(routeId);
                if (!string.IsNullOrEmpty(fileId))
                {
                    await CloudStorage.DownloadRoutePhotoAsync(fileId, fileName);

                    if (File.Exists(photoPath))
                    {
                        var brush = CreateImageBrush(photoPath);
                        if (brush.ImageSource != null)
                            return brush;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки фото маршрута: {ex.Message}");
            }

            return defaultBrush;
        }

        private async Task<string> GetRoutePhotoFileId(int routeId)
        {
            try
            {
                using (var context = new TouristRoutesEntities())
                {
                    var photo = await context.Photos
                        .Where(p => p.IdRoute == routeId)
                        .OrderBy(p => p.IdPhoto)
                        .FirstOrDefaultAsync();

                    return photo?.Photo;
                }
            }
            catch
            {
                return null;
            }
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
            if (sender is Button button && button.Background is ImageBrush brush)
            {
                BackButton.IsCancel = false;
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

        private void ApplyFilters()
        {
            var selectedCategory = CategoryComboBox.SelectedItem as Categories;
            var selectedCountry = CountryComboBox.SelectedItem as string;
            var selectedCity = CityComboBox.SelectedItem as string;
            var searchText = SearchTextBox.Text;

            _ = LoadRoutesAsync(
            selectedCategory?.IdCategory == 0 ? null : (selectedCategory?.IdCategory),
            selectedCountry,
            selectedCity,
            searchText
        );
        }

        private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void CountryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CountryComboBox.SelectedItem is string selectedCountry)
            {
                UpdateCitiesList(selectedCountry);
                ApplyFilters();
            }
        }

        private void CityComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.GoBack();
        }

        private void ResetFiltersButton_Click(object sender, RoutedEventArgs e) => ResetFilters();

        private void ResetFilters()
        {
            SearchTextBox.Text = "";
            CityComboBox.SelectedIndex = 0;
            CountryComboBox.SelectedIndex = 0;
            CategoryComboBox.SelectedIndex = 0;
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

            var result = MessageBox.Show("Вы уверены, что хотите удалить этот маршрут?", "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);

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
                            ApplyFilters();
                            MessageBox.Show("Маршрут удален", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении маршрута: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void AddRouteButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AddRoutePage(_userId));
        }
    }
}
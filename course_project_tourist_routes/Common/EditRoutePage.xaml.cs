using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System.Threading.Tasks;
using System.Windows.Media;
using System.IO;

namespace course_project_tourist_routes.Common
{
    public partial class EditRoutePage : Page
    {
        private readonly int _userId;
        private readonly int _routeId;
        private List<Categories> _categories = new List<Categories>();
        private List<RoutePoints> _selectedPoints = new List<RoutePoints>();
        private List<string> _photoPaths = new List<string>() { null, null, null };
        private List<string> _oldPhotoIds = new List<string>() { null, null, null };

        public EditRoutePage(int userId, int routeId)
        {
            InitializeComponent();
            _userId = userId;
            _routeId = routeId;
            LoadRouteData();
            LoadCategories();
            PointsDataGrid.ItemsSource = _selectedPoints;
        }

        private async void LoadRouteData()
        {
            try
            {
                using (var context = new TouristRoutesEntities())
                {
                    var route = await context.Routes
                        .Include(r => r.Categories)
                        .Include(r => r.RoutePoints.Select(p => p.PointTypes))
                        .Include(r => r.Photos)
                        .FirstOrDefaultAsync(r => r.IdRoute == _routeId);

                    if (route == null)
                    {
                        MessageBox.Show("Маршрут не найден", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        NavigationService?.GoBack();
                        return;
                    }

                    TitleTextBox.Text = route.TitleRoute;
                    DescriptionTextBox.Text = route.DescriptionRoute;
                    LengthTextBox.Text = route.LengthPoint?.ToString();
                    StepsCountTextBox.Text = route.StepsCount?.ToString();

                    _selectedPoints = route.RoutePoints.ToList();
                    RefreshPointsGrid();

                    var photos = route.Photos.OrderBy(p => p.IdPhoto).Take(3).ToList();
                    for (int i = 0; i < photos.Count; i++)
                    {
                        _oldPhotoIds[i] = photos[i].Photo;
                        await LoadPhotoFromDatabase(photos[i].Photo, i);
                    }

                    UpdateAddPhotoButtonsState();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных маршрута: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadPhotoFromDatabase(string photoId, int index)
        {
            if (string.IsNullOrEmpty(photoId)) return;

            try
            {
                ShowPhotoProgress(index + 1, true);

                string fileName = $"route_{_routeId}_photo_{index + 1}.jpg";
                await CloudStorage.DownloadRoutePhotoAsync(photoId, fileName);

                string localPath = Path.Combine(CloudStorage.RoutePhotosDirectoryPath, fileName);
                _photoPaths[index] = localPath;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(localPath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                Resources[$"photo{index + 1}brush"] = new ImageBrush(bitmap);
                UpdatePhotoUI(index + 1, true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки фотографии: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ShowPhotoProgress(index + 1, false);
            }
        }

        private void LoadCategories()
        {
            try
            {
                using (var context = new TouristRoutesEntities())
                {
                    bool isTraveler = context.Users
                        .Where(u => u.IdUser == _userId)
                        .Select(u => u.Roles.NameRole)
                        .FirstOrDefault() == "Путешественник";

                    if (isTraveler)
                    {
                        var customCategory = new List<dynamic>
                        {
                            new { IdCategory = -1, NameCategory = "Пользовательские" }
                        };

                        CategoryComboBox.ItemsSource = customCategory;
                        CategoryComboBox.SelectedValuePath = "IdCategory";
                        CategoryComboBox.SelectedIndex = 0;
                    }
                    else
                    {
                        _categories = context.Categories
                            .Where(c => c.NameCategory != "Пользовательские")
                            .ToList();

                        var categoryList = new List<dynamic>
                        {
                            new { IdCategory = 0, NameCategory = "Выберите категорию" }
                        };
                        categoryList.AddRange(_categories.Select(c => new { c.IdCategory, c.NameCategory }));

                        CategoryComboBox.ItemsSource = categoryList;
                        CategoryComboBox.SelectedValuePath = "IdCategory";

                        var currentRoute = context.Routes.Find(_routeId);
                        if (currentRoute != null)
                        {
                            var selectedItem = categoryList.FirstOrDefault(c => c.IdCategory == currentRoute.IdCategory);
                            CategoryComboBox.SelectedItem = selectedItem;
                        }
                        else
                        {
                            CategoryComboBox.SelectedIndex = 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке категорий: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void AddPhotoButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Изображения (*.jpg, *.jpeg, *.png)|*.jpg;*.jpeg;*.png",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };

            if (openFileDialog.ShowDialog() == true)
            {
                int photoIndex = GetPhotoIndex(button.Name);
                if (photoIndex == -1) return;

                ShowPhotoProgress(photoIndex + 1, true);

                try
                {
                    await LoadPhotoAsync(openFileDialog.FileName, photoIndex + 1);
                    _photoPaths[photoIndex] = openFileDialog.FileName;
                    UpdatePhotoUI(photoIndex + 1, true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка загрузки фото: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    ShowPhotoProgress(photoIndex + 1, false);
                }
            }
        }

        private async Task LoadPhotoAsync(string filePath, int index)
        {
            try
            {
                ShowPhotoProgress(index, true);

                var loadingTask = Task.Delay(500);

                var bitmap = new BitmapImage();

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(filePath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    bitmap.EndInit();
                });

                bitmap.Freeze();

                await loadingTask;

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Resources[$"photo{index}brush"] = new ImageBrush(bitmap);
                    ShowPhotoProgress(index, false);
                });
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ShowPhotoProgress(index, false);
                    MessageBox.Show($"Ошибка загрузки изображения: {ex.Message}");
                });
            }
        }

        private void ShowPhotoProgress(int photoIndex, bool show)
        {
            var progressBar = FindName($"Photo{photoIndex}Progress") as ProgressBar;
            if (progressBar != null)
            {
                progressBar.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void UpdateAddPhotoButtonsState()
        {
            int lastAddedIndex = -1;
            for (int i = 0; i < _photoPaths.Count; i++)
            {
                if (_photoPaths[i] != null)
                {
                    lastAddedIndex = i;
                }
            }

            for (int i = 0; i < 3; i++)
            {
                var addButton = FindName($"AddPhoto{i + 1}Button") as Button;
                if (addButton != null)
                {
                    addButton.IsEnabled = (i == 0) || (i > 0 && _photoPaths[i - 1] != null);

                    if (_photoPaths[i] != null)
                    {
                        addButton.IsEnabled = false;
                    }
                }
            }
        }

        private void UpdatePhotoUI(int photoIndex, bool showPhoto)
        {
            var photoButton = FindName($"Photo{photoIndex}Button") as Button;
            var addButton = FindName($"AddPhoto{photoIndex}Button") as Button;
            var removeButton = FindName($"RemovePhoto{photoIndex}Button") as Button;

            if (photoButton != null && addButton != null && removeButton != null)
            {
                photoButton.Visibility = showPhoto ? Visibility.Visible : Visibility.Collapsed;
                removeButton.Visibility = showPhoto ? Visibility.Visible : Visibility.Collapsed;
                addButton.Visibility = showPhoto ? Visibility.Collapsed : Visibility.Visible;
            }

            UpdateAddPhotoButtonsState();
        }

        private int GetPhotoIndex(string buttonName)
        {
            if (buttonName == "AddPhoto1Button") return 0;
            if (buttonName == "AddPhoto2Button") return 1;
            if (buttonName == "AddPhoto3Button") return 2;
            return -1;
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

        private void ShiftPhotosAfterDeletion(int deletedIndex)
        {
            for (int i = deletedIndex; i < _photoPaths.Count - 1; i++)
            {
                if (_photoPaths[i + 1] != null)
                {
                    _photoPaths[i] = _photoPaths[i + 1];
                    _oldPhotoIds[i] = _oldPhotoIds[i + 1];
                    Resources[$"photo{i + 1}brush"] = Resources[$"photo{i + 2}brush"];

                    UpdatePhotoUI(i + 1, true);

                    _photoPaths[i + 1] = null;
                    _oldPhotoIds[i + 1] = null;
                    Resources[$"photo{i + 2}brush"] = new ImageBrush();
                    UpdatePhotoUI(i + 2, false);
                }
                else
                {
                    break;
                }
            }

            UpdateAddPhotoButtonsState();
        }

        private void RemovePhotoButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            int photoIndex = -1;
            if (button.Name == "RemovePhoto1Button") photoIndex = 0;
            else if (button.Name == "RemovePhoto2Button") photoIndex = 1;
            else if (button.Name == "RemovePhoto3Button") photoIndex = 2;

            if (photoIndex >= 0 && _photoPaths[photoIndex] != null)
            {
                _photoPaths[photoIndex] = null;
                _oldPhotoIds[photoIndex] = null;
                Resources[$"photo{photoIndex + 1}brush"] = new ImageBrush();
                UpdatePhotoUI(photoIndex + 1, false);

                ShiftPhotosAfterDeletion(photoIndex);
            }
        }

        public void AddPointToRoute(RoutePoints point)
        {
            if (point == null) return;

            if (!_selectedPoints.Any(p => p.IdPoint == point.IdPoint))
            {
                using (var context = new TouristRoutesEntities())
                {
                    var fullPoint = context.RoutePoints
                        .Include(p => p.PointTypes)
                        .FirstOrDefault(p => p.IdPoint == point.IdPoint);

                    if (fullPoint != null)
                    {
                        _selectedPoints.Add(fullPoint);
                        RefreshPointsGrid();
                    }
                }
            }
            else
            {
                MessageBox.Show("Эта точка уже добавлена в маршрут", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void RefreshPointsGrid()
        {
            PointsDataGrid.ItemsSource = null;
            PointsDataGrid.ItemsSource = _selectedPoints;
        }

        private void AddPointButton_Click(object sender, RoutedEventArgs e)
        {
            var pointsPage = new PointsPage(_userId);
            pointsPage.PointSelected += (s, point) =>
            {
                AddPointToRoute(point);
                if (NavigationService != null && NavigationService.CanGoBack)
                {
                    NavigationService.GoBack();
                }
            };
            NavigationService?.Navigate(pointsPage);
        }

        private void RemovePointButton_Click(object sender, RoutedEventArgs e)
        {
            if (PointsDataGrid.SelectedItem is RoutePoints selectedPoint)
            {
                _selectedPoints.Remove(selectedPoint);
                RefreshPointsGrid();
            }
        }

        private void ClearPointsButton_Click(object sender, RoutedEventArgs e)
        {
            _selectedPoints.Clear();
            RefreshPointsGrid();
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateRoute()) return;

            SavingProgressPanel.Visibility = Visibility.Visible;
            SaveButton.Visibility = Visibility.Collapsed;

            IsEnabled = false;

            try
            {
                await UpdateRouteAsync();
                await UpdateRoutePhotosAsync();

                SavingProgressPanel.Visibility = Visibility.Collapsed;
                MessageBox.Show("Изменения успешно сохранены!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                SavingProgressPanel.Visibility = Visibility.Collapsed;
                MessageBox.Show($"Ошибка при сохранении изменений: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SaveButton.Visibility = Visibility.Visible;
                IsEnabled = true;
            }
        }

        private async Task UpdateRouteAsync()
        {
            using (var context = new TouristRoutesEntities())
            {
                var route = await context.Routes
                    .Include(r => r.RoutePoints)
                    .FirstOrDefaultAsync(r => r.IdRoute == _routeId);

                if (route == null) return;

                route.TitleRoute = TitleTextBox.Text.Trim();
                route.DescriptionRoute = DescriptionTextBox.Text.Trim();
                route.LengthPoint = decimal.TryParse(LengthTextBox.Text, out var length) ? length : 0;
                route.StepsCount = int.TryParse(StepsCountTextBox.Text, out var steps) ? steps : 0;

                if ((int)CategoryComboBox.SelectedValue != -1)
                {
                    route.IdCategory = (int)CategoryComboBox.SelectedValue;
                }

                route.RoutePoints.Clear();
                foreach (var point in _selectedPoints)
                {
                    var dbPoint = await context.RoutePoints.FindAsync(point.IdPoint);
                    if (dbPoint != null)
                    {
                        route.RoutePoints.Add(dbPoint);
                    }
                }

                await context.SaveChangesAsync();
            }
        }

        private async Task UpdateRoutePhotosAsync()
        {
            try
            {
                using (var context = new TouristRoutesEntities())
                {
                    var currentPhotos = await context.Photos
                        .Where(p => p.IdRoute == _routeId)
                        .OrderBy(p => p.IdPhoto)
                        .ToListAsync();

                    for (int i = 0; i < 3; i++)
                    {
                        if (i < currentPhotos.Count && _photoPaths[i] == null)
                        {
                            await CloudStorage.DeleteFileAsync(currentPhotos[i].Photo);
                            context.Photos.Remove(currentPhotos[i]);
                        }
                    }

                    for (int i = 0; i < _photoPaths.Count; i++)
                    {
                        if (!string.IsNullOrEmpty(_photoPaths[i]))
                        {
                            string fileName = $"Route_{_routeId}_Photo_{i + 1}";
                            string fileId = await CloudStorage.UploadRoutePhotoAsync(
                                _photoPaths[i],
                                fileName,
                                _oldPhotoIds[i]);

                            if (i < currentPhotos.Count)
                            {
                                currentPhotos[i].Photo = fileId;
                            }
                            else
                            {
                                var photo = new Photos
                                {
                                    IdRoute = _routeId,
                                    Photo = fileId,
                                    DateAddedPhoto = DateTime.Now
                                };
                                context.Photos.Add(photo);
                            }
                        }
                    }

                    await context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении фотографий: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        private bool ValidateRoute()
        {
            var errors = new List<string>();

            if (CategoryComboBox.SelectedItem == null ||
               (CategoryComboBox.SelectedValue is int id && id == 0))
            {
                errors.Add("Выберите категорию маршрута");
            }

            if (string.IsNullOrWhiteSpace(TitleTextBox.Text))
            {
                errors.Add("Введите название маршрута");
            }

            if (!decimal.TryParse(LengthTextBox.Text, out decimal length) || length <= 0)
            {
                errors.Add("Укажите корректную протяженность маршрута (положительное число)");
            }

            if (!int.TryParse(StepsCountTextBox.Text, out int steps) || steps <= 0)
            {
                errors.Add("Укажите корректное количество шагов (положительное целое число)");
            }

            if (_selectedPoints.Count == 0)
            {
                errors.Add("Добавьте хотя бы одну точку в маршрут");
            }

            if (errors.Count > 0)
            {
                MessageBox.Show(string.Join("\n", errors), "Ошибки ввода", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            return true;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
        }
    }
}
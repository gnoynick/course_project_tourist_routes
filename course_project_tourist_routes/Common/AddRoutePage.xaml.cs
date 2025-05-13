using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System.Threading.Tasks;
using System.IO;
using static course_project_tourist_routes.Common.PointsPage;
using System.Windows.Input;

namespace course_project_tourist_routes.Common
{
    public partial class AddRoutePage : Page
    {
        private readonly int _userId;
        private List<Categories> _categories = new List<Categories>();
        private readonly List<RoutePoints> _selectedPoints = new List<RoutePoints>();
        private readonly List<string> _photoPaths = new List<string>() { null, null, null };

        public AddRoutePage(int userId)
        {
            InitializeComponent();
            _userId = userId;
            LoadCategories();
            PointsDataGrid.ItemsSource = _selectedPoints;

            UpdateAddPhotoButtonsState();

            AddPhoto1Button.IsEnabled = true;
            AddPhoto2Button.IsEnabled = false;
            AddPhoto3Button.IsEnabled = false;
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !(char.IsDigit(e.Text, 0) && e.Text != " ");
        }

        private void NumberTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                string newText = new string(textBox.Text.Where(char.IsDigit).ToArray());

                if (newText != textBox.Text)
                {
                    textBox.Text = newText;
                    textBox.CaretIndex = newText.Length;
                }
            }
        }

        private void LengthTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox textBox = (TextBox)sender;
            string newText = textBox.Text.Insert(textBox.CaretIndex, e.Text);

            bool isDigit = char.IsDigit(e.Text, 0);
            bool isComma = e.Text == ",";

            if (!isDigit && !isComma)
            {
                e.Handled = true;
                return;
            }

            if (isComma)
            {
                if (textBox.Text.Length == 0 || textBox.Text.Contains(","))
                {
                    e.Handled = true;
                    return;
                }
            }
        }

        private void LengthTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                string newText = new string(textBox.Text.Where(c => char.IsDigit(c) || c == ',').ToArray());

                if (newText.StartsWith(",")) newText = newText.Substring(1);

                int commaCount = newText.Count(c => c == ',');
                if (commaCount > 1)
                {
                    int firstCommaIndex = newText.IndexOf(',');
                    newText = newText.Substring(0, firstCommaIndex + 1) +
                             newText.Substring(firstCommaIndex + 1).Replace(",", "");
                }

                if (newText != textBox.Text)
                {
                    int caretIndex = textBox.CaretIndex;
                    textBox.Text = newText;
                    textBox.CaretIndex = Math.Min(caretIndex, newText.Length);
                }
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
                        _categories = context.Categories.ToList();

                        var categoryList = new List<dynamic>
                        {
                            new { IdCategory = 0, NameCategory = "Выберите категорию" }
                        };
                        categoryList.AddRange(_categories.Select(c => new { c.IdCategory, c.NameCategory }));

                        CategoryComboBox.ItemsSource = categoryList;
                        CategoryComboBox.SelectedValuePath = "IdCategory";
                        CategoryComboBox.SelectedIndex = 0;
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
            if (!(sender is Button button)) return;

            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Image files (*.jpg, *.jpeg, *.png)|*.jpg;*.jpeg;*.png",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };

            if (openFileDialog.ShowDialog() == true)
            {
                long maxFileSize = 1 * 1024 * 1024;
                FileInfo fileInfo = new FileInfo(openFileDialog.FileName);

                if (fileInfo.Length > maxFileSize)
                {
                    double fileSizeInMb = fileInfo.Length / (1024.0 * 1024.0);
                    MessageBox.Show($"Размер фото не должен превышать 1 МБ.\n\n" +
                                  $"Текущий размер: {fileSizeInMb:F2} МБ",
                                  "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                int photoIndex = GetPhotoIndex(button.Name);
                if (photoIndex == -1) return;

                ShowPhotoProgress(photoIndex, true);

                try
                {
                    await LoadPhotoAsync(openFileDialog.FileName, photoIndex);
                    _photoPaths[photoIndex] = openFileDialog.FileName;
                    UpdatePhotoUI(photoIndex, true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка загрузки фото: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    ShowPhotoProgress(photoIndex, false);
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
                    Resources[$"photo{index + 1}brush"] = new ImageBrush(bitmap);
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
            if (FindName($"Photo{photoIndex + 1}Progress") is ProgressBar progressBar)
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
                if (FindName($"AddPhoto{i + 1}Button") is Button addButton)
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
            if (FindName($"Photo{photoIndex + 1}Button") is Button photoButton && FindName($"AddPhoto{photoIndex + 1}Button") is Button addButton && FindName($"RemovePhoto{photoIndex + 1}Button") is Button removeButton)
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
            if (!(sender is Button button)) return;

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
            BackButton.IsCancel = false;
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
                    Resources[$"photo{i + 1}brush"] = Resources[$"photo{i + 2}brush"];

                    UpdatePhotoUI(i, true);

                    _photoPaths[i + 1] = null;
                    Resources[$"photo{i + 2}brush"] = new ImageBrush();
                    UpdatePhotoUI(i + 1, false);
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
            if (!(sender is Button button)) return;

            int photoIndex = -1;
            if (button.Name == "RemovePhoto1Button") photoIndex = 0;
            else if (button.Name == "RemovePhoto2Button") photoIndex = 1;
            else if (button.Name == "RemovePhoto3Button") photoIndex = 2;

            if (photoIndex >= 0 && _photoPaths[photoIndex] != null)
            {
                _photoPaths[photoIndex] = null;
                Resources[$"photo{photoIndex + 1}brush"] = new ImageBrush();
                UpdatePhotoUI(photoIndex, false);

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
            var pointsPage = new PointsPage(_userId, PointsPageMode.RouteEdit);
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
                int routeId = await SaveRouteAsync();

                if (_photoPaths.Any(p => !string.IsNullOrEmpty(p)))
                {
                    await SavePhotosToDatabase(routeId);
                }

                SavingProgressPanel.Visibility = Visibility.Collapsed;
                MessageBox.Show("Маршрут успешно сохранен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                ClearForm();
            }
            catch (Exception ex)
            {
                SavingProgressPanel.Visibility = Visibility.Collapsed;
                MessageBox.Show($"Ошибка при сохранении маршрута: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SaveButton.Visibility = Visibility.Visible;
                IsEnabled = true;
            }
        }

        private async Task<int> SaveRouteAsync()
        {
            using (var context = new TouristRoutesEntities())
            {
                var newRoute = new Routes
                {
                    IdUser = _userId,
                    TitleRoute = TitleTextBox.Text.Trim(),
                    DescriptionRoute = DescriptionTextBox.Text.Trim(),
                    LengthRoute = decimal.TryParse(LengthTextBox.Text, out var length) ? length : 0,
                    StepsCount = int.TryParse(StepsCountTextBox.Text, out var steps) ? steps : 0,
                    DateAddedRoute = DateTime.Now
                };

                if ((int)CategoryComboBox.SelectedValue == -1)
                {
                    var userCategory = context.Categories
                        .FirstOrDefault(c => c.NameCategory == "Пользовательские");

                    if (userCategory == null)
                    {
                        userCategory = new Categories { NameCategory = "Пользовательские" };
                        context.Categories.Add(userCategory);
                        await context.SaveChangesAsync();
                    }
                    newRoute.IdCategory = userCategory.IdCategory;
                }
                else
                {
                    newRoute.IdCategory = (int)CategoryComboBox.SelectedValue;
                }

                foreach (var point in _selectedPoints)
                {
                    var dbPoint = await context.RoutePoints.FindAsync(point.IdPoint);
                    if (dbPoint != null)
                    {
                        newRoute.RoutePoints.Add(dbPoint);
                    }
                }

                context.Routes.Add(newRoute);
                await context.SaveChangesAsync();
                return newRoute.IdRoute;
            }
        }

        private async Task SavePhotosToDatabase(int routeId)
        {
            try
            {
                using (var context = new TouristRoutesEntities())
                {
                    for (int i = 0; i < _photoPaths.Count; i++)
                    {
                        if (!string.IsNullOrEmpty(_photoPaths[i]))
                        {
                            string fileName = $"Route_{routeId}_Photo_{i + 1}";
                            string fileId = await Task.Run(() =>
                                CloudStorage.UploadRoutePhotoAsync(_photoPaths[i], fileName));

                            if (!string.IsNullOrEmpty(fileId))
                            {
                                var photo = new Photos
                                {
                                    IdRoute = routeId,
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
                MessageBox.Show($"Ошибка при сохранении фотографий: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        private bool ValidateRoute()
        {
            if ((CategoryComboBox.SelectedItem == null || (CategoryComboBox.SelectedValue is int id && id == 0)) &&
                string.IsNullOrWhiteSpace(TitleTextBox.Text) &&
                string.IsNullOrWhiteSpace(LengthTextBox.Text) &&
                string.IsNullOrWhiteSpace(StepsCountTextBox.Text) &&
                _selectedPoints.Count == 0 &&
                string.IsNullOrWhiteSpace(DescriptionTextBox.Text))
            {
                MessageBox.Show("Все поля должны быть заполнены!", "Ошибки ввода", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            var errors = new List<string>();

            if (CategoryComboBox.SelectedItem == null ||
               (CategoryComboBox.SelectedValue is int categoryId && categoryId == 0))
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

            if (!string.IsNullOrEmpty(StepsCountTextBox.Text))
            {
                if (!int.TryParse(StepsCountTextBox.Text, out int steps) || steps <= 0)
                {
                    errors.Add("Укажите корректное количество шагов (положительное целое число)");
                }
            }

            if (_selectedPoints.Count < 2)
            {
                errors.Add("Добавьте хотя бы две точки в маршрут");
            }

            if (string.IsNullOrWhiteSpace(DescriptionTextBox.Text))
            {
                errors.Add("Введите описание маршрута");
            }

            if (errors.Count > 0)
            {
                MessageBox.Show(string.Join("\n", errors), "Ошибки ввода", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            return true;
        }

        private void ClearForm()
        {
            TitleTextBox.Text = string.Empty;
            DescriptionTextBox.Text = string.Empty;
            LengthTextBox.Text = string.Empty;
            StepsCountTextBox.Text = string.Empty;

            if (CategoryComboBox.Items.Count > 0)
            {
                CategoryComboBox.SelectedIndex = 0;
            }

            _selectedPoints.Clear();
            RefreshPointsGrid();

            for (int i = 0; i < 3; i++)
            {
                _photoPaths[i] = null;
                Resources[$"photo{i + 1}brush"] = new ImageBrush();
                UpdatePhotoUI(i, false);
            }

            AddPhoto1Button.IsEnabled = true;
            AddPhoto2Button.IsEnabled = false;
            AddPhoto3Button.IsEnabled = false;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (HasUnsavedChanges())
            {
                var result = MessageBox.Show("У вас есть несохраненные изменения. Вы уверены, что хотите выйти? Все несохраненные данные будут потеряны.",
                                          "Подтверждение выхода",
                                          MessageBoxButton.YesNo,
                                          MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    return;
                }
            }

            if (NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
        }

        private bool HasUnsavedChanges()
        {
            if (!string.IsNullOrWhiteSpace(TitleTextBox.Text)) return true;
            if (!string.IsNullOrWhiteSpace(DescriptionTextBox.Text)) return true;
            if (!string.IsNullOrWhiteSpace(LengthTextBox.Text)) return true;
            if (!string.IsNullOrWhiteSpace(StepsCountTextBox.Text)) return true;

            if (CategoryComboBox.SelectedItem != null &&
                CategoryComboBox.SelectedValue is int id &&
                id != 0 && id != -1)
            {
                return true;
            }

            if (_selectedPoints.Count > 0) return true;

            if (_photoPaths.Any(p => !string.IsNullOrEmpty(p))) return true;

            return false;
        }
    }
}
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using course_project_tourist_routes.Common;
using System.Collections.Generic;
using System.Data.Entity;
using System.Windows.Input;

namespace course_project_tourist_routes.Traveler
{
    public partial class EditTravelEventPage : Page
    {
        private readonly int _userId;
        private readonly int _eventId;
        private TravelEvents _currentEvent;
        private dynamic _selectedRoute;
        private Dictionary<int, ImageBrush> _routePhotosCache = new Dictionary<int, ImageBrush>();

        public EditTravelEventPage(int userId, int eventId)
        {
            InitializeComponent();
            _userId = userId;
            _eventId = eventId;
            LoadEventData();
            LoadOrganizerInfo();
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !(char.IsDigit(e.Text, 0) && e.Text != " ");
        }

        private void TextBoxPasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string));
                if (!IsTextAllowed(text))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
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


        private bool IsTextAllowed(string text)
        {
            return text.All(c => char.IsDigit(c));
        }

        private async void LoadEventData()
        {
            try
            {
                using (var context = new TouristRoutesEntities())
                {
                    _currentEvent = context.TravelEvents.FirstOrDefault(e => e.IdEvent == _eventId);
                    if (_currentEvent != null)
                    {
                        TitleTextBox.Text = _currentEvent.TitleEvent;
                        MaxParticipantsTextBox.Text = _currentEvent.MaxParticipants.ToString();
                        StartDatePicker.SelectedDate = _currentEvent.StartDate;
                        EndDatePicker.SelectedDate = _currentEvent.EndDate;
                        DescriptionTextBox.Text = _currentEvent.DescriptionEvent;

                        foreach (ComboBoxItem item in StatusComboBox.Items)
                        {
                            if (item.Content.ToString() == _currentEvent.StatusEvent)
                            {
                                StatusComboBox.SelectedItem = item;
                                break;
                            }
                        }

                        var route = context.Routes
                            .Include(r => r.Users)
                            .Include(r => r.RoutePoints)
                            .FirstOrDefault(r => r.IdRoute == _currentEvent.IdRoute);

                        if (route != null)
                        {
                            var photoBrush = await LoadRoutePhotoAsync(route.IdRoute);
                            _routePhotosCache[route.IdRoute] = photoBrush;

                            _selectedRoute = new
                            {
                                IdRoute = route.IdRoute,
                                TitleRoute = route.TitleRoute,
                                DescriptionRoute = route.DescriptionRoute.Length > 30 ?
                                route.DescriptionRoute.Substring(0, 30) + "..." :
                                route.DescriptionRoute,
                                CategoryName = route.Categories.NameCategory,
                                AuthorInfo = route.Categories.NameCategory == "Пользовательские" ? $"Автор: {route.Users.UserName}" : "",
                                Countries = string.Join(", ", route.RoutePoints.Select(p => p.Country).Distinct()),
                                Cities = string.Join(", ", route.RoutePoints.Select(p => p.City).Distinct()),
                                RoutePhotoBrush = photoBrush
                            };

                            SelectedRouteListView.Items.Add(_selectedRoute);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных путешествия: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    ImageSource = bitmap,
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

        private void LoadOrganizerInfo()
        {
            try
            {
                using (var context = new TouristRoutesEntities())
                {
                    var user = context.Users.FirstOrDefault(u => u.IdUser == _userId);
                    if (user != null)
                    {
                        OrganizerTextBox.Text = user.UserName;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных организатора: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.GoBack();
        }

        private void SelectRouteButton_Click(object sender, RoutedEventArgs e)
        {
            var routesPage = new RoutesPage(_userId, true);
            routesPage.RouteSelected += async route =>
            {
                var photoBrush = await LoadRoutePhotoAsync(route.IdRoute);
                _routePhotosCache[route.IdRoute] = photoBrush;

                route.RoutePhotoBrush = photoBrush;
                SetSelectedRoute(route);
            };
            NavigationService.Navigate(routesPage);
        }

        private void PhotoBackButton_Click(object sender, RoutedEventArgs e)
        {
            BackButton.IsCancel = true;
            SupRect.Visibility = Visibility.Collapsed;
            FullScreenPhoto.Visibility = Visibility.Collapsed;
            PhotoBackButton.Visibility = Visibility.Collapsed;
        }

        public void SetSelectedRoute(dynamic route)
        {
            _selectedRoute = route;
            SelectedRouteListView.Items.Clear();
            SelectedRouteListView.Items.Add(route);
        }

        private void SelectedRouteListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SelectedRouteListView.SelectedItem != null)
            {
                dynamic selectedItem = SelectedRouteListView.SelectedItem;
                int routeId = selectedItem.IdRoute;
                NavigationService?.Navigate(new OpenRoutePage(routeId, _userId));
                SelectedRouteListView.SelectedItem = null;
            }
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

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {

            if (string.IsNullOrWhiteSpace(TitleTextBox.Text) &&
    string.IsNullOrWhiteSpace(MaxParticipantsTextBox.Text) &&
    StartDatePicker.SelectedDate == null &&
    EndDatePicker.SelectedDate == null &&
    _selectedRoute == null &&
    string.IsNullOrWhiteSpace(DescriptionTextBox.Text))
            {
                MessageBox.Show("Все поля должны быть заполнены!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (StartDatePicker.SelectedDate == null && EndDatePicker.SelectedDate == null)
            {
                MessageBox.Show("Введите даты проведения путешествия", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            else if (StartDatePicker.SelectedDate == null)
            {
                MessageBox.Show("Введите дату начала путешествия", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            else if (EndDatePicker.SelectedDate == null)
            {
                MessageBox.Show("Введите дату окончания путешествия", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            else if (EndDatePicker.SelectedDate < StartDatePicker.SelectedDate)
            {
                MessageBox.Show("Дата окончания не может быть раньше даты начала", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(TitleTextBox.Text))
            {
                MessageBox.Show("Введите название путешествия", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(MaxParticipantsTextBox.Text, out int maxParticipants) || maxParticipants <= 0)
            {
                MessageBox.Show("Укажите корректное количество участников", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_selectedRoute == null)
            {
                MessageBox.Show("Выберите маршрут", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(DescriptionTextBox.Text))
            {
                MessageBox.Show("Заполните описание путешествия", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                SaveButton.Visibility = Visibility.Collapsed;
                SavingProgressPanel.Visibility = Visibility.Visible;
                SaveButton.IsEnabled = false;

                using (var context = new TouristRoutesEntities())
                {
                    var eventToUpdate = await context.TravelEvents.FindAsync(_eventId);
                    if (eventToUpdate != null)
                    {
                        eventToUpdate.IdRoute = _selectedRoute.IdRoute;
                        eventToUpdate.TitleEvent = TitleTextBox.Text;
                        eventToUpdate.StartDate = StartDatePicker.SelectedDate.Value;
                        eventToUpdate.EndDate = EndDatePicker.SelectedDate.Value;
                        eventToUpdate.MaxParticipants = maxParticipants;
                        eventToUpdate.StatusEvent = (StatusComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
                        eventToUpdate.DescriptionEvent = DescriptionTextBox.Text;

                        await context.SaveChangesAsync();
                    }
                }

                SavingProgressPanel.Visibility = Visibility.Collapsed;
                MessageBox.Show("Изменения успешно сохранены!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении изменений: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SaveButton.Visibility = Visibility.Visible;
                SaveButton.IsEnabled = true;
            }
        }
    }
}
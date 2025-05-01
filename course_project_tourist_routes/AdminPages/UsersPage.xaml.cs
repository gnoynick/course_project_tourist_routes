using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using MaterialDesignThemes.Wpf;

namespace course_project_tourist_routes.AdminPages
{
    public partial class UsersPage : Page
    {
        private int _userId;
        private List<Roles> _roles = new List<Roles>();
        private Dictionary<int, ImageBrush> _userPhotosCache = new Dictionary<int, ImageBrush>();

        public UsersPage(int userId)
        {
            InitializeComponent();
            _userId = userId;

            LoadRoles();
            LoadStatuses();
            LoadUsers();
        }

        private void LoadRoles()
        {
            try
            {
                using (var context = new TouristRoutesEntities())
                {
                    _roles = context.Roles.OrderBy(r => r.IdRole).ToList();
                    _roles.Insert(0, new Roles { IdRole = 0, NameRole = "Все роли" });
                    RoleComboBox.ItemsSource = _roles;
                    RoleComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке ролей: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadStatuses()
        {
            StatusComboBox.ItemsSource = new List<string> { "Все статусы", "Активен", "Заблокирован" };
            StatusComboBox.SelectedIndex = 0;
        }

        private async void LoadUsers(int? roleId = null, string status = null, string searchText = null)
        {
            try
            {
                using (var context = new TouristRoutesEntities())
                {
                    var query = context.Users
                        .Include(u => u.Roles)
                        .Where(u => u.IdUser != _userId)
                        .AsQueryable();

                    if (roleId.HasValue && roleId.Value != 0)
                        query = query.Where(u => u.IdRole == roleId.Value);

                    if (!string.IsNullOrEmpty(status) && status != "Все статусы")
                        query = query.Where(u => u.AccountStatus == status);

                    if (!string.IsNullOrEmpty(searchText))
                    {
                        searchText = searchText.ToLower();
                        query = query.Where(u =>
                            u.UserName.ToLower().Contains(searchText) ||
                            u.Email.ToLower().Contains(searchText));
                    }

                    var users = await query
                        .OrderByDescending(u => u.IdUser)
                        .ToListAsync();

                    var newUsers = users.Where(u => !_userPhotosCache.ContainsKey(u.IdUser)).ToList();
                    await Task.WhenAll(newUsers.Select(async user =>
                    {
                        var photoBrush = await LoadUserPhotoAsync(user.ProfilePhoto, user.IdUser);
                        _userPhotosCache[user.IdUser] = photoBrush;
                    }));

                    UsersListView.ItemsSource = users.Select(user => new
                    {
                        user.IdUser,
                        user.UserName,
                        user.Email,
                        user.Roles,
                        user.AccountStatus,
                        user.DateAddedUser,
                        ProfilePhotoBrush = _userPhotosCache.TryGetValue(user.IdUser, out var brush) ? brush : CreateDefaultImageBrush(),
                        AccountStatusIcon = user.AccountStatus == "Активен" ? PackIconKind.LockOpenVariant : PackIconKind.Lock,
                        AccountStatusButtonTooltip = user.AccountStatus == "Активен" ? "Заблокировать" : "Разблокировать"
                    }).ToList();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке пользователей: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingGrid.Visibility = Visibility.Collapsed;
                UsersListViewBorder.Visibility = Visibility.Visible;
            }
        }

        private async Task<ImageBrush> LoadUserPhotoAsync(string profilePhotoId, int userId)
        {
            // Путь к изображению по умолчанию
            string defaultPhotoPath = "pack://application:,,,/Resources/profile_photo.jpg";

            if (string.IsNullOrEmpty(profilePhotoId))
                return CreateImageBrush(defaultPhotoPath);

            try
            {
                // Создаем директорию для хранения фото профилей
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appFolder = Path.Combine(localAppData, "TouristRoutes");
                string profilePhotosDir = Path.Combine(appFolder, "profile_photos");
                Directory.CreateDirectory(profilePhotosDir);

                string photoPath = Path.Combine(profilePhotosDir, $"user_{userId}.jpg");

                // Если файл уже существует и не пустой - используем его
                if (File.Exists(photoPath) && new FileInfo(photoPath).Length > 0)
                {
                    return CreateImageBrush(photoPath);
                }

                // Загружаем фото из облака
                await CloudStorage.DownloadProfilePhotoAsync(profilePhotoId, userId);

                // Проверяем, что файл был загружен
                if (File.Exists(photoPath))
                {
                    return CreateImageBrush(photoPath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки фото: {ex.Message}");
            }

            // В случае любой ошибки возвращаем изображение по умолчанию
            return CreateImageBrush(defaultPhotoPath);
        }

        private ImageBrush CreateImageBrush(string imagePath)
        {
            var imageBrush = new ImageBrush();

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(imagePath, UriKind.RelativeOrAbsolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bitmap.EndInit();
                bitmap.Freeze();

                imageBrush.ImageSource = bitmap;
                imageBrush.Stretch = Stretch.UniformToFill;
                imageBrush.Freeze();
            }
            catch
            {
                // Если что-то пошло не так, используем изображение по умолчанию
                imageBrush.ImageSource = new BitmapImage(
                    new Uri("pack://application:,,,/Resources/profile_photo.jpg"));
                imageBrush.Freeze();
            }

            return imageBrush;
        }

        private ImageBrush CreateDefaultImageBrush()
        {
            return new ImageBrush
            {
                ImageSource = new BitmapImage(
                    new Uri("pack://application:,,,/Resources/profile_photo.jpg")),
                Stretch = Stretch.UniformToFill
            };
        }

        private void ApplyFilters(bool forceReload = false)
        {
            var selectedRole = RoleComboBox.SelectedItem as Roles;
            var selectedStatus = StatusComboBox.SelectedItem as string;
            var searchText = SearchTextBox.Text.Trim();

            if (!forceReload &&
                (selectedRole == null || selectedRole.IdRole == 0) &&
                (selectedStatus == null || selectedStatus == "Все статусы") &&
                string.IsNullOrEmpty(searchText))
            {
                return;
            }

            LoadUsers(
                selectedRole?.IdRole == 0 ? null : (selectedRole?.IdRole),
                selectedStatus == "Все статусы" ? null : selectedStatus,
                searchText
            );
        }

        private void RoleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFilters();
        private void StatusComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFilters();

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(500);
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                ApplyFilters();
            };
            timer.Start();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            CloudStorage.ClearProfilePhotosDirectoryAsync();
            NavigationService.GoBack();
        }

        private void ResetFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Text = "";
            RoleComboBox.SelectedIndex = 0;
            StatusComboBox.SelectedIndex = 0;

            LoadUsers();
        }

        private void ToggleStatusButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is int userId))
                return;

            try
            {
                using (var context = new TouristRoutesEntities())
                {
                    var user = context.Users.FirstOrDefault(u => u.IdUser == userId);
                    if (user == null) return;

                    user.AccountStatus = user.AccountStatus == "Активен" ? "Заблокирован" : "Активен";
                    context.SaveChanges();
                    ApplyFilters(true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при изменении статуса: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is int userId))
                return;

            if (MessageBox.Show("Вы уверены, что хотите удалить этого пользователя?",
                "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                using (var context = new TouristRoutesEntities())
                {
                    var user = context.Users
                        .Include(u => u.Favorites)
                        .Include(u => u.Routes.Select(r => r.Photos))
                        .Include(u => u.Routes.Select(r => r.Favorites))
                        .Include(u => u.HikeParticipants)
                        .FirstOrDefault(u => u.IdUser == userId);

                    if (user != null)
                    {
                        if (!string.IsNullOrEmpty(user.ProfilePhoto))
                        {
                            CloudStorage.DeleteFileAsync(user.ProfilePhoto);
                        }

                        foreach (var route in user.Routes)
                        {
                            foreach (var photo in route.Photos.Where(p => !string.IsNullOrEmpty(p.Photo)))
                            {
                                CloudStorage.DeleteFileAsync(photo.Photo);
                            }
                        }

                        context.HikeParticipants.RemoveRange(user.HikeParticipants);

                        foreach (var route in user.Routes.ToList())
                        {
                            context.Favorites.RemoveRange(route.Favorites);

                            context.Photos.RemoveRange(route.Photos);

                            context.Routes.Remove(route);
                        }

                        context.Favorites.RemoveRange(user.Favorites);

                        context.Users.Remove(user);

                        context.SaveChanges();

                        _userPhotosCache.Remove(userId);

                        ApplyFilters(true);

                        MessageBox.Show("Пользователь упешно удален.",
                            "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении пользователя: {ex.Message}\n\n" +
                    "Подробности см. в логах.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine($"Ошибка удаления пользователя: {ex}");
            }
        }

        private void UserPhotoButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                FullScreenPhoto.Fill = button.Background as ImageBrush;
                SupRect.Visibility = Visibility.Visible;
                FullScreenPhoto.Visibility = Visibility.Visible;
                PhotoBackButton.Visibility = Visibility.Visible;
            }
        }

        private void PhotoBackButton_Click(object sender, RoutedEventArgs e)
        {
            SupRect.Visibility = Visibility.Collapsed;
            FullScreenPhoto.Visibility = Visibility.Collapsed;
            PhotoBackButton.Visibility = Visibility.Collapsed;
        }

        private void AddAdminButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new AddAdminPage());
        }
    }
}
﻿using course_project_tourist_routes.Admin;
using course_project_tourist_routes.Traveler;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Data.Entity;

namespace course_project_tourist_routes.Common
{
    public partial class SettingsPage : Page
    {
        private string _originalLogin;
        private string _originalStatus;
        private readonly int _userId;
        private readonly AdminPage _adminPage;
        private readonly TravelerPage _travelerPage;
        private readonly Users _currentUser;
        private ImageSource _previousAvatar;
        private string _newImagePath;
        private readonly ImageBrush _currentImageBrush;
        private bool _isPhotoLoaded = false;


        public SettingsPage(int userId, string login, string status, AdminPage adminPage, Users user)
        {
            InitializeComponent();
            _currentUser = user;
            _userId = userId;
            _originalLogin = login;
            _originalStatus = status;
            _adminPage = adminPage;

            LoginTextBox.Text = login;
            StatusTextBox.Text = status;

            _currentImageBrush = new ImageBrush();
            Resources["imagebrushset"] = _currentImageBrush;

            Loaded += SettingsPage_Loaded;
        }

        public SettingsPage(int userId, string login, string status, TravelerPage travelerPage, Users user)
        {
            InitializeComponent();
            _currentUser = user;
            _userId = userId;
            _originalLogin = login;
            _originalStatus = status;
            _travelerPage = travelerPage;

            LoginTextBox.Text = login;
            StatusTextBox.Text = status;

            _currentImageBrush = new ImageBrush();
            Resources["imagebrushset"] = _currentImageBrush;

            Loaded += SettingsPage_Loaded;
        }

        private async void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_isPhotoLoaded)
            {
                await LoadProfilePhotoAsync();
                _isPhotoLoaded = true;
            }
        }

        private void SettingsPage_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Visibility == Visibility.Visible)
            {
                using (var context = new TouristRoutesEntities())
                {
                    context.ChangeTracker.Entries().ToList().ForEach(x => x.Reload());
                }

                _ = LoadProfilePhotoAsync();
            }
        }

        private async Task LoadProfilePhotoAsync()
        {
            try
            {
                PhotoLoadingProgress.Visibility = Visibility.Visible;
                ProfilePhoto.Visibility = Visibility.Collapsed;

                string photoPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "TouristRoutes", "current_profile_photo.jpg");

                if (File.Exists(photoPath) && new FileInfo(photoPath).Length > 0)
                {
                    using (var stream = new FileStream(photoPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        var image = new BitmapImage();
                        image.BeginInit();
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.StreamSource = stream;
                        image.EndInit();
                        image.Freeze();
                        UpdateProfileImage(image);
                    }
                }
                else
                {
                    await CloudStorage.DownloadCurrentUserPhotoAsync(_currentUser.ProfilePhoto);

                    if (File.Exists(photoPath))
                    {
                        var image = new BitmapImage(new Uri(photoPath));
                        UpdateProfileImage(image);
                    }
                    else
                    {
                        UpdateProfileImage(new BitmapImage(new Uri("pack://application:,,,/Resources/profile_photo.jpg")));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки фото: {ex.Message}");
                UpdateProfileImage(new BitmapImage(new Uri("pack://application:,,,/Resources/profile_photo.jpg")));
            }
            finally
            {
                PhotoLoadingProgress.Visibility = Visibility.Collapsed;
                ProfilePhoto.Visibility = Visibility.Visible;
            }
        }

        private void UpdateProfileImage(BitmapImage image)
        {
            _currentImageBrush.ImageSource = image;
            SharedResources.ProfileImageBrush = _currentImageBrush;

            if (_adminPage != null)
                _adminPage.Resources["imagebrush"] = _currentImageBrush;
            else if (_travelerPage != null)
                _travelerPage.Resources["imagebrush"] = _currentImageBrush;
        }

        private void ChangePhotoButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Изображения|*.jpg;*.jpeg;*.png",
                Title = "Выберите новое фото профиля"
            };

            if (dialog.ShowDialog() == true)
            {
                const long maxFileSize = 1 * 1024 * 1024;
                FileInfo fileInfo = new FileInfo(dialog.FileName);

                if (fileInfo.Length > maxFileSize)
                {
                    double fileSizeInMb = fileInfo.Length / (1024.0 * 1024.0);
                    MessageBox.Show($"Размер фото не должен превышать 1 МБ.\n\n" +
                                    $"Текущий размер: {fileSizeInMb:F2} МБ",
                                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                try
                {
                    _previousAvatar = _currentImageBrush.ImageSource;

                    var newImage = new BitmapImage(new Uri(dialog.FileName));
                    UpdateProfileImage(newImage);

                    _newImagePath = dialog.FileName;

                    CancelPhotoStackPanel.Visibility = Visibility.Visible;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка загрузки изображения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void SavePhotoButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_newImagePath))
            {
                MessageBox.Show("Не выбрано новое фото", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                PhotoLoadingProgress.Visibility = Visibility.Visible;
                SavePhotoButton.IsEnabled = false;

                string newFileId = await CloudStorage.UploadProfilePhotoAsync(_newImagePath, $"user_id{_userId}", _currentUser.ProfilePhoto);

                if (string.IsNullOrEmpty(newFileId))
                {
                    MessageBox.Show("Не удалось сохранить фото", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                using (var db = new TouristRoutesEntities())
                {
                    var user = db.Users.FirstOrDefault(u => u.IdUser == _userId);
                    if (user != null)
                    {
                        user.ProfilePhoto = newFileId;
                        await db.SaveChangesAsync();
                        _currentUser.ProfilePhoto = newFileId;
                    }
                }

                await UpdateProfileImageImmediately(_newImagePath);

                MessageBox.Show("Фото профиля успешно обновлено!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения фото: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                PhotoLoadingProgress.Visibility = Visibility.Collapsed;
                SavePhotoButton.IsEnabled = true;
                CancelPhotoStackPanel.Visibility = Visibility.Collapsed;
                _newImagePath = null;
            }
        }

        private async Task UpdateProfileImageImmediately(string imagePath)
        {
            try
            {
                var newImage = new BitmapImage();
                newImage.BeginInit();
                newImage.UriSource = new Uri(imagePath);
                newImage.CacheOption = BitmapCacheOption.OnLoad;
                newImage.EndInit();
                newImage.Freeze();

                UpdateProfileImage(newImage);

                string tempPhotoPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "TouristRoutes", "current_profile_photo.jpg");

                Directory.CreateDirectory(Path.GetDirectoryName(tempPhotoPath));

                if (File.Exists(tempPhotoPath))
                {
                    File.Delete(tempPhotoPath);
                }

                await Task.Delay(100);
                File.Copy(imagePath, tempPhotoPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка обновления фото: {ex.Message}");
                MessageBox.Show("Не удалось сохранить фото локально", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CancelPhotoButton_Click(object sender, RoutedEventArgs e)
        {
            if (_previousAvatar != null)
            {
                var brush = new ImageBrush { ImageSource = _previousAvatar, Stretch = Stretch.UniformToFill };
                Resources["imagebrushset"] = brush;
            }
            CancelPhotoStackPanel.Visibility = Visibility.Collapsed;
            _newImagePath = null;
        }

        private void LoginTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
        }

        private void LoginTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsValidUsername(LoginTextBox.Text))
            {
                LoginTextBox.Text = Regex.Replace(LoginTextBox.Text, @"[^a-zA-Zа-яА-Я0-9_]", "");
                LoginTextBox.CaretIndex = LoginTextBox.Text.Length;
            }
        }

        private static bool IsValidUsername(string username) => Regex.IsMatch(username, @"^[a-zA-Zа-яА-Я0-9_]*$");

        private void EditLoginButton_Click(object sender, RoutedEventArgs e)
        {
            LoginTextBox.IsReadOnly = false;
            LoginTextBox.Focus();
            LoginTextBox.CaretIndex = LoginTextBox.Text.Length;
            LoginButtonsPanel.Visibility = Visibility.Visible;
        }

        private void SaveLoginButton_Click(object sender, RoutedEventArgs e)
        {
            SaveLoginChanges();
            LoginButtonsPanel.Visibility = Visibility.Collapsed;
        }

        private void CancelLoginButton_Click(object sender, RoutedEventArgs e)
        {
            LoginTextBox.Text = _originalLogin;
            LoginTextBox.IsReadOnly = true;
            LoginButtonsPanel.Visibility = Visibility.Collapsed;
        }

        private void SaveLoginChanges()
        {
            string newLogin = LoginTextBox.Text.Trim();

            if (string.IsNullOrEmpty(newLogin))
            {
                MessageBox.Show("Логин не может быть пустым.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (newLogin == _originalLogin)
            {
                LoginTextBox.IsReadOnly = true;
                return;
            }

            try
            {
                using (var db = new TouristRoutesEntities())
                {
                    var existingUser = db.Users.AsNoTracking()
                        .FirstOrDefault(u => u.UserName == newLogin && u.IdUser != _userId);

                    if (existingUser != null)
                    {
                        MessageBox.Show("Пользователь с таким логином уже существует!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    var user = db.Users.FirstOrDefault(u => u.IdUser == _userId);
                    if (user != null)
                    {
                        user.UserName = newLogin;
                        db.SaveChanges();
                        _originalLogin = newLogin;

                        if (_adminPage != null)
                        {
                            _adminPage.UpdateUserInfo(newLogin, StatusTextBox.Text);
                        }
                        else
                        {
                            _travelerPage?.UpdateUserInfo(newLogin, StatusTextBox.Text);
                        }

                        MessageBox.Show("Логин успешно изменен.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при изменении логина: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoginTextBox.IsReadOnly = true;
            }
        }

        private void EditStatusButton_Click(object sender, RoutedEventArgs e)
        {
            StatusTextBox.IsReadOnly = false;
            StatusTextBox.Focus();
            StatusTextBox.CaretIndex = StatusTextBox.Text.Length;
            StatusButtonsPanel.Visibility = Visibility.Visible;
        }

        private void SaveStatusButton_Click(object sender, RoutedEventArgs e)
        {
            SaveStatusChanges();
            StatusButtonsPanel.Visibility = Visibility.Collapsed;
        }

        private void CancelStatusButton_Click(object sender, RoutedEventArgs e)
        {
            StatusTextBox.Text = _originalStatus;
            StatusTextBox.IsReadOnly = true;
            StatusButtonsPanel.Visibility = Visibility.Collapsed;
        }

        private void SaveStatusChanges()
        {
            try
            {
                using (var db = new TouristRoutesEntities())
                {
                    var user = db.Users.FirstOrDefault(u => u.IdUser == _userId);
                    if (user != null)
                    {
                        user.ProfileBio = StatusTextBox.Text;
                        db.SaveChanges();
                        _originalStatus = StatusTextBox.Text;

                        if (_adminPage != null)
                        {
                            _adminPage.UpdateUserInfo(LoginTextBox.Text, StatusTextBox.Text);
                        }
                        else
                        {
                            _travelerPage?.UpdateUserInfo(LoginTextBox.Text, StatusTextBox.Text);
                        }

                        MessageBox.Show("Описание успешно изменено.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при изменении описания: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                StatusTextBox.IsReadOnly = true;
            }
        }

        private void StatusTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (StatusTextBox.Text.Length > 30)
            {
                StatusTextBox.Text = StatusTextBox.Text.Substring(0, 30);
                StatusTextBox.CaretIndex = StatusTextBox.Text.Length;
            }
        }

        private void DeleteProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Вы уверены, что хотите удалить профиль? Это действие нельзя отменить.",
                "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                using (var db = new TouristRoutesEntities())
                {
                    var user = db.Users
                        .Include(u => u.Favorites)
                        .Include(u => u.Routes.Select(r => r.Photos))
                        .Include(u => u.Routes.Select(r => r.Favorites))
                        .Include(u => u.TravelEvents)
                        .Include(u => u.TravelParticipants)
                        .FirstOrDefault(u => u.IdUser == _userId);

                    if (user != null)
                    {
                        if (!string.IsNullOrEmpty(user.ProfilePhoto))
                        {
                            _ = CloudStorage.DeleteFileAsync(user.ProfilePhoto);
                        }

                        foreach (var route in user.Routes)
                        {
                            foreach (var photo in route.Photos.Where(p => !string.IsNullOrEmpty(p.Photo)))
                            {
                                _ = CloudStorage.DeleteFileAsync(photo.Photo);
                            }
                        }

                        db.TravelEvents.RemoveRange(user.TravelEvents);
                        db.TravelParticipants.RemoveRange(user.TravelParticipants);

                        foreach (var route in user.Routes.ToList())
                        {
                            db.Favorites.RemoveRange(route.Favorites);
                            db.Photos.RemoveRange(route.Photos);
                            db.Routes.Remove(route);
                        }

                        db.Favorites.RemoveRange(user.Favorites);
                        db.Users.Remove(user);
                        db.SaveChanges();

                        MessageBox.Show("Профиль успешно удален.",
                            "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                        AutorizWindow mainWindow = new AutorizWindow();
                        Application.Current.MainWindow = mainWindow;
                        mainWindow.Show();

                        Window.GetWindow(this)?.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении профиля: {ex.Message}\n\n" +
                    "Подробности см. в логах.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine($"Ошибка удаления профиля: {ex}");
            }
        }
    }
}
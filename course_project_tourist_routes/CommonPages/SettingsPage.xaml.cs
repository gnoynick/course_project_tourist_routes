using course_project_tourist_routes.AdminPages;
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

namespace course_project_tourist_routes
{
    public partial class SettingsPage : Page
    {
        private string _originalLogin;
        private string _originalStatus;
        private readonly int _userId;
        private AdminPage _adminPage;
        private TravelerPage _travelerPage;
        private string _newImagePath;
        ImageSource PreviousAvatar;
        BitmapImage NewAvatar;
        string NewAvatarPath;

        public SettingsPage(int userId, string login, string status, AdminPage adminPage, Users user)
    : this(userId, login, status, user)
        {
            _adminPage = adminPage;
        }

        public SettingsPage(int userId, string login, string status, TravelerPage travelerPage, Users user)
            : this(userId, login, status, user)
        {
            _travelerPage = travelerPage;
        }

        private async void InitializeProfilePhotoAsync(string fileId)
        {
            try
            {
                PhotoLoadingProgress.Visibility = Visibility.Visible;
                ProfilePhoto.Visibility = Visibility.Collapsed;

                await Task.Run(() =>
                {
                    CloudStorage.DownloadCurrentUserPhoto(fileId);
                });

                var bitmapImage = CloudStorage.GetBitmapImage(CloudStorage.CurrentUserPhotoPath, true);
                var imageBrush = new ImageBrush { ImageSource = bitmapImage };

                SharedResources.ProfileImageBrush = imageBrush;
                Resources["imagebrush"] = imageBrush;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки фото профиля: {ex.Message}");
            }
            finally
            {
                PhotoLoadingProgress.Visibility = Visibility.Collapsed;
                ProfilePhoto.Visibility = Visibility.Visible;
            }
        }

        private SettingsPage(int userId, string login, string status, Users user)
        {
            Resources.Add("User", user);
            InitializeComponent();

            InitializeProfilePhotoAsync(user.ProfilePhoto);

            _userId = userId;
            _originalLogin = login;
            _originalStatus = status;
            LoginTextBox.Text = login;
            StatusTextBox.Text = status;
        }

        private async void ChangePhotoButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "JPEG files (*.jpg;*.jpeg;) | *.jpg;*.jpeg; | PNG files (*.png) | *.png",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    NewAvatarPath = openFileDialog.FileName;
                    NewAvatar = await Task.Run(() => CloudStorage.GetBitmapImage(NewAvatarPath));

                    var imageBrush = (ImageBrush)Resources["imagebrush"];
                    PreviousAvatar = imageBrush.ImageSource;
                    imageBrush.ImageSource = NewAvatar;

                    CancelPhotoStackPanel.Visibility = Visibility.Visible;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при загрузке изображения: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void SavePhotoButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(NewAvatarPath))
                {
                    MessageBox.Show("Не выбрано новое фото", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                PhotoLoadingProgress.Visibility = Visibility.Visible;
                ProfilePhoto.Visibility = Visibility.Collapsed;
                SavePhotoButton.IsEnabled = false;
                CancelPhotoButton.IsEnabled = false;

                await Task.Run(() => File.Copy(NewAvatarPath, CloudStorage.CurrentUserPhotoPath, true));

                using (var context = new TouristRoutesEntities())
                {
                    var user = (Users)Resources["User"];
                    string oldFileId = user.ProfilePhoto;

                    string newFileId = await Task.Run(() =>
                        CloudStorage.UploadProfilePhoto(
                            CloudStorage.CurrentUserPhotoPath,
                            $"ID_{user.IdUser}",
                            null));

                    if (string.IsNullOrEmpty(newFileId))
                    {
                        MessageBox.Show("Не удалось загрузить фото", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    var dbUser = context.Users.SingleOrDefault(u => u.IdUser == user.IdUser);
                    if (dbUser != null)
                    {
                        string previousFileId = dbUser.ProfilePhoto;
                        dbUser.ProfilePhoto = newFileId;
                        await context.SaveChangesAsync();

                        if (!string.IsNullOrEmpty(previousFileId))
                        {
                            await Task.Run(() => CloudStorage.DeleteFile(previousFileId));
                        }
                    }

                    await UpdatePhoto(newFileId);

                    MessageBox.Show("Фото профиля успешно изменено!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при изменении фото: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                try { File.Delete(CloudStorage.CurrentUserPhotoPath); } catch { }
                CancelPhotoStackPanel.Visibility = Visibility.Collapsed;
                SavePhotoButton.IsEnabled = true;
                CancelPhotoButton.IsEnabled = true;
                PhotoLoadingProgress.Visibility = Visibility.Collapsed;
                ProfilePhoto.Visibility = Visibility.Visible;
            }
        }

        private async Task UpdatePhoto(string fileId)
        {
            try
            {
                await Task.Run(() => CloudStorage.DownloadCurrentUserPhoto(fileId));
                var newImage = await Task.Run(() => CloudStorage.GetBitmapImage(CloudStorage.CurrentUserPhotoPath, true));

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var imageBrush = (ImageBrush)Resources["imagebrush"];
                    imageBrush.ImageSource = newImage;
                    SharedResources.ProfileImageBrush.ImageSource = newImage;

                    if (_adminPage != null)
                        _adminPage.Resources["imagebrush"] = SharedResources.ProfileImageBrush;
                    else if (_travelerPage != null)
                        _travelerPage.Resources["imagebrush"] = SharedResources.ProfileImageBrush;
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при обновлении фото: {ex.Message}");
            }
        }

        private void CancelPhotoButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (PreviousAvatar != null)
                {
                    var imageBrush = (ImageBrush)Resources["imagebrush"];
                    imageBrush.ImageSource = PreviousAvatar;
                    SharedResources.ProfileImageBrush.ImageSource = PreviousAvatar;
                }

                CancelPhotoStackPanel.Visibility = Visibility.Collapsed;
                _newImagePath = null;
                NewAvatar = null;
                NewAvatarPath = null;

                if (File.Exists(CloudStorage.CurrentUserPhotoPath))
                {
                    File.Delete(CloudStorage.CurrentUserPhotoPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при отмене изменения фото: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
                        user.ProfileStatus = StatusTextBox.Text;
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

                        MessageBox.Show("Статус успешно изменен.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при изменении статуса: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
            MessageBoxResult result = MessageBox.Show("Вы уверены, что хотите удалить профиль? Это действие нельзя отменить.", "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using (var db = new TouristRoutesEntities())
                    {
                        var user = db.Users.FirstOrDefault(u => u.IdUser == _userId);
                        if (user != null)
                        {
                            db.Users.Remove(user);
                            db.SaveChanges();
                            MessageBox.Show("Профиль успешно удален.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                            Window.GetWindow(this).Close();
                            AutorizWindow mainWindow = new AutorizWindow();
                            mainWindow.Show();
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка при удалении профиля: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
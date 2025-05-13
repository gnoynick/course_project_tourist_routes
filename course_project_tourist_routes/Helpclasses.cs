using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Net.Http;
using System.Threading;

namespace course_project_tourist_routes
{
    public static class CloudStorage
    {
        private const string ProfilePhotosDirectoryID = "1QOS_QWmIY9LF3FyDfLSIkh6NcVkYIfoC";
        private const string RoutePhotosDirectoryID = "1to-5jvJKdutPUfnP12DSlgtR_pEKZDIK";

        public static string AppDataPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "TouristRoutes");
        public static string ProfilePhotosDirectoryPath => Path.Combine(AppDataPath, "profile_photos");
        public static string CurrentUserPhotoPath => Path.Combine(AppDataPath, "current_profile_photo.jpg");
        public static string RoutePhotosDirectoryPath => Path.Combine(AppDataPath, "route_photos");
        public static string RoutePhotosTempPath => Path.Combine(AppDataPath, "route_photo_temp.jpg");
        public static string ResourcesPath => Path.Combine(AppDataPath, "Resources");

        private static async Task<string> GetServiceAccountKeyFromGistAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    var gistUrl = "https://gist.githubusercontent.com/gnoynick/027a30f0844a593b3494a27f7097ed38/raw/course-project-457018-ee7c1288fe18.json";

                    httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

                    var response = await httpClient.GetAsync(gistUrl, cancellationToken);
                    response.EnsureSuccessStatusCode();

                    return await response.Content.ReadAsStringAsync();
                }
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"Не удалось загрузить ключ сервисного аккаунта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                });
                throw;
            }
        }

        private static async Task<DriveService> GetDriveServiceAsync(CancellationToken cancellationToken = default)
        {
            var jsonKey = await GetServiceAccountKeyFromGistAsync(cancellationToken);

            var credential = GoogleCredential.FromJson(jsonKey)
                .CreateScoped(DriveService.ScopeConstants.Drive);

            return new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                HttpClientTimeout = TimeSpan.FromSeconds(30)
            });
        }

        public static async Task DeleteFileAsync(string fileId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(fileId)) return;

            try
            {
                var driveService = await GetDriveServiceAsync(cancellationToken);

                try
                {
                    var getRequest = driveService.Files.Get(fileId);
                    await getRequest.ExecuteAsync(cancellationToken);
                }
                catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
                {
                    return;
                }

                await driveService.Files.Delete(fileId).ExecuteAsync(cancellationToken);
            }
            catch (Google.GoogleApiException ex)
            {
                string errorMessage = ex.Error?.Message ?? ex.Message;
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"Ошибка при удалении файла: {errorMessage}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"Общая ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        public static async Task<BitmapImage> GetBitmapImageAsync(string imagePath, bool cache = false, CancellationToken cancellationToken = default)
        {
            try
            {
                return await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.UriSource = new Uri(imagePath, UriKind.RelativeOrAbsolute);
                    image.CacheOption = cache ? BitmapCacheOption.OnLoad : BitmapCacheOption.None;
                    image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    image.EndInit();

                    if (image.CanFreeze)
                    {
                        image.Freeze();
                    }

                    return image;
                }, System.Windows.Threading.DispatcherPriority.Background, cancellationToken);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading image from {imagePath}: {ex.Message}");
                return new BitmapImage();
            }
        }

        private static async Task ShowErrorAsync(string message)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        public static async Task EnsureDefaultProfilePhotoExistsAsync(bool forceReplace = false, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!Directory.Exists(ResourcesPath))
                {
                    Directory.CreateDirectory(ResourcesPath);
                }

                string defaultPhotoPath = Path.Combine(ResourcesPath, "profile_photo.jpg");

                if (!File.Exists(defaultPhotoPath))
                {
                    var defaultUri = new Uri("pack://application:,,,/Resources/profile_photo.jpg", UriKind.RelativeOrAbsolute);
                    var resourceInfo = Application.GetResourceStream(defaultUri);

                    if (resourceInfo != null)
                    {
                        using (var defaultStream = resourceInfo.Stream)
                        using (var fileStream = new FileStream(defaultPhotoPath, FileMode.Create))
                        {
                            await defaultStream.CopyToAsync(fileStream);
                        }
                    }
                }

                if (forceReplace || !File.Exists(CurrentUserPhotoPath))
                {
                    File.Copy(defaultPhotoPath, CurrentUserPhotoPath, true);
                }
            }
            catch (Exception ex)
            {
                await ShowErrorAsync($"Не удалось установить фото по умолчанию: {ex.Message}");
            }
        }

        public static async Task<string> UploadProfilePhotoAsync(string filePath, string fileName, string oldFileId = null, CancellationToken cancellationToken = default)
        {
            return await UploadPhotoAsync(filePath, fileName, oldFileId, ProfilePhotosDirectoryID, cancellationToken);
        }

        public static async Task DownloadProfilePhotoAsync(string fileId, int userId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(fileId)) return;

            try
            {
                if (!Directory.Exists(ProfilePhotosDirectoryPath))
                {
                    Directory.CreateDirectory(ProfilePhotosDirectoryPath);
                }

                string userPhotoPath = Path.Combine(ProfilePhotosDirectoryPath, $"user_{userId}.jpg");

                var driveService = await GetDriveServiceAsync(cancellationToken);
                var request = driveService.Files.Get(fileId);

                using (var fileStream = new FileStream(userPhotoPath, FileMode.Create, FileAccess.Write))
                {
                    await request.DownloadAsync(fileStream, cancellationToken);
                }

                Debug.WriteLine($"Фото профиля успешно загружено: {userPhotoPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки фото профиля: {ex.Message}");
                throw;
            }
        }

        public static async Task DownloadCurrentUserPhotoAsync(string fileId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrEmpty(fileId))
                {
                    await EnsureDefaultProfilePhotoExistsAsync(true, cancellationToken);
                    return;
                }

                if (!Directory.Exists(AppDataPath))
                {
                    Directory.CreateDirectory(AppDataPath);
                }

                var driveService = await GetDriveServiceAsync(cancellationToken);
                var request = driveService.Files.Get(fileId);

                using (var fileStream = new FileStream(CurrentUserPhotoPath, FileMode.Create))
                {
                    await request.DownloadAsync(fileStream, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при загрузке фото текущего пользователя: {ex.Message}");
                await EnsureDefaultProfilePhotoExistsAsync(true, cancellationToken);
            }
        }

        public static async Task<string> UploadRoutePhotoAsync(string filePath, string fileName, string oldFileId = null, CancellationToken cancellationToken = default)
        {
            return await UploadPhotoAsync(filePath, fileName, oldFileId, RoutePhotosDirectoryID, cancellationToken);
        }

        public static async Task DownloadRoutePhotoAsync(string fileId, string fileName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(fileId)) return;

            try
            {
                if (!Directory.Exists(RoutePhotosDirectoryPath))
                {
                    Directory.CreateDirectory(RoutePhotosDirectoryPath);
                }

                string savePath = Path.Combine(RoutePhotosDirectoryPath, fileName);

                var driveService = await GetDriveServiceAsync(cancellationToken);
                var request = driveService.Files.Get(fileId);

                using (var stream = new FileStream(savePath, FileMode.Create))
                {
                    await request.DownloadAsync(stream, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при загрузке фото маршрута: {ex.Message}");
            }
        }

        public static async Task<string> CreateRouteFolderAsync(string routeName, CancellationToken cancellationToken = default)
        {
            try
            {
                var driveService = await GetDriveServiceAsync(cancellationToken);

                var folderMetadata = new Google.Apis.Drive.v3.Data.File()
                {
                    Name = routeName,
                    MimeType = "application/vnd.google-apps.folder",
                    Parents = new List<string> { RoutePhotosDirectoryID }
                };

                var request = driveService.Files.Create(folderMetadata);
                request.Fields = "id";
                var folder = await request.ExecuteAsync(cancellationToken);

                return folder.Id;
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"Ошибка при создании папки для маршрута: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                });
                return null;
            }
        }

        public static async Task ClearRoutePhotosDirectoryAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await Task.Run(() =>
                {
                    if (Directory.Exists(RoutePhotosDirectoryPath))
                    {
                        Directory.Delete(RoutePhotosDirectoryPath, true);
                        Directory.CreateDirectory(RoutePhotosDirectoryPath);
                    }
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при очистке папки route_photos: {ex.Message}");
            }
        }

        public static async Task ClearProfilePhotosDirectoryAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await Task.Run(() =>
                {
                    if (Directory.Exists(ProfilePhotosDirectoryPath))
                    {
                        Directory.Delete(ProfilePhotosDirectoryPath, true);
                        Directory.CreateDirectory(ProfilePhotosDirectoryPath);
                    }
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при очистке папки profile_photos: {ex.Message}");
            }
        }

        private static async Task<string> UploadPhotoAsync(string filePath, string fileName, string oldFileId, string parentDirectoryId, CancellationToken cancellationToken = default)
        {
            try
            {
                var driveService = await GetDriveServiceAsync(cancellationToken);

                var fileMetadata = new Google.Apis.Drive.v3.Data.File()
                {
                    Name = $"{fileName}.jpg",
                    Parents = new List<string> { parentDirectoryId },
                    MimeType = "image/jpeg"
                };

                string uploadedFileId;
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    var request = driveService.Files.Create(fileMetadata, stream, fileMetadata.MimeType);
                    request.Fields = "id";
                    var results = await request.UploadAsync(cancellationToken);

                    if (results.Status == Google.Apis.Upload.UploadStatus.Failed)
                    {
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            MessageBox.Show("Не удалось загрузить файл!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                        return null;
                    }
                    uploadedFileId = request.ResponseBody.Id;
                }

                if (!string.IsNullOrEmpty(oldFileId))
                {
                    await DeleteFileAsync(oldFileId, cancellationToken);
                }

                return uploadedFileId;
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"Ошибка при загрузке фото: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                });
                return null;
            }
        }
    }

    public static class SharedResources
    {
        public static ImageBrush ProfileImageBrush { get; set; } = new ImageBrush();
    }
}

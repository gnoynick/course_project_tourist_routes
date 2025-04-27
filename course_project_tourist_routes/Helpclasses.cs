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

namespace course_project_tourist_routes
{
    public static class CloudStorage
    {
        private const string ProfilePhotosDirectoryID = "1QOS_QWmIY9LF3FyDfLSIkh6NcVkYIfoC";
        private const string RoutePhotosDirectoryID = "1to-5jvJKdutPUfnP12DSlgtR_pEKZDIK";

        public static string ProfilePhotosDirectoryPath = Path.Combine(
            Directory.GetParent(Environment.CurrentDirectory).Parent.FullName,
            "temp",
            "profile_photos");

        public static string CurrentUserPhotoPath = Path.Combine(
            Directory.GetParent(Environment.CurrentDirectory).Parent.FullName,
            "temp",
            "current_profile_photo.jpg");

        public static string RoutePhotosDirectoryPath = Path.Combine(Directory.GetParent(Environment.CurrentDirectory).Parent.FullName, "temp", "route_photos");
        public static string RoutePhotosTempPath = Path.Combine(Directory.GetParent(Environment.CurrentDirectory).Parent.FullName, "temp", "route_photo_temp.jpg");

        private static async Task<string> GetServiceAccountKeyFromGist()
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    var gistUrl = "https://gist.githubusercontent.com/gnoynick/027a30f0844a593b3494a27f7097ed38/raw/course-project-457018-ee7c1288fe18.json";

                    httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

                    var response = await httpClient.GetAsync(gistUrl);
                    response.EnsureSuccessStatusCode();

                    return await response.Content.ReadAsStringAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось загрузить ключ сервисного аккаунта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        private static DriveService GetDriveService()
        {
            var jsonKey = GetServiceAccountKeyFromGist().GetAwaiter().GetResult();

            var credential = GoogleCredential.FromJson(jsonKey)
                .CreateScoped(DriveService.ScopeConstants.Drive);

            return new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
            });
        }

        public static void DeleteFile(string fileId)
        {
            if (string.IsNullOrEmpty(fileId)) return;

            try
            {
                var driveService = GetDriveService();

                try
                {
                    var getRequest = driveService.Files.Get(fileId);
                    getRequest.Execute();
                }
                catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
                {
                    return;
                }

                driveService.Files.Delete(fileId).Execute();
            }
            catch (Google.GoogleApiException ex)
            {
                string errorMessage = ex.Error?.Message ?? ex.Message;
                MessageBox.Show($"Ошибка при удалении файла: {errorMessage}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Общая ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static BitmapImage GetBitmapImage(string imagePath, bool cache = false)
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            {
                return new BitmapImage();
            }

            var bitmapImage = new BitmapImage();

            try
            {
                bitmapImage.BeginInit();
                bitmapImage.UriSource = new Uri(imagePath, UriKind.RelativeOrAbsolute);
                bitmapImage.CacheOption = cache ? BitmapCacheOption.OnLoad : BitmapCacheOption.None;
                bitmapImage.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bitmapImage.EndInit();

                if (bitmapImage.CanFreeze)
                {
                    bitmapImage.Freeze();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки изображения: {ex.Message}");
                return new BitmapImage();
            }

            return bitmapImage;
        }

        public static void EnsureDefaultProfilePhotoExists(bool forceReplace = false)
        {
            string defaultPhotoPath = Path.Combine(
                Directory.GetParent(Environment.CurrentDirectory).Parent.FullName,
                "Resources",
                "profile_photo.jpg");

            if (forceReplace || !File.Exists(CurrentUserPhotoPath))
            {
                if (File.Exists(defaultPhotoPath))
                {
                    File.Copy(defaultPhotoPath, CurrentUserPhotoPath, true);
                }
                else
                {
                    MessageBox.Show("Заглушка для фото профиля не найдена!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public static string UploadProfilePhoto(string filePath, string fileName, string oldFileId = null)
        {
            return UploadPhoto(filePath, fileName, oldFileId, ProfilePhotosDirectoryID);
        }

        public static void DownloadProfilePhoto(string fileId, int userId)
        {
            if (string.IsNullOrEmpty(fileId)) return;

            string userPhotoPath = Path.Combine(
                ProfilePhotosDirectoryPath,
                $"user_{userId}.jpg");

            try
            {
                Directory.CreateDirectory(ProfilePhotosDirectoryPath);

                using (var stream = new FileStream(
                    userPhotoPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.ReadWrite))
                {
                    var driveService = GetDriveService();
                    var request = driveService.Files.Get(fileId);
                    request.Download(stream);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки фото: {ex.Message}");
                throw;
            }
        }

        public static void DownloadCurrentUserPhoto(string fileId)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CurrentUserPhotoPath));

            if (string.IsNullOrEmpty(fileId))
            {
                EnsureDefaultProfilePhotoExists(true);
                return;
            }

            try
            {
                var driveService = GetDriveService();
                var request = driveService.Files.Get(fileId);

                using (var stream = new FileStream(CurrentUserPhotoPath, FileMode.Create))
                {
                    request.Download(stream);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при загрузке фото текущего пользователя: {ex.Message}");
                EnsureDefaultProfilePhotoExists(true);
            }
        }


        public static string UploadRoutePhoto(string filePath, string fileName, string oldFileId = null)
        {
            return UploadPhoto(filePath, fileName, oldFileId, RoutePhotosDirectoryID);
        }

        public static void DownloadRoutePhoto(string fileId, string savePath)
        {
            if (string.IsNullOrEmpty(fileId)) return;

            try
            {
                var driveService = GetDriveService();
                var request = driveService.Files.Get(fileId);

                Directory.CreateDirectory(Path.GetDirectoryName(savePath));

                using (var stream = new FileStream(savePath, FileMode.Create))
                {
                    request.Download(stream);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при загрузке фото из Google Drive: {ex.Message}");
            }
        }

        public static async Task<string> CreateRouteFolder(string routeName)
        {
            try
            {
                var driveService = GetDriveService();

                var folderMetadata = new Google.Apis.Drive.v3.Data.File()
                {
                    Name = routeName,
                    MimeType = "application/vnd.google-apps.folder",
                    Parents = new List<string> { RoutePhotosDirectoryID }
                };

                var request = driveService.Files.Create(folderMetadata);
                request.Fields = "id";
                var folder = await request.ExecuteAsync();

                return folder.Id;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании папки для маршрута: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        public static void ClearRoutePhotosDirectory()
        {
            try
            {
                if (Directory.Exists(RoutePhotosDirectoryPath))
                {
                    Directory.Delete(RoutePhotosDirectoryPath, true);
                    Directory.CreateDirectory(RoutePhotosDirectoryPath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при очистке папки route_photos: {ex.Message}");
            }
        }

        public static void ClearProfilePhotosDirectory()
        {
            try
            {
                if (Directory.Exists(ProfilePhotosDirectoryPath))
                {
                    Directory.Delete(ProfilePhotosDirectoryPath, true);
                    Directory.CreateDirectory(ProfilePhotosDirectoryPath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при очистке папки profile_photos: {ex.Message}");
            }
        }

        private static string UploadPhoto(string filePath, string fileName, string oldFileId, string parentDirectoryId)
        {
            try
            {
                var driveService = GetDriveService();

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
                    var results = request.Upload();

                    if (results.Status == Google.Apis.Upload.UploadStatus.Failed)
                    {
                        MessageBox.Show("Не удалось загрузить файл!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return null;
                    }
                    uploadedFileId = request.ResponseBody.Id;
                }

                if (!string.IsNullOrEmpty(oldFileId))
                {
                    DeleteFile(oldFileId);
                }

                return uploadedFileId;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке фото: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }
    }

    public static class SharedResources
    {
        public static ImageBrush ProfileImageBrush { get; set; } = new ImageBrush();
    }
}
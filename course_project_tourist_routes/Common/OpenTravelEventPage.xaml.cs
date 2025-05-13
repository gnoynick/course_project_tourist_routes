using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System.Collections.Generic;
using System.Data.Entity;
using System.Threading.Tasks;
using course_project_tourist_routes.Traveler;

namespace course_project_tourist_routes.Common
{
    public partial class OpenTravelEventPage : Page
    {
        private readonly int _userId;
        private readonly int _eventId;
        private TravelEvents _currentEvent;
        private Dictionary<int, ImageBrush> _userPhotosCache = new Dictionary<int, ImageBrush>();
        private bool _isOrganizerOrAdmin = false;
        private bool _isParticipant = false;

        public OpenTravelEventPage(int userId, int eventId)
        {
            InitializeComponent();
            _userId = userId;
            _eventId = eventId;

            InitializeJoinButton();
            LoadEventData();
        }

        private void ShowLoadingState(bool isLoading)
        {
            Dispatcher.Invoke(() =>
            {
                if (isLoading)
                {
                    ContentGrid.Visibility = Visibility.Collapsed;
                    LoadingGrid.Visibility = Visibility.Visible;
                }
                else
                {
                    ContentGrid.Visibility = Visibility.Visible;
                    LoadingGrid.Visibility = Visibility.Collapsed;
                }
            });
        }

        private void InitializeJoinButton()
        {
            try
            {
                using (var context = new TouristRoutesEntities())
                {
                    var currentUser = context.Users.Find(_userId);
                    bool isAdmin = currentUser?.IdRole == 1;
                    bool isOrganizer = context.TravelEvents
                        .Any(e => e.IdEvent == _eventId && e.IdUser == _userId);

                    JoinButton.Visibility = (isAdmin || isOrganizer)
                        ? Visibility.Collapsed
                        : Visibility.Visible;

                    JoinButton.Content = "Загрузка...";
                    JoinButton.IsEnabled = false;
                }
            }
            catch
            {
                JoinButton.Visibility = Visibility.Collapsed;
            }
        }

        private async void LoadEventData()
        {
            try
            {
                ShowLoadingState(true);

                using (var context = new TouristRoutesEntities())
                {
                    _currentEvent = await context.TravelEvents
                        .Include(e => e.Routes)
                        .Include(e => e.Routes.Categories)
                        .Include(e => e.Routes.RoutePoints)
                        .Include(e => e.Users)
                        .Include(e => e.Users.Roles)
                        .FirstOrDefaultAsync(e => e.IdEvent == _eventId);

                    if (_currentEvent == null)
                    {
                        MessageBox.Show("Путешествие не найдено", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        NavigationService.GoBack();
                        return;
                    }

                    var currentUser = await context.Users.FindAsync(_userId);
                    _isOrganizerOrAdmin = _currentEvent.IdUser == _userId || currentUser?.IdRole == 1;

                    CompleteEventButton.Visibility = _currentEvent.IdUser == _userId &&
                                                  (_currentEvent.StatusEvent == "Запланировано" ||
                                                   _currentEvent.StatusEvent == "Активно")
                        ? Visibility.Visible
                        : Visibility.Collapsed;

                    _isParticipant = await context.TravelParticipants
                        .AnyAsync(p => p.IdEvent == _eventId && p.IdUser == _userId);

                    if (!_isOrganizerOrAdmin)
                    {
                        var eventToUpdate = await context.TravelEvents.FindAsync(_eventId);
                        if (eventToUpdate != null)
                        {
                            eventToUpdate.ViewsCount = (eventToUpdate.ViewsCount ?? 0) + 1;
                            await context.SaveChangesAsync();
                        }
                    }

                    TitleTextBlock.Text = _currentEvent.TitleEvent;
                    ViewsTextBlock.Text = $"Просмотры: {_currentEvent.ViewsCount ?? 0}";
                    DatesTextBlock.Text = $"{_currentEvent.StartDate:dd.MM.yyyy} - {_currentEvent.EndDate:dd.MM.yyyy}";
                    StatusTextBlock.Text = _currentEvent.StatusEvent;
                    DescriptionTextBlock.Text = _currentEvent.DescriptionEvent;

                    if (_currentEvent.Routes != null)
                    {
                        RouteTitleTextBlock.Text = _currentEvent.Routes.TitleRoute.Length > 30 ?
                            _currentEvent.Routes.TitleRoute.Substring(0, 30) + "..." :
                            _currentEvent.Routes.TitleRoute;
                        RouteDescriptionTextBlock.Text = _currentEvent.Routes.DescriptionRoute.Length > 150 ?
                            _currentEvent.Routes.DescriptionRoute.Substring(0, 150) + "..." :
                            _currentEvent.Routes.DescriptionRoute;
                        RouteCategoryTextBlock.Text = _currentEvent.Routes.Categories?.NameCategory != null
                            ? (_currentEvent.Routes.Categories.NameCategory.Length > 20
                            ? _currentEvent.Routes.Categories.NameCategory.Substring(0, 17) + "..."
                            : _currentEvent.Routes.Categories.NameCategory)
                            : "Не указана";

                        var countries = _currentEvent.Routes.RoutePoints
                            .Select(p => p.Country)
                            .Distinct()
                            .Where(c => !string.IsNullOrEmpty(c))
                            .ToList();

                        string countriesText = countries.Any() ? string.Join(", ", countries) : "Не указаны";
                        RouteCountriesTextBlock.Text = countriesText.Length > 20
                            ? countriesText.Substring(0, 17) + " ..."
                            : countriesText;

                        var cities = _currentEvent.Routes.RoutePoints
                            .Select(p => p.City)
                            .Distinct()
                            .Where(c => !string.IsNullOrEmpty(c))
                            .ToList();

                        string citiesText = cities.Any() ? string.Join(", ", cities) : "Не указаны";
                        RouteCitiesTextBlock.Text = citiesText.Length > 20
                            ? citiesText.Substring(0, 17) + " ..."
                            : citiesText;
                    }

                    bool isOrganizer = _currentEvent.IdUser == _userId;

                    if (_currentEvent.Users != null)
                    {
                        OrganizerNameTextBlock.Text = isOrganizer ? "Вы" : _currentEvent.Users.UserName;
                        OrganizerEmailTextBlock.Text = $"Email: {_currentEvent.Users.Email}";
                    }

                    if (_currentEvent.Routes != null)
                    {
                        await LoadRoutePhoto(_currentEvent.Routes.IdRoute);
                    }

                    if (_currentEvent.Users != null)
                    {
                        await LoadOrganizerPhoto(_currentEvent.Users.ProfilePhoto, (int)_currentEvent.IdUser);
                    }

                    await LoadParticipants();

                    UpdateJoinButtonState();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ShowLoadingState(false);
            }
        }

        private async Task LoadRoutePhoto(int routeId)
        {
            try
            {
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                string routePhotosDir = Path.Combine(appDataPath, "TouristRoutes", "route_photos");
                string photoPath = Path.Combine(routePhotosDir, $"Route_{routeId}_Photo_1.jpg");

                ImageBrush brush;
                if (File.Exists(photoPath))
                {
                    brush = CreateImageBrush(photoPath);
                }
                else
                {
                    using (var context = new TouristRoutesEntities())
                    {
                        var photo = await context.Photos
                            .Where(p => p.IdRoute == routeId)
                            .OrderBy(p => p.IdPhoto)
                            .FirstOrDefaultAsync();

                        if (photo != null && !string.IsNullOrEmpty(photo.Photo))
                        {
                            Directory.CreateDirectory(routePhotosDir);
                            await CloudStorage.DownloadRoutePhotoAsync(photo.Photo, $"Route_{routeId}_Photo_1.jpg");
                            brush = CreateImageBrush(photoPath);
                        }
                        else
                        {
                            brush = new ImageBrush
                            {
                                ImageSource = new BitmapImage(new Uri("pack://application:,,,/Resources/route_placeholder.jpg")),
                            };
                        }
                    }
                }

                RoutePhotoButton.Background = brush;
            }
            catch
            {
                RoutePhotoButton.Background = new ImageBrush
                {
                    ImageSource = new BitmapImage(new Uri("pack://application:,,,/Resources/route_placeholder.jpg")),
                };
            }
        }

        private async Task LoadOrganizerPhoto(string photoId, int userId)
        {
            try
            {
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                string profilePhotosDir = Path.Combine(appDataPath, "TouristRoutes", "profile_photos");
                string photoPath = Path.Combine(profilePhotosDir, $"user_{userId}.jpg");

                ImageBrush brush;
                if (File.Exists(photoPath))
                {
                    brush = CreateImageBrush(photoPath);
                }
                else if (!string.IsNullOrEmpty(photoId))
                {
                    Directory.CreateDirectory(profilePhotosDir);
                    await CloudStorage.DownloadProfilePhotoAsync(photoId, userId);
                    brush = CreateImageBrush(photoPath);
                }
                else
                {
                    brush = new ImageBrush
                    {
                        ImageSource = new BitmapImage(new Uri("pack://application:,,,/Resources/profile_photo.jpg")),
                    };
                }

                OrganizerPhotoButton.Background = brush;
            }
            catch
            {
                OrganizerPhotoButton.Background = new ImageBrush
                {
                    ImageSource = new BitmapImage(new Uri("pack://application:,,,/Resources/profile_photo.jpg")),
                };
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
                return new ImageBrush
                {
                    ImageSource = new BitmapImage(new Uri("pack://application:,,,/Resources/profile_photo.jpg")),
                };
            }
        }

        private async Task LoadParticipants()
        {
            try
            {
                ShowLoadingState(true);

                using (var context = new TouristRoutesEntities())
                {
                    var participants = await context.TravelParticipants
                        .Include(p => p.Users)
                        .Where(p => p.IdEvent == _eventId)
                        .OrderByDescending(p => p.IdUser == _currentEvent.IdUser)
                        .ThenBy(p => p.DateJoinParticipant)
                        .AsNoTracking()
                        .ToListAsync()
                        .ConfigureAwait(false);

                    int maxParticipants = _currentEvent.MaxParticipants;

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        ParticipantsCountTextBlock.Text = $"({participants.Count} из {maxParticipants})";
                    });

                    var participantsList = new List<dynamic>();
                    foreach (var p in participants)
                    {
                        var photoBrush = await GetUserPhotoBrushAsync(p.Users.ProfilePhoto, (int)p.IdUser)
                            .ConfigureAwait(false);

                        participantsList.Add(new
                        {
                            IdUser = p.IdUser,
                            Name = p.IdUser == _currentEvent.IdUser ? $"{p.Users.UserName} (Организатор)" : p.Users.UserName,
                            Email = p.Users.Email,
                            PhotoBrush = photoBrush,
                            JoinDate = p.DateJoinParticipant.ToString("dd.MM.yyyy"),
                            IsOrganizer = _currentEvent.IdUser == _userId
                        });
                    }

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        ParticipantsListView.ItemsSource = participantsList;
                    });
                }
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"Ошибка при загрузке участников: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            finally
            {
                ShowLoadingState(false);
            }
        }

        private async Task<ImageBrush> GetUserPhotoBrushAsync(string photoId, int userId)
        {
            if (_userPhotosCache.TryGetValue(userId, out var cachedBrush))
                return cachedBrush;

            var brush = await LoadUserPhoto(photoId, userId).ConfigureAwait(false);

            lock (_userPhotosCache)
            {
                if (!_userPhotosCache.ContainsKey(userId))
                    _userPhotosCache[userId] = brush;
            }

            return brush;
        }

        private async void RemoveParticipantButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is int participantId))
                return;

            if (MessageBox.Show("Вы уверены, что хотите удалить этого участника?",
                "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                using (var context = new TouristRoutesEntities())
                {
                    var participant = await context.TravelParticipants
                        .FirstOrDefaultAsync(p => p.IdEvent == _eventId && p.IdUser == participantId);

                    if (participant != null)
                    {
                        context.TravelParticipants.Remove(participant);
                        await context.SaveChangesAsync();

                        await LoadParticipants();

                        MessageBox.Show("Участник успешно удален", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении участника: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task<ImageBrush> LoadUserPhoto(string photoId, int userId)
        {
            try
            {
                string photoPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    "TouristRoutes", "profile_photos", $"user_{userId}.jpg");

                if (!File.Exists(photoPath))
                {
                    if (!string.IsNullOrEmpty(photoId))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(photoPath));
                        await CloudStorage.DownloadProfilePhotoAsync(photoId, userId)
                            .ConfigureAwait(false);
                    }
                }

                return await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    return File.Exists(photoPath)
                        ? CreateImageBrush(photoPath)
                        : new ImageBrush
                        {
                            ImageSource = new BitmapImage(new Uri("pack://application:,,,/Resources/profile_photo.jpg")),
                            Stretch = Stretch.UniformToFill
                        };
                });
            }
            catch
            {
                return new ImageBrush
                {
                    ImageSource = new BitmapImage(new Uri("pack://application:,,,/Resources/profile_photo.jpg")),
                    Stretch = Stretch.UniformToFill
                };
            }
        }

        private void UpdateJoinButtonState()
        {
            try
            {
                if (_currentEvent == null)
                {
                    JoinButton.Visibility = Visibility.Collapsed;
                    return;
                }

                bool isOrganizerOrAdmin = _currentEvent.IdUser == _userId ||
                                        (JoinButton.Visibility == Visibility.Collapsed);

                if (isOrganizerOrAdmin)
                {
                    JoinButton.Visibility = Visibility.Collapsed;
                    return;
                }

                bool isEventActive = _currentEvent.StatusEvent == "Запланировано" ||
                                   _currentEvent.StatusEvent == "Активно";

                bool hasFreeSlots = true;

                using (var context = new TouristRoutesEntities())
                {
                    int participantsCount = context.TravelParticipants.Count(p => p.IdEvent == _eventId);
                    hasFreeSlots = participantsCount < _currentEvent.MaxParticipants;

                    _isParticipant = context.TravelParticipants
                        .Any(p => p.IdEvent == _eventId && p.IdUser == _userId);
                }

                JoinButton.Content = _isParticipant ? "Покинуть" : "Присоединиться";
                JoinButton.IsEnabled = isEventActive && hasFreeSlots;
            }
            catch
            {
                JoinButton.Visibility = Visibility.Collapsed;
            }
        }

        private async void JoinButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var context = new TouristRoutesEntities())
                {
                    if (_isParticipant)
                    {
                        var participation = await context.TravelParticipants
                            .FirstOrDefaultAsync(p => p.IdEvent == _eventId && p.IdUser == _userId);

                        if (participation != null)
                        {
                            context.TravelParticipants.Remove(participation);
                            await context.SaveChangesAsync();
                            _isParticipant = false;
                            MessageBox.Show("Вы вышли из путешествия", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    else
                    {
                        context.TravelParticipants.Add(new TravelParticipants
                        {
                            IdEvent = _eventId,
                            IdUser = _userId,
                            DateJoinParticipant = DateTime.Now
                        });

                        await context.SaveChangesAsync();
                        _isParticipant = true;
                        MessageBox.Show("Вы успешно присоединились к путешествию", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    }

                    await LoadParticipants();
                    UpdateJoinButtonState();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CompleteEventButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Завершить это путешествие?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    using (var context = new TouristRoutesEntities())
                    {
                        var eventToComplete = await context.TravelEvents.FindAsync(_eventId);
                        if (eventToComplete != null)
                        {
                            eventToComplete.StatusEvent = "Завершено";
                            await context.SaveChangesAsync();
                            _currentEvent.StatusEvent = "Завершено";
                            StatusTextBlock.Text = "Завершено";
                            CompleteEventButton.Visibility = Visibility.Collapsed;
                            MessageBox.Show("Путешествие завершено", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.GoBack();
        }

        private void RoutePhotoButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Background is ImageBrush brush)
            {
                FullScreenPhoto.Fill = brush;
                SupRect.Visibility = Visibility.Visible;
                FullScreenPhoto.Visibility = Visibility.Visible;
                PhotoBackButton.Visibility = Visibility.Visible;
            }
        }

        private void OrganizerPhotoButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Background is ImageBrush brush)
            {
                FullScreenPhoto.Fill = brush;
                SupRect.Visibility = Visibility.Visible;
                FullScreenPhoto.Visibility = Visibility.Visible;
                PhotoBackButton.Visibility = Visibility.Visible;
            }
        }

        private void ParticipantPhotoButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext != null)
            {
                dynamic participant = button.DataContext;
                if (participant.PhotoBrush is ImageBrush brush)
                {
                    FullScreenPhoto.Fill = brush;
                    SupRect.Visibility = Visibility.Visible;
                    FullScreenPhoto.Visibility = Visibility.Visible;
                    PhotoBackButton.Visibility = Visibility.Visible;
                }
            }
        }

        private void PhotoBackButton_Click(object sender, RoutedEventArgs e)
        {
            SupRect.Visibility = Visibility.Collapsed;
            FullScreenPhoto.Visibility = Visibility.Collapsed;
            PhotoBackButton.Visibility = Visibility.Collapsed;
        }

        private void RouteDetailsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentEvent?.Routes != null)
            {
                int routeId = _currentEvent.Routes.IdRoute;
                NavigationService?.Navigate(new OpenRoutePage(routeId, _userId));
            }
        }
    }
}
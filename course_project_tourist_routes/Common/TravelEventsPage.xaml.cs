using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Data.Entity;
using System.Collections.Generic;
using course_project_tourist_routes.Traveler;
using static MaterialDesignThemes.Wpf.Theme;
using System.Dynamic;

namespace course_project_tourist_routes.Common
{
    public partial class TravelEventsPage : Page
    {
        private int _userId;
        private List<dynamic> _allEvents = new List<dynamic>();

        public TravelEventsPage(int userId)
        {
            InitializeComponent();
            _userId = userId;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            bool isAdmin = IsCurrentUserAdmin();

            MyEventsCheckBox.Visibility = isAdmin ? Visibility.Collapsed : Visibility.Visible;

            LoadEvents();
        }

        private bool IsCurrentUserAdmin()
        {
            try
            {
                using (var context = new TouristRoutesEntities())
                {
                    return context.Users.Any(u => u.IdUser == _userId && u.IdRole == 1);
                }
            }
            catch
            {
                return false;
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

        private void LoadEvents()
        {
            try
            {
                using (var context = new TouristRoutesEntities())
                {
                    int currentUserRoleId = GetUserRoleId();
                    bool isAdmin = currentUserRoleId == 1;

                    var events = context.TravelEvents
                        .Include(e => e.Users)
                        .OrderByDescending(e => e.StartDate)
                        .ToList();

                    _allEvents = events.Select(e =>
                    {
                        dynamic expando = new ExpandoObject();
                        expando.IdEvent = e.IdEvent;
                        expando.TitleEvent = e.TitleEvent;
                        expando.DescriptionEvent = e.DescriptionEvent.Length > 50 ?
                            e.DescriptionEvent.Substring(0, 50) + "..." : e.DescriptionEvent;
                        expando.OrganizerName = e.Users.UserName;
                        expando.OrganizerId = e.Users.IdUser;
                        expando.MaxParticipants = e.MaxParticipants + " уч.";
                        expando.StartDate = e.StartDate;
                        expando.EndDate = e.EndDate;
                        expando.StatusEvent = e.StatusEvent;
                        expando.StatusColor = GetStatusBrush(e.StatusEvent);
                        expando.DateRange = $"{e.StartDate:dd.MM.yyyy} - {e.EndDate:dd.MM.yyyy}";
                        expando.ShowActions = isAdmin || e.Users.IdUser == _userId;
                        expando.IsMyEvent = e.Users.IdUser == _userId && MyEventsCheckBox.IsChecked == true;
                        expando.IsAdminOrMyEvent = isAdmin || (e.Users.IdUser == _userId && MyEventsCheckBox.IsChecked == true);
                        return expando;
                    }).ToList<dynamic>();

                    EventsListView.ItemsSource = _allEvents;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке путешествий: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private SolidColorBrush GetStatusBrush(string status)
        {
            switch (status)
            {
                case "Запланировано": return new SolidColorBrush(Color.FromRgb(33, 150, 243));
                case "В процессе": return new SolidColorBrush(Color.FromRgb(255, 193, 7));
                case "Завершено": return new SolidColorBrush(Color.FromRgb(76, 175, 80));
                case "Отменено": return new SolidColorBrush(Color.FromRgb(244, 67, 54));
                default: return new SolidColorBrush(Color.FromRgb(158, 158, 158));
            }
        }

        private void ApplyFilters()
        {
            if (_allEvents == null || !_allEvents.Any()) return;

            var filtered = _allEvents.AsEnumerable();

            if (StatusComboBox.SelectedItem is ComboBoxItem statusItem && statusItem.Content.ToString() != "Все")
            {
                filtered = filtered.Where(e => e.StatusEvent == statusItem.Content.ToString());
            }

            if (ParticipantsComboBox.SelectedItem is ComboBoxItem participantsItem && participantsItem.Content.ToString() != "Все")
            {
                switch (participantsItem.Content.ToString())
                {
                    case "До 5 человек":
                        filtered = filtered.Where(e => int.Parse(e.MaxParticipants.ToString().Replace(" уч.", "")) <= 5);
                        break;
                    case "5-10 человек":
                        filtered = filtered.Where(e =>
                        {
                            var parts = int.Parse(e.MaxParticipants.ToString().Replace(" уч.", ""));
                            return parts > 5 && parts <= 10;
                        });
                        break;
                    case "10-20 человек":
                        filtered = filtered.Where(e =>
                        {
                            var parts = int.Parse(e.MaxParticipants.ToString().Replace(" уч.", ""));
                            return parts > 10 && parts <= 20;
                        });
                        break;
                    case "Более 20 человек":
                        filtered = filtered.Where(e => int.Parse(e.MaxParticipants.ToString().Replace(" уч.", "")) > 20);
                        break;
                }
            }

            if (DateComboBox.SelectedItem is ComboBoxItem dateItem && dateItem.Content.ToString() != "Все")
            {
                var today = DateTime.Today;
                switch (dateItem.Content.ToString())
                {
                    case "Сегодня":
                        filtered = filtered.Where(e => e.StartDate.Date == today);
                        break;
                    case "На этой неделе":
                        var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
                        var endOfWeek = startOfWeek.AddDays(6);
                        filtered = filtered.Where(e => e.StartDate.Date >= startOfWeek && e.StartDate.Date <= endOfWeek);
                        break;
                    case "В этом месяце":
                        filtered = filtered.Where(e => e.StartDate.Month == today.Month && e.StartDate.Year == today.Year);
                        break;
                    case "Предстоящие":
                        filtered = filtered.Where(e => e.StartDate > today);
                        break;
                    case "Прошедшие":
                        filtered = filtered.Where(e => e.EndDate < today);
                        break;
                }
            }

            if (!string.IsNullOrWhiteSpace(SearchTextBox.Text))
            {
                var searchText = SearchTextBox.Text.ToLower();
                filtered = filtered.Where(e =>
                    e.TitleEvent.ToString().ToLower().Contains(searchText) ||
                    e.DescriptionEvent.ToString().ToLower().Contains(searchText));
            }

            if (MyEventsCheckBox.IsChecked == true)
            {
                filtered = filtered.Where(e => e.OrganizerId == _userId);
            }

            int currentUserRoleId = GetUserRoleId();
            bool isAdmin = currentUserRoleId == 1;

            filtered = filtered.Select(e =>
            {
                e.IsMyEvent = e.OrganizerId == _userId && MyEventsCheckBox.IsChecked == true;
                e.IsAdminOrMyEvent = isAdmin || (e.OrganizerId == _userId && MyEventsCheckBox.IsChecked == true);
                return e;
            });

            EventsListView.ItemsSource = filtered.ToList();
        }

        private void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void MyEventsCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            ApplyFilters();
        }

        private void MyEventsCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            ApplyFilters();
        }

        private void ResetFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            StatusComboBox.SelectedIndex = 0;
            ParticipantsComboBox.SelectedIndex = 0;
            DateComboBox.SelectedIndex = 0;
            SearchTextBox.Text = "";
            MyEventsCheckBox.IsChecked = false;
        }

        private void CreateEventButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AddTravelEventPage(_userId));
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            if (button != null)
            {
                int eventId = (int)button.Tag;
                NavigationService.Navigate(new EditTravelEventPage(_userId, eventId));
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            if (button != null)
            {
                int eventId = (int)button.Tag;

                var result = MessageBox.Show("Вы уверены, что хотите удалить это путешествие?",
                    "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (var context = new TouristRoutesEntities())
                        {
                            var eventToDelete = context.TravelEvents.FirstOrDefault(ev => ev.IdEvent == eventId);
                            if (eventToDelete != null)
                            {
                                context.TravelEvents.Remove(eventToDelete);
                                context.SaveChanges();
                                LoadEvents();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при удалении путешествия: {ex.Message}",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void EventsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EventsListView.SelectedItem != null)
            {
                dynamic selectedEvent = EventsListView.SelectedItem;
                // Можно добавить переход на страницу с подробной информацией
                EventsListView.SelectedItem = null;
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.GoBack();
        }
    }
}
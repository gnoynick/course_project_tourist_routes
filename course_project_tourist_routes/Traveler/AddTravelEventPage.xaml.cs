﻿using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using course_project_tourist_routes.Common;
using System.Windows.Media;
using System.Windows.Input;

namespace course_project_tourist_routes.Traveler
{
    public partial class AddTravelEventPage : Page
    {
        private readonly int _userId;
        private dynamic _selectedRoute;

        public AddTravelEventPage(int userId)
        {
            InitializeComponent();
            _userId = userId;
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
            routesPage.RouteSelected += route =>
            {
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
            SelectedRouteBorder.Visibility = Visibility.Visible;
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

        private void SaveButton_Click(object sender, RoutedEventArgs e)
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

                var travelEvent = new TravelEvents
                {
                    IdRoute = _selectedRoute.IdRoute,
                    IdUser = _userId,
                    TitleEvent = TitleTextBox.Text,
                    StartDate = StartDatePicker.SelectedDate.Value,
                    EndDate = EndDatePicker.SelectedDate.Value,
                    MaxParticipants = maxParticipants,
                    StatusEvent = "Запланировано",
                    DescriptionEvent = DescriptionTextBox.Text,
                    DateAddedEvent = DateTime.Now
                };

                using (var context = new TouristRoutesEntities())
                {
                    context.TravelEvents.Add(travelEvent);
                    context.SaveChanges();
                }

                SavingProgressPanel.Visibility = Visibility.Collapsed;
                MessageBox.Show("Путешествие успешно создано!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                NavigationService.GoBack();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании путешествия: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SaveButton.Visibility = Visibility.Visible;
                SaveButton.IsEnabled = true;
            }
        }
    }
}
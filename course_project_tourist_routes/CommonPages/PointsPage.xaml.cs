using course_project_tourist_routes.AdminPages;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace course_project_tourist_routes.CommonPages
{
    public partial class PointsPage : Page
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool _isAdmin;
        public bool IsAdmin
        {
            get => _isAdmin;
            set
            {
                if (_isAdmin == value) return;
                _isAdmin = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsAdmin)));
            }
        }

        private List<RoutePoints> allPoints = new List<RoutePoints>();
        private List<PointTypes> pointTypes = new List<PointTypes>();
        private List<string> cities = new List<string>();
        private List<string> countries = new List<string>();
        private int _userId;

        public PointsPage(int userId)
        {
            InitializeComponent();
            _userId = userId;
            this.DataContext = this;
            LoadData();
            AddEditPointsPage.DataUpdated += () =>
            {
                Dispatcher.Invoke(() => LoadData());
            };
            AdminButtonsVisibility();
        }

        public void AdminButtonsVisibility()
        {
            try
            {
                using (var context = new TouristRoutesEntities())
                {
                    var role = context.Users
                        .Where(u => u.IdUser == _userId)
                        .Select(u => u.Roles.NameRole)
                        .FirstOrDefault();

                    IsAdmin = (role == "Admin");
                    AddNewPointButton.Visibility = IsAdmin ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            catch
            {
                IsAdmin = false;
                AddNewPointButton.Visibility = Visibility.Collapsed;
            }
        }

        private void ApplyFilters()
        {
            try
            {
                if (PointsDataGrid == null || FilterTypeComboBox == null ||
                    FilterCityComboBox == null || FilterCountryComboBox == null)
                {
                    return;
                }

                var filteredPoints = allPoints.AsEnumerable();

                if (!string.IsNullOrWhiteSpace(SearchTextBox?.Text))
                {
                    string searchText = SearchTextBox.Text.ToLower();
                    filteredPoints = filteredPoints.Where(p =>
                        p.PointName?.ToLower().Contains(searchText) == true);
                }

                if (FilterTypeComboBox.SelectedValue is int selectedTypeId && selectedTypeId > 0)
                {
                    filteredPoints = filteredPoints.Where(p =>
                        p.PointTypes != null && p.PointTypes.IdType == selectedTypeId);
                }

                if (FilterCityComboBox.SelectedItem is string selectedCity && selectedCity != "Все города")
                {
                    filteredPoints = filteredPoints.Where(p =>
                        !string.IsNullOrEmpty(p.City) && p.City == selectedCity);
                }

                if (FilterCountryComboBox.SelectedItem is string selectedCountry && selectedCountry != "Все страны")
                {
                    filteredPoints = filteredPoints.Where(p =>
                        !string.IsNullOrEmpty(p.Country) && p.Country == selectedCountry);
                }

                PointsDataGrid.ItemsSource = filteredPoints.ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при применении фильтров: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void LoadData()
        {
            try
            {
                using (var context = new TouristRoutesEntities())
                {
                    allPoints = context.RoutePoints
                        .Include(p => p.PointTypes)
                        .ToList();

                    PointsDataGrid.ItemsSource = allPoints;

                    pointTypes = context.PointTypes.ToList();

                    var typeList = new List<dynamic>
                    {
                        new { IdType = 0, NameType = "Все типы" }
                    };
                    typeList.AddRange(pointTypes.Select(t => new { t.IdType, t.NameType }));

                    FilterTypeComboBox.ItemsSource = typeList;
                    FilterTypeComboBox.SelectedValuePath = "IdType";
                    FilterTypeComboBox.SelectedIndex = 0;

                    countries = allPoints
                    .Where(p => !string.IsNullOrEmpty(p.Country))
                    .Select(p => p.Country)
                    .Distinct()
                    .OrderBy(c => c)
                    .ToList();

                    FilterCountryComboBox.ItemsSource = new List<string> { "Все страны" }.Concat(countries);
                    FilterCountryComboBox.SelectedIndex = 0;

                    UpdateCitiesList();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void UpdateCitiesList()
        {
            var selectedCountry = FilterCountryComboBox.SelectedItem as string;

            if (selectedCountry == "Все страны" || string.IsNullOrEmpty(selectedCountry))
            {
                cities = allPoints
                    .Where(p => !string.IsNullOrEmpty(p.City))
                    .Select(p => p.City)
                    .Distinct()
                    .OrderBy(c => c)
                    .ToList();
            }
            else
            {
                cities = allPoints
                    .Where(p => !string.IsNullOrEmpty(p.City) &&
                                !string.IsNullOrEmpty(p.Country) &&
                                p.Country == selectedCountry)
                    .Select(p => p.City)
                    .Distinct()
                    .OrderBy(c => c)
                    .ToList();
            }

            var cityList = new List<string> { "Все города" };
            cityList.AddRange(cities);

            FilterCityComboBox.ItemsSource = cityList;
            FilterCityComboBox.SelectedIndex = 0;
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilters();
        private void FilterTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFilters();
        private void FilterCityComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFilters();
        private void FilterCountryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
            UpdateCitiesList();
        }
        private void ResetFiltersButton_Click(object sender, RoutedEventArgs e) => ResetFilters();

        private void ResetFilters()
        {
            SearchTextBox.Text = "";
            FilterTypeComboBox.SelectedIndex = 0;
            FilterCountryComboBox.SelectedIndex = 0;
            UpdateCitiesList();
            PointsDataGrid.ItemsSource = allPoints;
        }
        public event EventHandler<RoutePoints> PointSelected;
        private void SelectPointButton_Click(object sender, RoutedEventArgs e)
        {
            if (PointsDataGrid.SelectedItem is RoutePoints selectedPoint)
            {
                PointSelected?.Invoke(this, selectedPoint);
                if (NavigationService.CanGoBack)
                {
                    NavigationService.GoBack();
                }
            }
            else
            {
                MessageBox.Show("Выберите точку маршрута", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddNewPointButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new AddEditPointsPage());
        }

        private void EditPointButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is RoutePoints selectedPoint)
            {
                NavigationService?.Navigate(new AddEditPointsPage(selectedPoint));
            }
        }

        private void DeletePointButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is RoutePoints pointToDelete)
            {
                var result = MessageBox.Show(
                    $"Вы уверены, что хотите удалить точку '{pointToDelete.PointName}'? Это действие также удалит её из всех маршрутов.",
                    "Подтверждение удаления",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (var context = new TouristRoutesEntities())
                        {
                            var point = context.RoutePoints
                                .Include(p => p.Routes)
                                .FirstOrDefault(p => p.IdPoint == pointToDelete.IdPoint);

                            if (point != null)
                            {
                                foreach (var route in point.Routes.ToList())
                                {
                                    route.RoutePoints.Remove(point);
                                }

                                context.RoutePoints.Remove(point);
                                context.SaveChanges();
                            }
                        }

                        LoadData();
                        MessageBox.Show("Точка успешно удалена!", "Успех",
                                      MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при удалении точки: {ex.Message}", "Ошибка",
                                        MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
        }
    }
}
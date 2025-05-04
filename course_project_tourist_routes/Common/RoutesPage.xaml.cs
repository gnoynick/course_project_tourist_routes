using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Collections.Generic;
using System.Diagnostics;
using System.Data.Entity;

namespace course_project_tourist_routes.Common
{
    public partial class RoutesPage : Page
    {
        private int _userId;
        private List<Categories> _categories = new List<Categories>();
        private List<string> _countries = new List<string>();
        private List<string> _cities = new List<string>();

        public RoutesPage(int userId)
        {
            InitializeComponent();
            _userId = userId;

            LoadCategories();
            LoadCountriesAndCities();
            LoadRoutes();
        }

        private void RoutesPage_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Visibility == Visibility.Visible)
            {
                using (var context = new TouristRoutesEntities())
                {
                    context.ChangeTracker.Entries().ToList().ForEach(x => x.Reload());
                }

                LoadRoutes();
            }
        }

        private void LoadCountriesAndCities()
        {
            try
            {
                using (var context = new TouristRoutesEntities())
                {
                    _countries = context.RoutePoints
                        .Where(p => !string.IsNullOrEmpty(p.Country))
                        .Select(p => p.Country)
                        .Distinct()
                        .OrderBy(c => c)
                        .ToList();

                    _cities = context.RoutePoints
                        .Where(p => !string.IsNullOrEmpty(p.City))
                        .Select(p => p.City)
                        .Distinct()
                        .OrderBy(c => c)
                        .ToList();

                    CountryComboBox.ItemsSource = new List<string> { "Все страны" }.Concat(_countries);
                    CityComboBox.ItemsSource = new List<string> { "Все города" }.Concat(_cities);

                    CountryComboBox.SelectedIndex = 0;
                    CityComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке стран и городов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateCitiesList(string selectedCountry)
        {
            try
            {
                if (selectedCountry == "Все страны" || string.IsNullOrEmpty(selectedCountry))
                {
                    CityComboBox.ItemsSource = new List<string> { "Все города" }.Concat(_cities);
                }
                else
                {
                    using (var context = new TouristRoutesEntities())
                    {
                        var filteredCities = context.RoutePoints
                            .Where(p => p.Country == selectedCountry && !string.IsNullOrEmpty(p.City))
                            .Select(p => p.City)
                            .Distinct()
                            .OrderBy(c => c)
                            .ToList();

                        CityComboBox.ItemsSource = new List<string> { "Все города" }.Concat(filteredCities);
                    }
                }
                CityComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении списка городов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private void LoadCategories()
        {
            try
            {
                using (var context = new TouristRoutesEntities())
                {
                    _categories = context.Categories.OrderBy(c => c.IdCategory).ToList();

                    var allItem = new Categories { IdCategory = 0, NameCategory = "Все" };
                    _categories.Insert(0, allItem);

                    CategoryComboBox.ItemsSource = _categories;
                    CategoryComboBox.SelectedValuePath = "IdCategory";
                    CategoryComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке категорий: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void LoadRoutes(int? categoryId = null, string country = null, string city = null, string searchText = null)
        {
            try
            {
                using (var context = new TouristRoutesEntities())
                {
                    int currentUserRoleId = GetUserRoleId();

                    IQueryable<Routes> routesQuery = context.Routes
                        .Include("Categories")
                        .Include("Users")
                        .Include("Users.Roles")
                        .Include("RoutePoints");

                    if (currentUserRoleId == 2)
                    {
                        routesQuery = routesQuery.Where(r => r.IdUser != _userId);
                    }

                    if (categoryId.HasValue && categoryId.Value > 0)
                    {
                        routesQuery = routesQuery.Where(r => r.IdCategory == categoryId.Value);
                    }

                    if (!string.IsNullOrEmpty(country) && country != "Все страны")
                    {
                        routesQuery = routesQuery.Where(r => r.RoutePoints.Any(p => p.Country == country));
                    }

                    if (!string.IsNullOrEmpty(city) && city != "Все города")
                    {
                        routesQuery = routesQuery.Where(r => r.RoutePoints.Any(p => p.City == city));
                    }

                    if (!string.IsNullOrEmpty(searchText))
                    {
                        routesQuery = routesQuery.Where(r => r.TitleRoute.Contains(searchText));
                    }

                    var routes = routesQuery
                        .OrderByDescending(r => r.IdRoute)
                        .ToList()
                        .Select(r => new
                        {
                            r.IdRoute,
                            r.TitleRoute,
                            DescriptionRoute = r.DescriptionRoute ?? "",
                            CategoryName = r.Categories.NameCategory,
                            AuthorInfo = r.Categories.NameCategory == "Пользовательские" ? $"Автор: {r.Users.UserName}" : "",
                            Countries = string.Join(", ", r.RoutePoints.Select(p => p.Country).Distinct()),
                            Cities = string.Join(", ", r.RoutePoints.Select(p => p.City).Distinct()),
                            ViewsCount = $"Просмотров: {r.ViewsCount ?? 0}",
                            r.IdCategory,
                            r.IdUser,
                            AuthorRole = r.Users.Roles.IdRole,
                            CanEdit = currentUserRoleId == 1 && r.Categories.NameCategory != "Пользовательские",
                            CanDelete = currentUserRoleId == 1
                        });

                    RoutesListView.ItemsSource = routes;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке маршрутов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFilters()
        {
            var selectedCategory = CategoryComboBox.SelectedItem as Categories;
            var selectedCountry = CountryComboBox.SelectedItem as string;
            var selectedCity = CityComboBox.SelectedItem as string;
            var searchText = SearchTextBox.Text;

            LoadRoutes(
                selectedCategory?.IdCategory == 0 ? null : (selectedCategory?.IdCategory),
                selectedCountry,
                selectedCity,
                searchText
            );
        }

        private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void CountryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedCountry = CountryComboBox.SelectedItem as string;
            if (selectedCountry != null)
            {
                UpdateCitiesList(selectedCountry);
                ApplyFilters();
            }
        }

        private void CityComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.GoBack();
        }

        private void ResetFiltersButton_Click(object sender, RoutedEventArgs e) => ResetFilters();

        private void ResetFilters()
        {
            SearchTextBox.Text = "";
            CityComboBox.SelectedIndex = 0;
            CountryComboBox.SelectedIndex = 0;
            CategoryComboBox.SelectedIndex = 0;
        }

        private void RoutesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RoutesListView.SelectedItem != null)
            {
                dynamic selectedItem = RoutesListView.SelectedItem;
                int routeId = selectedItem.IdRoute;
                NavigationService?.Navigate(new OpenRoutePage(routeId, _userId));
                RoutesListView.SelectedItem = null;
            }
        }

        private void EditRouteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is int routeId))
            {
                MessageBox.Show("Ошибка открытия маршрута для редактирования");
                return;
            }
            NavigationService?.Navigate(new EditRoutePage(_userId, routeId));
        }

        private void DeleteRouteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is int routeId))
            {
                MessageBox.Show("Ошибка удаления маршрута");
                return;
            }

            var result = MessageBox.Show("Вы уверены, что хотите удалить этот маршрут?", "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using (var context = new TouristRoutesEntities())
                    {
                        var routeToDelete = context.Routes
                            .Include(r => r.Photos)
                            .Include(r => r.RoutePoints)
                            .Include(r => r.Favorites)
                            .Include(r => r.Hikes)
                            .FirstOrDefault(r => r.IdRoute == routeId);

                        if (routeToDelete != null)
                        {
                            foreach (var photo in routeToDelete.Photos.ToList())
                            {
                                if (!string.IsNullOrEmpty(photo.Photo))
                                {
                                    try
                                    {
                                        CloudStorage.DeleteFileAsync(photo.Photo);
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine($"Ошибка при удалении фото из Google Drive: {ex.Message}");
                                    }
                                }
                                context.Photos.Remove(photo);
                            }

                            foreach (var point in routeToDelete.RoutePoints.ToList())
                            {
                                point.Routes.Remove(routeToDelete);
                            }

                            foreach (var favorite in routeToDelete.Favorites.ToList())
                            {
                                context.Favorites.Remove(favorite);
                            }

                            foreach (var tour in routeToDelete.Hikes.ToList())
                            {
                                context.Hikes.Remove(tour);
                            }

                            context.Routes.Remove(routeToDelete);

                            context.SaveChanges();
                            ApplyFilters();
                            MessageBox.Show("Маршрут удален", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении маршрута: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
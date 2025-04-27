using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using course_project_tourist_routes.AdminPages;
using course_project_tourist_routes.CommonPages;

namespace course_project_tourist_routes.TravelerPages
{
    public partial class MyRoutesPage : Page
    {
        private int _userId;
        private string _tempDirectory;

        public MyRoutesPage(int userId)
        {
            InitializeComponent();
            _userId = userId;

            _tempDirectory = Path.Combine(Directory.GetParent(Environment.CurrentDirectory).Parent.FullName, "temp", "route_photos");
            CleanupEntireTempDirectory();

            LoadRoutes();
        }

        private void CleanupEntireTempDirectory()
        {
            try
            {
                if (Directory.Exists(_tempDirectory))
                {
                    foreach (var file in Directory.GetFiles(_tempDirectory))
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Не удалось удалить файл {file}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при очистке временной директории: {ex.Message}");
            }
        }

        private void LoadRoutes(string searchText = null)
        {
            try
            {
                using (var context = new TouristRoutesEntities())
                {
                    IQueryable<Routes> routesQuery = context.Routes
                        .Include("Categories")
                        .Where(r => r.IdUser == _userId && r.Categories.NameCategory == "Пользовательские");

                    if (!string.IsNullOrWhiteSpace(searchText))
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
                            r.DateAddedRoute
                        });

                    RoutesListView.ItemsSource = routes;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке маршрутов: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            LoadRoutes(SearchTextBox.Text);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            CleanupEntireTempDirectory();
            NavigationService.GoBack();
        }

        private void RoutesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RoutesListView.SelectedItem != null)
            {
                dynamic selectedItem = RoutesListView.SelectedItem;
                int routeId = selectedItem.IdRoute;
                CleanupEntireTempDirectory();
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
            CleanupEntireTempDirectory();
            //NavigationService?.Navigate(new EditRoutePage(routeId, _userId));
        }

        private void DeleteRouteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is int routeId))
            {
                MessageBox.Show("Ошибка удаления маршрута");
                return;
            }

            var result = MessageBox.Show("Вы уверены, что хотите удалить этот маршрут?", "Подтверждение удаления",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using (var context = new TouristRoutesEntities())
                    {
                        var routeToDelete = context.Routes.FirstOrDefault(r => r.IdRoute == routeId);

                        if (routeToDelete != null)
                        {
                            context.Routes.Remove(routeToDelete);
                            context.SaveChanges();
                            LoadRoutes(SearchTextBox.Text);
                            MessageBox.Show("Маршрут успешно удален", "Успех",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении маршрута: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
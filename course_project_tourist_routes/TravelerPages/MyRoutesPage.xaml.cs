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

        public MyRoutesPage(int userId)
        {
            InitializeComponent();
            _userId = userId;

            CloudStorage.ClearRoutePhotosDirectoryAsync();

            LoadRoutes();
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
            CloudStorage.ClearRoutePhotosDirectoryAsync();
            NavigationService.GoBack();
        }

        private void RoutesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RoutesListView.SelectedItem != null)
            {
                dynamic selectedItem = RoutesListView.SelectedItem;
                int routeId = selectedItem.IdRoute;
                CloudStorage.ClearRoutePhotosDirectoryAsync();
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
            CloudStorage.ClearRoutePhotosDirectoryAsync();
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
                        var routeToDelete = context.Routes
                            .Include("Photos")
                            .Include("RoutePoints")
                            .Include("Favorites")
                            .Include("Hikes")
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
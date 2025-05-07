using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Collections.Generic;
using System.Data.Entity;
using course_project_tourist_routes.Common;

namespace course_project_tourist_routes.Traveler
{
    public partial class FavouritesPage : Page
    {
        private readonly int _userId;
        private List<Categories> _categories = new List<Categories>();

        public FavouritesPage(int userId)
        {
            InitializeComponent();
            _userId = userId;

            LoadCategories();
            LoadFavourites();
        }

        private void FavouritesPage_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Visibility == Visibility.Visible)
            {
                using (var context = new TouristRoutesEntities())
                {
                    context.ChangeTracker.Entries().ToList().ForEach(x => x.Reload());
                }
                LoadFavourites();
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

        private void LoadFavourites(int? categoryId = null, string searchText = null)
        {
            try
            {
                using (var context = new TouristRoutesEntities())
                {
                    IQueryable<Favorites> favouritesQuery = context.Favorites
                        .Include(f => f.Routes)
                        .Include(f => f.Routes.Categories)
                        .Include(f => f.Routes.Users)
                        .Where(f => f.IdUser == _userId);

                    if (categoryId.HasValue && categoryId.Value > 0)
                    {
                        favouritesQuery = favouritesQuery.Where(f => f.Routes.IdCategory == categoryId.Value);
                    }

                    if (!string.IsNullOrEmpty(searchText))
                    {
                        favouritesQuery = favouritesQuery.Where(f => f.Routes.TitleRoute.Contains(searchText));
                    }

                    var favourites = favouritesQuery
                        .OrderByDescending(f => f.DateAddedFavorite)
                        .ToList()
                        .Select(f => new
                        {
                            f.IdFavorite,
                            f.DateAddedFavorite,
                            Route = new
                            {
                                f.Routes.IdRoute,
                                f.Routes.TitleRoute,
                                DescriptionRoute = f.Routes.DescriptionRoute ?? "",
                                CategoryName = f.Routes.Categories.NameCategory,
                                f.Routes.IdCategory
                            }
                        });

                    FavouritesListView.ItemsSource = favourites;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке избранных маршрутов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFilters()
        {
            var selectedCategory = CategoryComboBox.SelectedItem as Categories;
            var searchText = SearchTextBox.Text;

            LoadFavourites(
                selectedCategory?.IdCategory == 0 ? null : (int?)selectedCategory?.IdCategory,
                searchText
            );
        }

        private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
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

        private void ResetFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Text = "";
            CategoryComboBox.SelectedIndex = 0;
        }

        private void FavouritesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FavouritesListView.SelectedItem != null)
            {
                dynamic selectedItem = FavouritesListView.SelectedItem;
                int routeId = selectedItem.Route.IdRoute;
                _ = CloudStorage.ClearRoutePhotosDirectoryAsync();
                NavigationService?.Navigate(new OpenRoutePage(routeId, _userId));
                FavouritesListView.SelectedItem = null;
            }
        }

        private void RemoveFavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is int favoriteId))
            {
                MessageBox.Show("Ошибка удаления из избранного");
                return;
            }

            var result = MessageBox.Show("Вы уверены, что хотите удалить этот маршрут из избранного?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using (var context = new TouristRoutesEntities())
                    {
                        var favoriteToRemove = context.Favorites.FirstOrDefault(f => f.IdFavorite == favoriteId);
                        if (favoriteToRemove != null)
                        {
                            context.Favorites.Remove(favoriteToRemove);
                            context.SaveChanges();
                            LoadFavourites();
                            MessageBox.Show("Маршрут удален из избранного", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении из избранного: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
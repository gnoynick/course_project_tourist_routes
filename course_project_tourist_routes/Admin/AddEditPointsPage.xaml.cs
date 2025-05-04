using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Data.Entity;
using course_project_tourist_routes.Common;

namespace course_project_tourist_routes.Admin
{
    public partial class AddEditPointsPage : Page
    {
        public RoutePoints CurrentPoint { get; private set; }
        public List<PointTypes> PointTypes { get; private set; }
        public string PageTitle { get; private set; }

        private bool _isEditMode = false;

        public AddEditPointsPage(RoutePoints pointToEdit = null)
        {
            InitializeComponent();

            CurrentPoint = pointToEdit ?? new RoutePoints();
            _isEditMode = pointToEdit != null;

            PageTitle = _isEditMode ? "Редактирование точки маршрута" : "Добавление точки";

            LoadPointTypes();

            DataContext = this;
        }

        private void LoadPointTypes()
        {
            try
            {
                using (var context = new TouristRoutesEntities())
                {
                    PointTypes = context.PointTypes.ToList();

                    var typeList = new List<dynamic>
                    {
                        new { IdType = 0, NameType = "Выберите тип" }
                    };
                    typeList.AddRange(PointTypes.Select(t => new { t.IdType, t.NameType }));

                    PointTypeComboBox.ItemsSource = typeList;
                    PointTypeComboBox.SelectedValuePath = "IdType";

                    if (_isEditMode && CurrentPoint.IdType > 0)
                    {
                        PointTypeComboBox.SelectedValue = CurrentPoint.IdType;
                    }
                    else
                    {
                        PointTypeComboBox.SelectedIndex = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке типов точек: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                PointTypes = new List<PointTypes>();
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            StringBuilder errors = new StringBuilder();

            var selectedType = (dynamic)PointTypeComboBox.SelectedItem;
            if (selectedType == null || selectedType.IdType == 0)
            {
                errors.AppendLine("Выберите тип точки");
            }
            else
            {
                CurrentPoint.IdType = selectedType.IdType;
            }

            if (string.IsNullOrWhiteSpace(CurrentPoint.PointName))
                errors.AppendLine("Укажите название точки");

            if (CurrentPoint.IdType == 0)
                errors.AppendLine("Выберите тип точки");

            if (string.IsNullOrWhiteSpace(CurrentPoint.Country))
                errors.AppendLine("Укажите страну");

            if (string.IsNullOrWhiteSpace(CurrentPoint.City))
                errors.AppendLine("Укажите город");

            if (errors.Length > 0)
            {
                MessageBox.Show(errors.ToString(), "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                using (var context = new TouristRoutesEntities())
                {
                    if (_isEditMode)
                    {
                        var pointToUpdate = context.RoutePoints.Find(CurrentPoint.IdPoint);
                        if (pointToUpdate != null)
                        {
                            pointToUpdate.PointName = CurrentPoint.PointName;
                            pointToUpdate.IdType = CurrentPoint.IdType;
                            pointToUpdate.Country = CurrentPoint.Country;
                            pointToUpdate.City = CurrentPoint.City;

                            context.SaveChanges();

                            context.Entry(pointToUpdate).Reload();
                            CurrentPoint = pointToUpdate;

                            MessageBox.Show("Изменения успешно сохранены!", "Успех",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    else
                    {
                        CurrentPoint.DateAddedPoint = DateTime.Now;
                        context.RoutePoints.Add(CurrentPoint);
                        context.SaveChanges();

                        MessageBox.Show("Точка успешно сохранена!", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }

                    if (NavigationService?.CanGoBack == true)
                    {
                        NavigationService.GoBack();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
        }
    }
}
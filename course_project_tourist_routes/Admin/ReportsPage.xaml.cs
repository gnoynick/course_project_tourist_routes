using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Data;
using OfficeOpenXml;
using System.IO;
using OfficeOpenXml.Style;

namespace course_project_tourist_routes.Admin
{
    public partial class ReportsPage : Page
    {
        private readonly TouristRoutesEntities _context = new TouristRoutesEntities();

        public ReportsPage()
        {
            InitializeComponent();
            ExcelPackage.License.SetNonCommercialPersonal("Artyom Petrov");
            ReportDataGrid.AutoGenerateColumns = true;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.GoBack();
        }

        private void GenerateNewUsersReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var reportData = _context.Users
                    .OrderByDescending(u => u.DateUserRegistration)
                    .Select(u => new
                    {
                        Логин = u.UserName,
                        Email = u.Email,
                        Статус_аккаунта = u.AccountStatus,
                        Дата_регистрации = u.DateUserRegistration
                    })
                    .ToList();

                ReportTitle.Text = "Отчёт 'Новые пользователи'";
                ReportDataGrid.ItemsSource = reportData;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при формировании отчета: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GeneratePopularRoutesReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var reportData = _context.Routes
                    .OrderByDescending(r => r.ViewsCount)
                    .Select(r => new
                    {
                        Название = r.TitleRoute,
                        Категория = r.Categories.NameCategory,
                        Автор = r.Users.UserName,
                        Дата_добавления = r.DateAddedRoute,
                        Просмотры = r.ViewsCount ?? 0,
                        В_избранном = r.Favorites.Count
                    })
                    .ToList();

                ReportTitle.Text = "Отчёт 'Популярные маршруты'";
                ReportDataGrid.ItemsSource = reportData;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при формировании отчета: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GeneratePopularPointsReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var reportData = _context.RoutePoints
                    .OrderByDescending(p => p.Routes.Count)
                    .Select(p => new
                    {
                        Название = p.PointName,
                        Тип = p.PointTypes.NameType,
                        Страна = p.Country,
                        Город = p.City,
                        Количество_маршрутов = p.Routes.Count,
                        Последнее_добавление = p.Routes.OrderByDescending(r => r.DateAddedRoute)
                               .Select(r => r.DateAddedRoute)
                               .FirstOrDefault() != null
                                   ? p.Routes.OrderByDescending(r => r.DateAddedRoute)
                                             .Select(r => r.DateAddedRoute)
                                             .FirstOrDefault().ToString()
                                   : "нет"
                    })
                    .ToList();

                ReportTitle.Text = "Отчёт 'Популярные точки'";
                ReportDataGrid.ItemsSource = reportData;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при формировании отчета: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportNewUsersToExcel_Click(object sender, RoutedEventArgs e)
        {
            ExportToExcel("Новые пользователи");
        }

        private void ExportPopularRoutesToExcel_Click(object sender, RoutedEventArgs e)
        {
            ExportToExcel("Популярные маршруты");
        }

        private void ExportPopularPointsToExcel_Click(object sender, RoutedEventArgs e)
        {
            ExportToExcel("Популярные точки");
        }

        private void ExportToExcel(string reportName)
        {
            try
            {
                var data = ReportDataGrid.ItemsSource;
                if (data == null)
                {
                    MessageBox.Show("Нет данных для экспорта", "Предупреждение",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string reportsFolder = Path.Combine(documentsPath, "TouristRoutesReports");

                if (!Directory.Exists(reportsFolder))
                {
                    Directory.CreateDirectory(reportsFolder);
                }

                string path = Path.Combine(reportsFolder, $"{reportName}_{DateTime.Now:yyyy.MM.dd_HH-mm-ss}.xlsx");
                FileInfo file = new FileInfo(path);

                using (ExcelPackage package = new ExcelPackage(file))
                {
                    ExcelWorksheet worksheet = package.Workbook.Worksheets.Add(reportName);

                    var firstItem = ((System.Collections.IEnumerable)data).Cast<object>().FirstOrDefault();
                    if (firstItem == null) return;

                    var properties = firstItem.GetType().GetProperties();

                    int columnsCount = properties.Length;

                    worksheet.Cells[1, 1, 1, columnsCount].Merge = true;
                    var titleCell = worksheet.Cells[1, 1];
                    titleCell.Value = $"Отчёт \"{reportName}\"";
                    titleCell.Style.Font.Bold = true;
                    titleCell.Style.Font.Size = 14;
                    titleCell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    titleCell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    titleCell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    titleCell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    var headerRange = worksheet.Cells[2, 1, 2, columnsCount];

                    for (int i = 0; i < columnsCount; i++)
                    {
                        worksheet.Cells[2, i + 1].Value = properties[i].Name;
                    }

                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    headerRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    headerRange.Style.Fill.PatternType = ExcelFillStyle.None;

                    int row = 3;
                    foreach (var item in (System.Collections.IEnumerable)data)
                    {
                        for (int col = 0; col < columnsCount; col++)
                        {
                            var value = properties[col].GetValue(item);
                            var cell = worksheet.Cells[row, col + 1];

                            if (value == null)
                            {
                                cell.Value = null;
                            }
                            else if (value is int)
                            {
                                cell.Style.Numberformat.Format = "0";

                            }
                            else if (value is string s)
                            {
                                if (DateTime.TryParse(s, out DateTime parsedDate))
                                {
                                    cell.Value = parsedDate;
                                    cell.Style.Numberformat.Format = "yyyy-mm-dd HH:mm:ss";
                                }
                                else
                                {
                                    cell.Value = s;
                                }
                            }
                            else
                            {
                                cell.Value = value.ToString();
                            }

                            cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                            cell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                        }
                        row++;
                    }

                    worksheet.Cells.AutoFitColumns();
                    package.Save();
                }

                MessageBox.Show($"Отчет сохранен: {path}", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Threading.Tasks;

namespace course_project_tourist_routes
{
    public partial class App : Application
    {
        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            try
            {
                CloudStorage.ClearProfilePhotosDirectory();
                CloudStorage.ClearRoutePhotosDirectory();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при очистке временных директорий: {ex.Message}");
            }
        }
    }
}
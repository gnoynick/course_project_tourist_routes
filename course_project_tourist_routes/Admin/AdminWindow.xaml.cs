using course_project_tourist_routes.Admin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace course_project_tourist_routes.Admin
{
    public partial class AdminWindow : NavigationWindow
    {
        public AdminWindow(int userId, string userName, string userStatus, Users user)
        {
            InitializeComponent();
            ShowsNavigationUI = false;
            NavigationService.Navigate(new AdminPage(userId, userName, userStatus, this, user));
        }
    }
}

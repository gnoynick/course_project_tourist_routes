using course_project_tourist_routes.AdminPages;
using System;
using System.Collections.Generic;
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

namespace course_project_tourist_routes
{
    public partial class TravelerWindow : NavigationWindow
    {
        public TravelerWindow(int userId, string userName, string userStatus, Users user)
        {
            InitializeComponent();
            ShowsNavigationUI = false;
            NavigationService.Navigate(new TravelerPage(userId, userName, userStatus, this, user));
        }
        public TravelerPage CurrentTravelerPage => NavigationService?.Content as TravelerPage;
    }
}

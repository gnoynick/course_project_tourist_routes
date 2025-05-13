using System.Windows.Navigation;

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

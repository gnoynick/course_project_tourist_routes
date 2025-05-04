using System.Windows.Navigation;

namespace course_project_tourist_routes.Traveler
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

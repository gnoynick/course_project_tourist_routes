using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace course_project_tourist_routes
{
    public partial class AutorizWindow : Window
    {
        public AutorizWindow()
        {
            InitializeComponent();
        }

        public static string GetHash(string password)
        {
            using (var hash = SHA1.Create())
            {
                return string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(password)).Select(x => x.ToString("X2")));
            }
        }

        private void ShowPasswordButton_Click(object sender, RoutedEventArgs e)
        {
            TogglePasswordVisibility(PasswordBox, PasswordTextBox);
            var button = (Button)sender;
            if (PasswordBox.Visibility == Visibility.Visible)
            {
                ((MaterialDesignThemes.Wpf.PackIcon)button.Content).Kind = MaterialDesignThemes.Wpf.PackIconKind.Eye;
            }
            else
            {
                ((MaterialDesignThemes.Wpf.PackIcon)button.Content).Kind = MaterialDesignThemes.Wpf.PackIconKind.EyeOff;
            }
        }

        private void TogglePasswordVisibility(PasswordBox passwordBox, TextBox textBox)
        {
            if (passwordBox.Visibility == Visibility.Visible)
            {
                textBox.Text = passwordBox.Password;
                passwordBox.Visibility = Visibility.Collapsed;
                textBox.Visibility = Visibility.Visible;
            }
            else
            {
                passwordBox.Password = textBox.Text;
                textBox.Visibility = Visibility.Collapsed;
                passwordBox.Visibility = Visibility.Visible;
            }
        }

        private void SignInButton_Click(object sender, RoutedEventArgs e)
        {
            string login = LoginTextBox.Text;
            string password = GetPassword();

            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Введите логин и пароль!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                var context = TouristRoutesEntities.GetContext();
                var userExists = context.Users.AsNoTracking()
                       .Any(u => u.UserName == login);

                if (!userExists)
                {
                    MessageBox.Show("Такого аккаунта не существует!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string hashedPassword = GetHash(password);

                var user = context.Users.AsNoTracking()
                    .FirstOrDefault(u => u.UserName == login && u.PasswordHash == hashedPassword);

                if (user == null)
                {
                    MessageBox.Show("Неверный логин или пароль!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (user.AccountStatus == "Заблокирован")
                {
                    MessageBox.Show("Ваш аккаунт заблокирован!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (user.IdRole == 1)
                {
                    AdminWindow adminWindow = new AdminWindow(user.IdUser, user.UserName, user.ProfileStatus, user);
                    this.Close();
                    adminWindow.Show();
                }
                else if (user.IdRole == 2)
                {
                    TravelerWindow travelerWindow = new TravelerWindow(user.IdUser, user.UserName, user.ProfileStatus, user);
                    this.Close();
                    travelerWindow.Show();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при входе: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetPassword()
        {
            return PasswordTextBox.Visibility == Visibility.Visible ? PasswordTextBox.Text : PasswordBox.Password;
        }

        private void PasswordTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsValidPasswordInput(PasswordTextBox.Text))
            {
                PasswordTextBox.Text = Regex.Replace(PasswordTextBox.Text, @"[^a-zA-Zа-яА-Я0-9!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]", "");
                PasswordTextBox.CaretIndex = PasswordTextBox.Text.Length;
            }
            PasswordBox.Password = PasswordTextBox.Text;
            UpdatePasswordCounter();
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            UpdatePasswordCounter();
        }

        private bool IsValidPasswordInput(string password)
        {
            string pattern = @"^[a-zA-Zа-яА-Я0-9!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]*$";
            return Regex.IsMatch(password, pattern);
        }

        private void UpdatePasswordCounter()
        {
            PasswordCounter.Text = $"{PasswordBox.Password.Length}/20";
        }

        private void TextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            RegistWindow registWindow = new RegistWindow();
            registWindow.Show();
            this.Close();
        }

        private void LoginTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsValidUsername(LoginTextBox.Text))
            {
                LoginTextBox.Text = Regex.Replace(LoginTextBox.Text, @"[^a-zA-Zа-яА-Я0-9_]", "");
                LoginTextBox.CaretIndex = LoginTextBox.Text.Length;
            }
        }

        private bool IsValidUsername(string username)
        {
            string pattern = @"^[a-zA-Zа-яА-Я0-9_]*$";
            return Regex.IsMatch(username, pattern);
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
        }

        private void PasswordBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
        }
    }
}
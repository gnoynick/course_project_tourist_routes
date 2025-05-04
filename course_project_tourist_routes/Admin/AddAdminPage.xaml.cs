using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;

namespace course_project_tourist_routes.Admin
{
    public partial class AddAdminPage : Page
    {
        public AddAdminPage()
        {
            InitializeComponent();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.GoBack();
        }

        private void UsernameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsValidUsername(UsernameTextBox.Text))
            {
                UsernameTextBox.Text = Regex.Replace(UsernameTextBox.Text, @"[^a-zA-Zа-яА-Я0-9_]", "");
                UsernameTextBox.CaretIndex = UsernameTextBox.Text.Length;
            }
        }

        private bool IsValidUsername(string username)
        {
            string pattern = @"^[a-zA-Zа-яА-Я0-9_]*$";
            return Regex.IsMatch(username, pattern);
        }

        public static string GetHash(string password)
        {
            using (var hash = SHA1.Create())
            {
                return string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(password)).Select(x => x.ToString("X2")));
            }
        }

        private bool IsValidEmail(string email)
        {
            string pattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";
            return Regex.IsMatch(email, pattern);
        }

        private bool IsValidEmailInput(string email)
        {
            string pattern = @"^[a-zA-Z0-9@.\-]*$";
            return Regex.IsMatch(email, pattern) &&
                   email.Count(c => c == '@') <= 1 &&
                   (email.IndexOf('@') < 0 || email.Substring(email.IndexOf('@')).Count(c => c == '.') <= 1);
        }

        private void CreateAdminButton_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameTextBox.Text;
            string email = EmailTextBox.Text;
            string password = GetPassword();
            string repeatPassword = GetRepeatPassword();

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(repeatPassword))
            {
                MessageBox.Show("Все поля должны быть заполнены!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!IsValidUsername(username))
            {
                MessageBox.Show("Логин может содержать только буквы (латиница/кириллица), цифры и символ подчеркивания", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (password != repeatPassword)
            {
                MessageBox.Show("Пароли не совпадают!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!IsValidPassword(password))
            {
                MessageBox.Show("Пароль должен содержать минимум 6 символов, хотя бы одну цифру, одну заглавную и одну строчную букву.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!IsValidEmail(email))
            {
                MessageBox.Show("Введите корректный email! Разрешены только латинские буквы, цифры и символы @ . -", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string hashedPassword = GetHash(password);

            try
            {
                using (var db = new TouristRoutesEntities())
                {
                    if (db.Users.Any(u => u.UserName == username))
                    {
                        MessageBox.Show("Администратор с таким логином уже существует!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    if (db.Users.Any(u => u.Email == email))
                    {
                        MessageBox.Show("Администратор с такой почтой уже существует!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    db.Users.Add(new Users
                    {
                        UserName = username,
                        Email = email,
                        PasswordHash = hashedPassword,
                        IdRole = 1,
                        ProfileStatus = "Администратор",
                        AccountStatus = "Активен",
                        DateAddedUser = DateTime.Now
                    });

                    db.SaveChanges();
                }

                MessageBox.Show("Администратор успешно добавлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                NavigationService.GoBack();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении администратора: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetPassword()
        {
            return PasswordTextBox.Visibility == Visibility.Visible ? PasswordTextBox.Text : PasswordBox.Password;
        }

        private string GetRepeatPassword()
        {
            return RepeatPasswordTextBox.Visibility == Visibility.Visible ? RepeatPasswordTextBox.Text : RepeatPasswordBox.Password;
        }

        private bool IsValidPassword(string password)
        {
            string pattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{6,}$";
            return Regex.IsMatch(password, pattern);
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

        private void ShowRepeatPasswordButton_Click(object sender, RoutedEventArgs e)
        {
            TogglePasswordVisibility(RepeatPasswordBox, RepeatPasswordTextBox);
            var button = (Button)sender;
            if (RepeatPasswordBox.Visibility == Visibility.Visible)
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

        private void PasswordTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsValidPasswordInput(PasswordTextBox.Text))
            {
                PasswordTextBox.Text = Regex.Replace(PasswordTextBox.Text, @"[^a-zA-Zа-яА-Я0-9!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]", "");
                PasswordTextBox.CaretIndex = PasswordTextBox.Text.Length;
            }
            PasswordBox.Password = PasswordTextBox.Text;
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
        }

        private void RepeatPasswordTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsValidPasswordInput(RepeatPasswordTextBox.Text))
            {
                RepeatPasswordTextBox.Text = Regex.Replace(RepeatPasswordTextBox.Text, @"[^a-zA-Zа-яА-Я0-9!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]", "");
                RepeatPasswordTextBox.CaretIndex = RepeatPasswordTextBox.Text.Length;
            }
            RepeatPasswordBox.Password = RepeatPasswordTextBox.Text;
        }

        private void RepeatPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
        }

        private bool IsValidPasswordInput(string password)
        {
            string pattern = @"^[a-zA-Zа-яА-Я0-9!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]*$";
            return Regex.IsMatch(password, pattern);
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

        private void EmailTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsValidEmailInput(EmailTextBox.Text))
            {
                EmailTextBox.Text = Regex.Replace(EmailTextBox.Text, @"[^a-zA-Z0-9@.\-]", "");
                EmailTextBox.CaretIndex = EmailTextBox.Text.Length;
            }
        }
    }
}
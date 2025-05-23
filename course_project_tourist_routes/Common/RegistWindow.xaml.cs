﻿using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace course_project_tourist_routes.Common
{
    public partial class RegistWindow : Window
    {
        public RegistWindow()
        {
            InitializeComponent();
            UsernameTextBox.Focus();
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

        private void SignUpButton_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameTextBox.Text;
            string email = EmailTextBox.Text;
            string password = GetPassword(); // получение пароля
            string repeatPassword = GetRepeatPassword(); // получение подтвержения пароля

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(repeatPassword))
            {
                MessageBox.Show("Все поля должны быть заполнены!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!IsValidUsername(username)) // валидация логина
            {
                MessageBox.Show("Логин может содержать только буквы (латиница/кириллица), цифры и символ подчеркивания", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (password != repeatPassword) // проверка совпадения паролей
            {
                MessageBox.Show("Пароли не совпадают!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!IsValidPassword(password)) // валидация пароля 
            {
                MessageBox.Show("Пароль должен содержать минимум 6 символов, хотя бы одну цифру, одну заглавную и одну строчную букву.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!IsValidEmail(email)) // валидация электронной почты
            {
                MessageBox.Show("Введите корректный email! Разрешены только латинские буквы, цифры и символы @ . -", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string hashedPassword = GetHash(password); // получение хэша пароля

            try
            {
                using (var db = new TouristRoutesEntities())
                {
                    if (db.Users.Any(u => u.UserName == username)) // проверка на существование пользователя по логину
                    {
                        MessageBox.Show("Пользователь с таким логином уже существует!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    if (db.Users.Any(u => u.Email == email)) // проверка на существование пользователя по электронной почте
                    {
                        MessageBox.Show("Пользователь с такой почтой уже существует!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    db.Users.Add(new Users
                    {
                        UserName = username,
                        Email = email,
                        PasswordHash = hashedPassword,
                        IdRole = 2, // автоматическое присваивание роли "Путешественник" 
                        ProfileBio = "Ваше описание",
                        AccountStatus = "Активен",
                        DateUserRegistration = DateTime.Now
                    });

                    db.SaveChanges(); // сохранение в базе данных
                }

                MessageBox.Show("Регистрация прошла успешно!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                new AutorizWindow().Show();
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при регистрации: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
            UpdatePasswordCounter();
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            UpdatePasswordCounter();
        }

        private void RepeatPasswordTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsValidPasswordInput(RepeatPasswordTextBox.Text))
            {
                RepeatPasswordTextBox.Text = Regex.Replace(RepeatPasswordTextBox.Text, @"[^a-zA-Zа-яА-Я0-9!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]", "");
                RepeatPasswordTextBox.CaretIndex = RepeatPasswordTextBox.Text.Length;
            }
            RepeatPasswordBox.Password = RepeatPasswordTextBox.Text;
            UpdateRepeatPasswordCounter();
        }

        private void RepeatPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            UpdateRepeatPasswordCounter();
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

        private void UpdateRepeatPasswordCounter()
        {
            RepeatPasswordCounter.Text = $"{RepeatPasswordBox.Password.Length}/20";
        }

        private void TextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            new AutorizWindow().Show();
            Close();
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
            if (sender is TextBox textBox)
            {
                string newText = textBox.Text.ToLower();

                newText = Regex.Replace(newText, @"[^a-z0-9@.\-]", "");

                if (newText != textBox.Text)
                {
                    int caretIndex = textBox.CaretIndex;
                    textBox.Text = newText;
                    textBox.CaretIndex = Math.Min(caretIndex, newText.Length);
                }
            }
        }
    }
}

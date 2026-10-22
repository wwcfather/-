using System.Windows;
using System.Windows.Input;

namespace halconDemo
{
    /// <summary>登录窗口：账号密码写死校验，成功后打开主窗口。</summary>
    public partial class LoginWindow : Window
    {
        // 写死的登录凭据，需要修改时改这里
        private const string ValidUsername = "admin";
        private const string ValidPassword = "admin123";

        public LoginWindow()
        {
            InitializeComponent();
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            TryLogin();
        }

        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                TryLogin();
        }

        private void TryLogin()
        {
            if (UsernameBox.Text == ValidUsername && PasswordBox.Password == ValidPassword)
            {
                var main = new MainWindow();
                Application.Current.MainWindow = main;
                main.Show();
                Close();
            }
            else
            {
                ErrorText.Visibility = Visibility.Visible;
                PasswordBox.Clear();
                PasswordBox.Focus();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}

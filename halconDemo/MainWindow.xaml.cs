using System.Windows;
using System.Windows.Input;
using halconDemo.ViewModels;

namespace halconDemo
{
    /// <summary>Interaction logic for MainWindow.xaml</summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        // ---------- 自定义标题栏 ----------

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 双击切换最大化/还原，单击拖动窗口
            if (e.ClickCount == 2)
                ToggleMaximize();
            else if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximize();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OpenCommunication_Click(object sender, RoutedEventArgs e)
        {
            new CommunicationWindow().Show();
        }

        private void OpenQuery_Click(object sender, RoutedEventArgs e)
        {
            new QueryWindow().Show();
        }

        private void ToggleMaximize()
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        /// <summary>最大化时去掉圆角和投影边距，避免屏幕边缘出现缝隙。</summary>
        private void MainWindow_StateChanged(object? sender, System.EventArgs e)
        {
            bool maximized = WindowState == WindowState.Maximized;
            RootBorder.Margin = maximized ? new Thickness(0) : new Thickness(12);
            RootBorder.CornerRadius = new CornerRadius(maximized ? 0 : 12);
        }
    }
}

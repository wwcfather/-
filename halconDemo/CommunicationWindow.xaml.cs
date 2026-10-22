using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace halconDemo
{
    /// <summary>通讯配置窗口：仅界面样式，无实际通讯逻辑。</summary>
    public partial class CommunicationWindow : Window
    {
        public CommunicationWindow()
        {
            InitializeComponent();
            Owner = Application.Current.MainWindow;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>按协议类型切换 TCP / 串口参数面板。</summary>
        private void ProtocolBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TcpPanel == null || SerialPanel == null) return;

            bool serial = ProtocolBox.SelectedIndex == 2;
            SerialPanel.Visibility = serial ? Visibility.Visible : Visibility.Collapsed;
            TcpPanel.Visibility = serial ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}

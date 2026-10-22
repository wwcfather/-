using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace halconDemo
{
    /// <summary>记录查询窗口：仅界面展示，数据为写死的示例行。</summary>
    public partial class QueryWindow : Window
    {
        public QueryWindow()
        {
            InitializeComponent();
            Owner = Application.Current.MainWindow;
            DataContext = this;

            // 示例数据，仅用于展示表格样式
            Records = new List<QueryRecord>
            {
                new(1, "2026-10-13 19:24:44", "hsg_001.bmp", 0, "OK", 86),
                new(2, "2026-10-13 19:24:47", "hsg_002.bmp", 2, "NG", 91),
                new(3, "2026-10-13 19:24:51", "hsg_003.bmp", 0, "OK", 83),
                new(4, "2026-10-13 19:24:55", "hsg_004.bmp", 5, "NG", 95),
                new(5, "2026-10-13 19:24:58", "hsg_005.bmp", 0, "OK", 88),
            };
        }

        public List<QueryRecord> Records { get; }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    /// <summary>查询结果行（示例模型）。</summary>
    public record QueryRecord(int Index, string Time, string FileName, int DefectCount, string Result, int Elapsed);
}

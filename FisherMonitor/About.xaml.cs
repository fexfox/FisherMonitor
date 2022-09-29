using System.Windows;
using System.Windows.Input;

namespace FisherMonitor
{
    /// <summary>
    /// About.xaml 的交互逻辑
    /// </summary>
    public partial class About
    {
        public About()
        {
            InitializeComponent();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            OkButton_Click(sender, e);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            VersionTextLabel.Content = "v" + Ver.VER + " " +Ver.DATE;
            InfoBox.Text = "开发者: https://space.bilibili.com/761607/" +"\n";
            InfoBox.AppendText("注意：\n本程序不是bilibili官方出品。\n请在不违反bilibili用户协议的前提下使用。\n\nFisherMonitor可能会收集一些您的设备信息用于改进程序，其中不包含您的任何隐私信息。");
        }
    }
}

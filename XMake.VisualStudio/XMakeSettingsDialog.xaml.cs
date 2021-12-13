using Microsoft.VisualStudio.PlatformUI;
using System.Windows.Controls;

namespace XMake.VisualStudio
{
    /// <summary>
    /// XMakeSettingsDialog.xaml 的交互逻辑
    /// </summary>
    public partial class XMakeSettingsDialog : DialogWindow
    {
        public XMakeSettingsDialog()
        {
            InitializeComponent();

            //ComboBoxItem debugComboBoxItem = new ComboBoxItem();
            //debugComboBoxItem.Content = "Debug";
            //this.ModeComboBox.Items.Add(Content);
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}

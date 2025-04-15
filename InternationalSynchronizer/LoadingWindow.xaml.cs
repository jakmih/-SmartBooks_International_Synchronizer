using System.Windows;

namespace InternationalSynchronizer
{
    public partial class LoadingWindow : Window
    {
        public LoadingWindow()
        {
            InitializeComponent();
        }

        public void UpdateText(string text)
        {
            LoadingTextBlock.Text = text;
        }
    }
}

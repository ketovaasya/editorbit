using Avalonia.Controls;
using Avalonia.Interactivity;
using AvaloniaApplication1.Views;
using System;

namespace AvaloniaApplication1.Views
{
    public partial class HomeView : UserControl
    {
        public HomeView()
        {
            InitializeComponent();
        }

        private void StartWork_Click(object sender, RoutedEventArgs e)
        {
            // Переход на MainView
            if (this.Parent is ContentControl contentControl)
            {
                contentControl.Content = new MainView();
            }
            else if (this.Parent is Window window)
            {
                window.Content = new MainView();
            }
        }

        private void OpenManual_Click(object sender, RoutedEventArgs e)
        {
            // Переход на ManualView
            if (this.Parent is ContentControl contentControl)
            {
                contentControl.Content = new ManualView();
            }
            else if (this.Parent is Window window)
            {
                window.Content = new ManualView();
            }
        }
    }
}
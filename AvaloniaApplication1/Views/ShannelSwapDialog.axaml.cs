using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace AvaloniaApplication1.Views
{
    public partial class ChannelSwapDialog : Window
    {
        public ChannelSwapResult Result { get; private set; }

        public ChannelSwapDialog()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            this.SizeToContent = SizeToContent.WidthAndHeight;
            this.MinWidth = 300;
            this.MinHeight = 200;
            this.Background = Brushes.White;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            var sourceCombo = this.FindControl<ComboBox>("SourceChannelComboBox");
            var destCombo = this.FindControl<ComboBox>("DestinationChannelComboBox");

            if (sourceCombo?.SelectedItem == null || destCombo?.SelectedItem == null)
                return;

            var sourceTag = (sourceCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            var destTag = (destCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();

            if (string.IsNullOrEmpty(sourceTag)) sourceTag = "Red";
            if (string.IsNullOrEmpty(destTag)) destTag = "Red";

            Result = new ChannelSwapResult
            {
                SourceChannel = ParseChannel(sourceTag),
                DestinationChannel = ParseChannel(destTag)
            };

            Close(Result);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close(null);
        }

        private Redactor.Channel ParseChannel(string channel)
        {
            return channel switch
            {
                "Red" => Redactor.Channel.Red,
                "Green" => Redactor.Channel.Green,
                "Blue" => Redactor.Channel.Blue,
                "Alpha" => Redactor.Channel.Alpha,
                _ => Redactor.Channel.Red
            };
        }
    }

    public class ChannelSwapResult
    {
        public Redactor.Channel SourceChannel { get; set; }
        public Redactor.Channel DestinationChannel { get; set; }
    }
}
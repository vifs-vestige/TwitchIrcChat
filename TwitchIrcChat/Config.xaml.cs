using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Xceed.Wpf.Toolkit;
using Xceed.Wpf.DataGrid;

namespace TwitchIrcChat
{
    public partial class Config : Window
    {
        private BrushConverter Converter;
        public SolidColorBrush BackgroundChatColor;
        public SolidColorBrush BackgroundUserColor;
        public SolidColorBrush BackgroundTextBoxColor;
        public SolidColorBrush TextColor;
        public string DateFormat;
        public bool ShowJoinPart;
        public SolidColorBrush JoinPartColor;
        public SolidColorBrush TextBoxTextColor;
        public SolidColorBrush UserColor;
        public bool FlashOnUser;
        public bool FlashOnText;
        private bool Saved = false;

        public Config()
        {
            InitializeComponent();
            Converter = new BrushConverter();
            Flash.Items.Add("Never");
            Flash.Items.Add("User Name");
            Flash.Items.Add("Always");
            Flash.SelectedItem = "Never";
            ApplyDefaults();
        }

        private void ApplyDefaults()
        {
            BackgroundChatColor = (SolidColorBrush)Converter.ConvertFromString(Settings.Default.BackgroundChatColor);
            BackgroundUserColor = (SolidColorBrush)Converter.ConvertFromString(Settings.Default.BackgroundUserColor);
            BackgroundTextBoxColor = (SolidColorBrush)Converter.ConvertFromString(Settings.Default.BackgroundTextBoxColor);
            TextColor = (SolidColorBrush)Converter.ConvertFromString(Settings.Default.TextColor);
            JoinPartColor = (SolidColorBrush)Converter.ConvertFromString(Settings.Default.JoinPartColor);
            TextBoxTextColor = (SolidColorBrush)Converter.ConvertFromString(Settings.Default.TextBoxTextColor);
            UserColor = (SolidColorBrush)Converter.ConvertFromString(Settings.Default.UserColor);
            DateFormat = Settings.Default.DateFormat;
            ShowJoinPart = Settings.Default.ShowJoinPart;
            FlashOnText = Settings.Default.FlashOnText;
            FlashOnUser = Settings.Default.FlashOnUser;

            BackgroundChatColorPicker.SelectedColor = BackgroundChatColor.Color;
            UserListColorPicker.SelectedColor = BackgroundUserColor.Color;
            BackgroundTextBoxColorPicker.SelectedColor = BackgroundTextBoxColor.Color;
            TextColorPicker.SelectedColor = TextColor.Color;
            JoinPartColorPicker.SelectedColor = JoinPartColor.Color;
            TextBoxTextColorPicker.SelectedColor = TextBoxTextColor.Color;
            UserListColorPicker.SelectedColor = UserColor.Color;
            DateFormatText.Text = DateFormat;
            JoinPart.IsChecked = ShowJoinPart;
            if (FlashOnText)
                Flash.SelectedItem = "Always";
            if (FlashOnUser && !FlashOnText)
                Flash.SelectedItem = "User Name";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            Saved = true;
            if (Flash.SelectedItem == "Never")
            {
                FlashOnText = false;
                FlashOnUser = false;
            }
            else if (Flash.SelectedItem == "User Name")
            {
                FlashOnText = false;
                FlashOnUser = true;
            }
            else
            {
                FlashOnText = true;
                FlashOnUser = true;
            }
            DateFormat = DateFormatText.Text;
            ShowJoinPart = (bool)JoinPart.IsChecked;
            SaveToDefaults();
            MainWindow main = (MainWindow)Application.Current.MainWindow;
            main.SaveConfig(this);
        }

        private void SaveToDefaults()
        {
            Settings.Default.BackgroundChatColor = BackgroundChatColor.Color.ToString();
            Settings.Default.BackgroundUserColor = BackgroundUserColor.Color.ToString();
            Settings.Default.BackgroundTextBoxColor = BackgroundTextBoxColor.Color.ToString();
            Settings.Default.TextColor = TextColor.Color.ToString();
            Settings.Default.JoinPartColor = JoinPartColor.Color.ToString();
            Settings.Default.TextBoxTextColor = TextBoxTextColor.Color.ToString();
            Settings.Default.DateFormat = DateFormat;
            Settings.Default.ShowJoinPart = ShowJoinPart;
            Settings.Default.FlashOnUser = FlashOnUser;
            Settings.Default.FlashOnText = FlashOnText;
            Settings.Default.UserColor = UserColor.Color.ToString();
            Settings.Default.Save();
        }

        private void BackgroundChatColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color> e)
        {
            var tempColor = e.NewValue.ToString();
            BackgroundChatColor = (SolidColorBrush)Converter.ConvertFromString(tempColor);
        }

        private void UserListBackgroundColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color> e)
        {
            var tempColor = e.NewValue.ToString();
            BackgroundUserColor = (SolidColorBrush)Converter.ConvertFromString(tempColor);
        }

        private void BackgroundTextBoxColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color> e)
        {
            var tempColor = e.NewValue.ToString();
            BackgroundTextBoxColor = (SolidColorBrush)Converter.ConvertFromString(tempColor);
        }

        private void TextBoxTextColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color> e)
        {
            var tempColor = e.NewValue.ToString();
            TextBoxTextColor = (SolidColorBrush)Converter.ConvertFromString(tempColor);
        }

        private void TextColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color> e)
        {
            var tempColor = e.NewValue.ToString();
            TextColor = (SolidColorBrush)Converter.ConvertFromString(tempColor);
        }

        private void JoinPartColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color> e)
        {
            var tempColor = e.NewValue.ToString();
            JoinPartColor = (SolidColorBrush)Converter.ConvertFromString(tempColor);
        }

        private void UserListColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color> e)
        {
            var tempColor = e.NewValue.ToString();
            UserColor = (SolidColorBrush)Converter.ConvertFromString(tempColor);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!Saved)
            {
                if (DealWithClosing())
                {
                    e.Cancel = true;
                }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private bool DealWithClosing()
        {
            if (System.Windows.MessageBox.Show("You did not save, are you sure you want to close?",
                "Did Not Save Error",
                MessageBoxButton.YesNo) == MessageBoxResult.No)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

    }
}

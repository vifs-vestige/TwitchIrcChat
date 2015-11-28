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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using System.Threading;
using System.Net.Sockets;
using WpfApplication1;
using System.Text.RegularExpressions;
using System.IO.IsolatedStorage;
using System.Windows.Controls.Primitives;

namespace TwitchIrcChat
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public Emotes EmoteList;
        public static string Server = "irc.twitch.tv";
        public static int Port = 6667;
        public string Channel, Nick, Password, FormatedMessage, ReplyingUser;
        string LineFromReader = "";
        StreamReader reader = null;
        StreamWriter writer = null;
        NetworkStream ns = null;
        bool IsLineRead = true;
        DispatcherTimer Timer;
        UserList userList;
        public char[] ChatSeperator = { ' ' };
        public string[] words;
        public TcpClient IRCconnection = null;
        Thread ReadStreamThread;
        bool isOnline = false;
        bool isDisconnected = false;
        Dictionary<Paragraph, String> EveryInput;
        IsolatedStorageFile isolatedStorage;
        string isoFile = "isoFile";
        public const string RegexHyperLink = @"((http|ftp|https):\/\/)?[\w\-_]+(\.[\w\-_]+)+([\w\-\.,@?^=%&amp;:/~\+#]*[\w\-\@?^=%&amp;/~\+#])?";
        public string ContextMenuUser = "";
        public string Current = "";
        private bool IsCurrent = false;
        private Stack<string> Up = new Stack<string>();
        private Stack<string> Down = new Stack<string>();
        public const int MAXSTACKSAVE = 30;
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
        public bool KeepOnTop;
        private List<TabWindow> Tabs;
        private int CurrentTab;
        private UserList EmptyUserList;
        private Image Image_test;


        public void UpdateSelected(UserList temp, int tabIndex = 0)
        {
            CurrentTab = tabIndex;
            userList = temp;
            updateUserList();
        }
        

        public MainWindow()
        {
            InitializeComponent();
            EmptyUserList = new UserList();
            Down.Push("");
            EveryInput = new Dictionary<Paragraph, string>();
            EmoteList = new Emotes();
            image_test.Width = 0;
            Image_test = image_test;
            Timer = new DispatcherTimer();
            Timer.Interval = TimeSpan.FromMilliseconds(200);
            Timer.Tick += new EventHandler(UpdateText_Tick);
            //Timer.Start();
            userList = new UserList();
            Text_UserList.Document.PageWidth = 1000;
            isolatedStorage = IsolatedStorageFile.GetUserStoreForAssembly();
            Tabs = new List<TabWindow>();
            if (isolatedStorage.FileExists(isoFile))
            {
                RememberMe.IsChecked = true;
                using (StreamReader sr = new StreamReader(new IsolatedStorageFileStream(isoFile, FileMode.Open, isolatedStorage)))
                {
                    text_user.Text = sr.ReadLine();
                    text_chan.Text = sr.ReadLine().Replace("#", "");
                    text_pass.Password = sr.ReadLine();
                }
            }
            LoadDefaults();
        }

        private void LoadDefaults()
        {
            var converter = new BrushConverter();
            BackgroundChatColor = (SolidColorBrush)converter.ConvertFromString(Settings.Default.BackgroundChatColor);
            BackgroundUserColor = (SolidColorBrush)converter.ConvertFromString(Settings.Default.BackgroundUserColor);
            BackgroundTextBoxColor = (SolidColorBrush)converter.ConvertFromString(Settings.Default.BackgroundTextBoxColor);
            TextColor = (SolidColorBrush)converter.ConvertFromString(Settings.Default.TextColor);
            JoinPartColor = (SolidColorBrush)converter.ConvertFromString(Settings.Default.JoinPartColor);
            TextBoxTextColor = (SolidColorBrush)converter.ConvertFromString(Settings.Default.TextBoxTextColor);
            UserColor = (SolidColorBrush)converter.ConvertFromString(Settings.Default.UserColor);
            DateFormat = Settings.Default.DateFormat;
            ShowJoinPart = Settings.Default.ShowJoinPart;
            FlashOnText = Settings.Default.FlashOnText;
            FlashOnUser = Settings.Default.FlashOnUser;
            KeepOnTop = Settings.Default.KeepOnTop;
            ApplySettings();
        }

        private void ApplySettings()
        {
            //chat_area.Background = BackgroundChatColor;
            ServerChatArea.Background = BackgroundChatColor;
            foreach (var item in Tabs)
            {
                item.UpdateColor(BackgroundChatColor);
            }
            Text_UserList.Background = BackgroundUserColor;
            Text_UserList.Foreground = UserColor;
            textBox1.Background = BackgroundTextBoxColor;
            textBox1.Foreground = TextBoxTextColor;
        }
        
        private void SendMessage(string message)
        {
            if (CurrentTab != 0)
            {
                Tabs[CurrentTab-1].SendMessage(message);
            }

        }

        private void Connect()
        {
            if (text_pass.Password.Contains("oauth"))
            {
                Nick = text_user.Text.ToLower();
                Password = text_pass.Password.ToString();
                Channel = "#" + text_chan.Text.ToString();
                if (Nick.Length > 1 && Password.Length > 1 && Channel.Length > 1)
                {
                    //textInput("Connected");
                    //DataSend("jtvclient", null);
                    try
                    {
                        IRCconnection = new TcpClient(Server, Port);
                        ns = IRCconnection.GetStream();
                        reader = new StreamReader(ns);
                        writer = new StreamWriter(ns);
                        DataSend("PASS", Password);
                        DataSend("NICK", Nick);
                        DataSend("USER", Nick);
                        DataSend("CAP REQ :twitch.tv/membership", null);
                        DataSend("CAP REQ :twitch.tv/commands", null);
                        DataSend("CAP REQ :twitch.tv/tags", null);
                        //DataSend("jtvclient", null);
                        ReadStreamThread = new Thread(new ThreadStart(ReadIn));
                        ReadStreamThread.Start();
                        Timer.Start();
                        isOnline = true;
                        isDisconnected = false;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("EXCEPTION: " + e);
                        textInput("Could not connect to server");
                    }
                    finally
                    {
                        if (reader == null)
                        {
                            reader.Close();
                        }
                        if (writer == null)
                        {
                            writer.Close();
                        }
                        if (ns == null)
                        {
                            ns.Close();
                        }
                    }
                }
                if (RememberMe.IsChecked == true)
                {
                    isolatedStorage = IsolatedStorageFile.GetUserStoreForAssembly();
                    using (StreamWriter srWriter = new StreamWriter(new IsolatedStorageFileStream(isoFile, FileMode.OpenOrCreate, isolatedStorage)))
                    {
                        srWriter.WriteLine(Nick);
                        srWriter.WriteLine(Channel);
                        srWriter.WriteLine(Password);
                    }
                }
            }
            else
            {
                if (MessageBox.Show("You need to generate an oauth twitch password at http://twitchapps.com/tmi/",
                    "Oauth Password Error",
                    MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                {
                    Process.Start("http://twitchapps.com/tmi/");
                }
            }
        }

        #region chat methods


        public void textInput(string text)
        {
            var emoteList = EmoteList.CheckTextForEmotes(text);
            Paragraph para = new Paragraph();
            para.LineHeight = 1;
            TextRange timeStamp = new TextRange(para.ContentStart, para.ContentStart);
            var tempDate = DateTime.Now;
            if (DateFormat != "")
            {
                try
                {
                    timeStamp.Text = tempDate.ToString(DateFormat);
                }
                catch (Exception) { }
            }
            para.Foreground = TextColor;
            para.Inlines.Add(text);
            EveryInput.Add(para, "");
            ServerChatArea.Document.Blocks.Add(para);
            ServerChatArea.ScrollToEnd();
        }
        
        private void updateUserList()
        {
            Text_UserList.Document.Blocks.Clear();
            var tempList = new List<string>();
            foreach (var item in userList.userList)
            {
                string tempName = "";
                if (item.IsMod)
                {
                    tempName += "@";
                }
                tempName += item.UserName;
                tempList.Add(tempName);
            }
            tempList.Sort();
            foreach (var item in tempList)
            {
                var temp = new Paragraph();
                temp.LineHeight = 1;
                temp.Inlines.Add(item);
                temp.ContextMenuOpening += new ContextMenuEventHandler(UserClick);
                Text_UserList.Document.Blocks.Add(temp);
            }
        }

        #endregion

        #region irc methods

        private void UpdateText_Tick(object sender, EventArgs e)
        {
            if (isDisconnected)
            {
                Part();
            }
            if (!IsLineRead && isOnline)
            {
                var input = new ServerInput(LineFromReader);
                
                if (input.Type == MesseageType.Server)
                {
                    textInput(input.Messeage);
                }
                if (input.Type == MesseageType.Channel)
                {
                    try
                    {
                        Tabs.First(x => x.Channel == input.Property).ServerInput(LineFromReader);
                    }
                    //empty catch if messeage comes in after closing tab but before closing connection
                    catch { }
                }
                if (input.Type == MesseageType.Ping)
                {
                    PingHandler();
                }
                if (input.Type == MesseageType.ClearChat)
                {
                    try
                    {
                        Tabs.First(x => x.Channel == input.Property).ClearText(LineFromReader);
                    }
                    catch { }
                }
                IsLineRead = true;
            }
        }

        private void PingHandler()
        {
            words = LineFromReader.Split(ChatSeperator);
            if (words[0] == "PING")
            {
                DataSend("PONG", words[1]);
            }
        }

        private void ReadIn()
        {
            while (true && isOnline)
            {
                try
                {
                    if (IsLineRead && reader != null)
                    {
                        LineFromReader = reader.ReadLine();
                        Console.WriteLine(LineFromReader);
                        if (LineFromReader != null)
                        {
                            IsLineRead = false;
                        }
                    }
                    Thread.Sleep(Timer.Interval);
                }
                catch (Exception e)
                {
                    isDisconnected = true;
                    break;
                }
            }
        }

        public void DataSend(string cmd, string param)
        {
            if (param == null)
            {
                writer.WriteLine(cmd);
                writer.Flush();
            }
            else
            {
                writer.WriteLine(cmd + ' ' + param);
                writer.Flush();
            }
        }

        #endregion

        private void Join(string channel)
        {
            if (isOnline)
            {
                if (!channel.StartsWith("#"))
                    channel = "#" + channel;
                if (Tabs.Where(x => x.Channel == channel).Count() == 0)
                {
                    DataSend("JOIN", channel);
                    var index = Tabs.Count() + 1;
                    var newTab = new TabWindow(channel, this, index, BackgroundChatColor);
                    TabControl.Items.Add(newTab);
                    Tabs.Add(newTab);
                    TabControl.SelectedIndex = index;
                }
            }
        }

        private void Part()
        {
            if (CurrentTab > 0)
            {
                var currentTab = TabControl.SelectedIndex - 1;
                Tabs[currentTab].Part();
                TabControl.Items.Remove(Tabs[currentTab]);
                var newTab = TabControl.SelectedIndex;
                if (newTab == 0)
                {
                    UpdateSelected(EmptyUserList);
                }
                else
                {
                    UpdateSelected(Tabs[newTab - 1].UserList, TabIndex);
                }
                Tabs.RemoveAt(currentTab);
            }
        }

        private void Disconnect()
        {
            if (isOnline || isDisconnected)
            {
                if (isDisconnected)
                    textInput("Disconnected from server");
                else
                {
                    IsLineRead = false;
                    ReadStreamThread.Abort();
                    Timer.Stop();
                    writer.Flush();
                    writer.Close();
                    reader.Close();
                }
                isOnline = false;
            }
        }

        #region window commands

        private void button_join_Click(object sender, RoutedEventArgs e)
        {
            Join(text_chan.Text);
        }

        private void button_part_Click(object sender, RoutedEventArgs e)
        {
            Part();
        }

        private void button_connect_Click(object sender, RoutedEventArgs e)
        {
            Connect();
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && isOnline)
            {
                var input = textBox1.Text;
                if (!string.IsNullOrWhiteSpace(input))
                {
                    if (input.ToLower().StartsWith(@"/join"))
                    {
                        var temp = input.Split(' ');
                        if (temp.Count() == 2)
                        {
                            Join(temp[1]);
                        }
                    }
                    else if (input.ToLower().StartsWith(@"/part") && CurrentTab > 0)
                    {
                        Part();
                    }
                    else if (CurrentTab > 0)
                    {
                        Tabs[TabControl.SelectedIndex - 1].SendMessage(input);
                    }

                    if (IsCurrent && Current != "")
                        Up.Push(Current);
                    MoveDownToUp();
                    IsCurrent = false;
                    Up.Push(textBox1.Text);
                    KeepStackUnderMax();
                }
                textBox1.Clear();
            }
            if (e.Key == Key.Up)
            {
                textBox1.Text = Ups();
                IsCurrent = true;
            }
            if (e.Key == Key.Down)
            {
                textBox1.Text = Downs();
                IsCurrent = true;
            }
        }

        #region stack methods

        private void KeepStackUnderMax()
        {
            if (Up.Count() > MAXSTACKSAVE)
            {
                var temp = Up.ToList();
                while (Up.Count() != 0)
                    Up.Pop();
                temp.Reverse();
                temp.RemoveAt(0);
                foreach (var item in temp)
                {
                    Up.Push(item);
                }
            }
        }

        private void MoveDownToUp()
        {
            while (Down.Count != 0)
            {
                var temp = Down.Pop();
                if(temp != "")
                    Up.Push(temp);
            }
            Down.Push("");
        }

        private string Ups()
        {
            if (Up.Count() != 0)
            {
                var temp = Up.Pop();
                if (IsCurrent)
                {
                        Down.Push(Current);
                }
                Current = temp;
            }
            return Current;
        }

        private string Downs()
        {
            if (Down.Count() != 0)
            {
                var temp = Down.Pop();
                if (IsCurrent)
                {
                    Up.Push(Current);
                }
                Current = temp;
            }
            return Current;
        }

#endregion

        private void Button_Clear_Click(object sender, RoutedEventArgs e)
        {
            var current = TabControl.SelectedIndex;
            if (current == 0)
            {
                ServerChatArea.Document.Blocks.Clear();
            }
            else
            {
                Tabs[current - 1].Clear();
            }
        }


        #endregion

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            this.SetPlacement(Settings.Default.MainWIndowPlacement);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Settings.Default.MainWIndowPlacement = this.GetPlacement();
            Settings.Default.Save();
            Tabs.ForEach(x => x.Part());
            Disconnect();
            Application.Current.Shutdown();
        }


        public void OnServerTabSelected(object sender, RoutedEventArgs e)
        {
            UpdateSelected(EmptyUserList);
        }

        public void UserClick(object sender, RoutedEventArgs e)
        {
            //temp.ContextMenuOpening += new ContextMenuEventHandler(UserClick);
            var inLines = ((Paragraph)sender).Inlines;
            if (inLines.Count() == 1)
            {
                var user = ((Run)inLines.First()).Text;
                if (user.StartsWith("@"))
                    user = user.Substring(1);
                ContextMenuUser = user;
            }
            else
            {
                var user = ((Run)inLines.First(x=> !((Run)x).Text.Contains("<"))).Text;
                if(user == ">")
                {
                    user = ((Run)inLines.First(x => ((Run)x).Text.Contains("<"))).Text.Split('<').Last();
                }
                ContextMenuUser = user;
            }
            Console.WriteLine(sender);
            ContextMenu myMenu = new ContextMenu();

            MenuItem userName = new MenuItem();
            userName.Header = ContextMenuUser;
            myMenu.Items.Add(userName);

            MenuItem ban = new MenuItem();
            ban.Header = "Ban";
            ban.Click += new RoutedEventHandler(ban_Click);
            MenuItem promote = new MenuItem();
            promote.Header = "Promote";
            promote.Click += new RoutedEventHandler(promote_Click);
            MenuItem demote = new MenuItem();
            demote.Header = "Demote";
            demote.Click += new RoutedEventHandler(demote_Click);
            MenuItem timeout = new MenuItem();
            timeout.Header = "Timeout";
            MenuItem t1 = new MenuItem();
            t1.Header = "1";
            t1.Click += new RoutedEventHandler(t1_Click);
            timeout.Items.Add(t1);
            MenuItem t60 = new MenuItem();
            t60.Header = "60";
            t60.Click += new RoutedEventHandler(t60_Click);
            timeout.Items.Add(t60);
            MenuItem t300 = new MenuItem();
            t300.Header = "300";
            t300.Click += new RoutedEventHandler(t300_Click);
            timeout.Items.Add(t300);
            MenuItem t600 = new MenuItem();
            t600.Header = "600";
            t600.Click += new RoutedEventHandler(t600_Click);
            timeout.Items.Add(t600);

            myMenu.Items.Add(timeout);
            myMenu.Items.Add(ban);
            myMenu.Items.Add(promote);
            myMenu.Items.Add(demote);

            ShowMenu(myMenu);
        }

        #region context menu clicks

        void ban_Click(object sender, RoutedEventArgs e)
        {
            SendMessage(".ban " + ContextMenuUser.Replace("@", ""));
        }

        void t600_Click(object sender, RoutedEventArgs e)
        {
            SendMessage(".timeout " + ContextMenuUser.Replace("@", "") + " 600");
        }

        void t300_Click(object sender, RoutedEventArgs e)
        {
            SendMessage(".timeout " + ContextMenuUser.Replace("@", "") + " 300");
        }

        void t60_Click(object sender, RoutedEventArgs e)
        {
            SendMessage(".timeout " + ContextMenuUser.Replace("@", "") + " 60");
        }

        void t1_Click(object sender, RoutedEventArgs e)
        {
            SendMessage(".timeout " + ContextMenuUser.Replace("@", "") + " 1");
        }

        void demote_Click(object sender, RoutedEventArgs e)
        {
            SendMessage(".unmod " + ContextMenuUser.Replace("@", ""));
        }

        void promote_Click(object sender, RoutedEventArgs e)
        {
            SendMessage(".mod " + ContextMenuUser.Replace("@", ""));
        }

        #endregion

        private void ShowMenu(ContextMenu menu)
        {
            menu.Placement = PlacementMode.MousePoint;
            menu.PlacementRectangle = new Rect(0.0, 0.0, 0.0, 0.0);
            menu.IsOpen = true;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Config config = new Config();
            config.Show();
        }

        public void SaveConfig(Config config)
        {
            BackgroundChatColor = config.BackgroundChatColor;
            BackgroundUserColor = config.BackgroundUserColor;
            BackgroundTextBoxColor = config.BackgroundTextBoxColor;
            UserColor = config.UserColor;
            TextColor = config.TextColor;
            JoinPartColor = config.JoinPartColor;
            TextBoxTextColor = config.TextBoxTextColor;
            DateFormat = config.DateFormat;
            ShowJoinPart = config.ShowJoinPart;
            FlashOnUser = config.FlashOnUser;
            FlashOnText = config.FlashOnText;
            KeepOnTop = config.KeepOnTop;
            ApplySettings();
        }


        private void Window_Activated(object sender, EventArgs e)
        {
            this.StopFlashingWindow();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            if (KeepOnTop)
            {
                this.Topmost = true;
                //this.Activate();
            }
            else
            {
                this.Topmost = false;
            }
        }


    }
}

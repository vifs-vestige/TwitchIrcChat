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
        Emotes emotes;
        static string Server = "irc.twitch.tv";
        static int Port = 6667;
        string Channel, Nick, Password, FormatedMessage, ReplyingUser;
        string LineFromReader = "";
        StreamReader reader = null;
        StreamWriter writer = null;
        NetworkStream ns = null;
        bool IsLineRead = true;
        DispatcherTimer Timer;
        UserList userList;
        char[] chatSeperator = { ' ' };
        public string[] words;
        TcpClient IRCconnection = null;
        Thread ReadStreamThread;
        bool isOnline = false;
        Dictionary<Paragraph, String> EveryInput;
        IsolatedStorageFile isolatedStorage;
        string isoFile = "isoFile";
        const string RegexHyperLink = @"(https?:\/\/)?([\da-z\.-]+)\.([a-z\.]{2,6})([^\s]*)";
        private string ContextMenuUser = "";
        private string Current = "";
        private bool IsCurrent = false;
        private Stack<string> Up = new Stack<string>();
        private Stack<string> Down = new Stack<string>();
        private const int MAXSTACKSAVE = 30;
        private SolidColorBrush BackgroundChatColor;
        private SolidColorBrush BackgroundUserColor;
        private SolidColorBrush BackgroundTextBoxColor;
        private SolidColorBrush TextColor;
        private string DateFormat;
        private bool ShowJoinPart;
        private SolidColorBrush JoinPartColor;
        private SolidColorBrush TextBoxTextColor;
        private SolidColorBrush UserColor;
        private bool FlashOnUser;
        private bool FlashOnText;

        public MainWindow()
        {
            Down.Push("");
            EveryInput = new Dictionary<Paragraph, string>();
            InitializeComponent();
            emotes = new Emotes();
            image_test.Width = 0;
            Timer = new DispatcherTimer();
            Timer.Interval = TimeSpan.FromMilliseconds(200);
            Timer.Tick += new EventHandler(UpdateText_Tick);
            //Timer.Start();
            userList = new UserList();
            Text_UserList.Document.PageWidth = 1000;
            isolatedStorage = IsolatedStorageFile.GetUserStoreForAssembly();
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
            ApplySettings();
        }

        private void ApplySettings()
        {
            chat_area.Background = BackgroundChatColor;
            Text_UserList.Background = BackgroundUserColor;
            Text_UserList.Foreground = UserColor;
            textBox1.Background = BackgroundTextBoxColor;
            textBox1.Foreground = TextBoxTextColor;
        }



        private void SendMessage(string message)
        {
            if (message.StartsWith(@"/"))
                DataSend(message.Replace(@"/", ""), null);
            else
                DataSend("PRIVMSG ", Channel + " :" + message);
            textInput(message, Nick);
        }

        private void Connect()
        {
            try
            {
                IRCconnection = new TcpClient(Server, Port);
            }
            catch
            {
                Paragraph para = new Paragraph();
                para.Inlines.Add("Connection Failed");
                chat_area.Document.Blocks.Add(para);
                chat_area.ScrollToEnd();
            }
            try
            {
                ns = IRCconnection.GetStream();
                reader = new StreamReader(ns);
                writer = new StreamWriter(ns);
                DataSend("PASS", Password);
                DataSend("NICK", Nick);
                DataSend("USER", Nick);
                DataSend("JOIN", Channel);
                ReadStreamThread = new Thread(new ThreadStart(this.ReadIn));
                ReadStreamThread.Start();
                Console.WriteLine("ok");
            }
            catch
            {
                chat_area.AppendText("Communication Error");
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

        #region chat methods

        private Image AddImageToPara(string text)
        {
            var emoteImgSrc = emotes.EmoteList[text];
            BitmapImage bitmap = new BitmapImage(new Uri(@"/Emotes/" + emoteImgSrc, UriKind.RelativeOrAbsolute));
            Image image = new Image();
            image.Source = bitmap;
            image.Width = 30;
            image_test.Source = bitmap;

            return image;
        }


        private void textInput(string text, string user = "")
        {
            var emoteList = emotes.CheckTextForEmotes(text);
            Paragraph para = new Paragraph();
            para.LineHeight = 1;
            if (user.Length > 1)
            {
                TextRange end = new TextRange(para.ContentStart, para.ContentStart);
                end.Text = ">";
                TextRange tr = new TextRange(para.ContentStart, para.ContentStart);
                tr.Text = user;
                tr.ApplyPropertyValue(TextElement.ForegroundProperty, userList.getColor(user));
                TextRange start = new TextRange(para.ContentStart, para.ContentStart);
                start.Text = "<";
                start.ApplyPropertyValue(TextElement.ForegroundProperty, TextColor);
            }
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
            para = addImageAndHyperLinks(text, para);
            if (user != "" && user != "jtv")
            {
                ContextMenuUser = user;
                para.ContextMenuOpening += new ContextMenuEventHandler(UserClick);
            }
            para.Foreground = TextColor;
            EveryInput.Add(para, user);
            chat_area.Document.Blocks.Add(para);
            chat_area.ScrollToEnd();
        }

        private bool checkSpaces(string input, int start, int end)
        {
            var isValid = true;
            if (start != 0)
            {
                if (input[start - 1] != ' ')
                {
                    isValid = false;
                }
            }
            if (end != input.Length)
            {
                if (input[end] != ' ')
                {
                    isValid = false;
                }
            }
            return isValid;
            
        }

        private Paragraph addImageAndHyperLinks(string input, Paragraph para)
        {

            var emoteList = emotes.CheckTextForEmotes(input);
            emoteList.Add(RegexHyperLink);
            var paraItems = new List<ParaInfo>();
            foreach (var item in emoteList)
            {
                var newItem = item;
                if (item != RegexHyperLink)
                {
                    newItem = newItem.Replace("\\","\\\\").Replace(")", "\\)").Replace("(", "\\(");
                }
                Regex r = new Regex(newItem);
                var matches = r.Matches(input);
                for (int i = 0; i < matches.Count; i++)
                {
                    var tempInfo = new ParaInfo();
                    tempInfo.start = matches[i].Index;
                    tempInfo.end = matches[i].Length + tempInfo.start;
                    if (checkSpaces(input, tempInfo.start, tempInfo.end))
                    {
                        tempInfo.item = item;
                        if (item == RegexHyperLink)
                        {
                            tempInfo.item = matches[i].Value;
                            tempInfo.isHyper = true;
                        }
                        paraItems.Add(tempInfo);
                    }
                }
            }
            int tracker = 0;
            foreach (var item in paraItems.OrderBy(x => x.start))
            {
                para.Inlines.Add(input.Substring(tracker, item.start - tracker));
                if (item.isHyper)
                {
                    para.Inlines.Add(addHyperLink(item.item));
                }
                else
                {
                    para.Inlines.Add(AddImageToPara(item.item));
                }
                tracker = item.end;
            }
            //if (paraItems.Count == 0)
            //{
            //    para.Inlines.Add(input);
            //}
            para.Inlines.Add(input.Substring(tracker, input.Length - tracker));
            return para;
        }

        private Inline addHyperLink(string text)
        {
            Inline stuff;
            Hyperlink link = new Hyperlink(new Run(text));
            stuff = link;
            try
            {
                link.NavigateUri = new Uri(text);
            }
            catch (UriFormatException e)
            {
                try
                {
                    link.NavigateUri = new Uri("http://" + text);
                }
                catch
                {
                    stuff = new Run(text);
                }
            }
            link.RequestNavigate += (sender, e) =>
            {
                Process.Start(e.Uri.ToString());
            };
            return stuff;
        }

        public void displayImage(string img)
        {
            BitmapImage bitmap = new BitmapImage(new Uri(@"/Emotes/" + img + ".png", UriKind.RelativeOrAbsolute));
            Image image = new Image();
            image.Source = bitmap;
            image.Width = 25;
            image_test.Source = bitmap;
            chat_area.ScrollToEnd();
            chat_area.AllowDrop = true;
        }

        private void parseColor(string input)
        {
            var temp = input.Split(' ');
            var user = temp[4];
            var color = temp.Last();
            userList.setColor(user, color);
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

        //timer that loops to post read in lines from the stream
        private void UpdateText_Tick(object sender, EventArgs e)
        {
            if (!IsLineRead && isOnline)
            {
                Console.WriteLine("STUFF");
                //if (LineFromReader != null)
                //    PostText(LineFromReader, Brushes.Gray);
                Console.WriteLine(LineFromReader);
                if (LineFromReader.Contains("PRIVMSG"))
                {
                    if (LineFromReader.Contains(":USERCOLOR"))
                    {
                        parseColor(LineFromReader);
                    }
                    else if(LineFromReader.Contains(":CLEAR")){
                        ClearText(LineFromReader);
                    }
                    else
                    {
                        WordSplitter();
                    }
                }
                else if (LineFromReader.Contains("PING"))
                {
                    PingHandler();
                }
                else if (LineFromReader.Contains("tmi.twitch.tv 353"))
                {
                    string[] tempUsers;
                    tempUsers = LineFromReader.Split(' ');
                    for (int i = 5; i < tempUsers.Length; i++)
                    {
                        string temp;
                        if (tempUsers[i].Contains(":"))
                        {
                            temp = tempUsers[i].Substring(1);
                        }
                        else
                        {
                            temp = tempUsers[i];
                        }
                        userList.Add(temp);
                    }
                    updateUserList();
                }
                //else if ((LineFromReader.ToLower().Contains("mode ")) && !(LineFromReader.ToLower().Contains(" +o")))
                //{
                //    string[] tempMods;
                //    tempMods = LineFromReader.Split(' ');
                //    UserList.Add(tempMods[tempMods.Length - 1]);
                //    //chat_area.AppendText(LineFromReader + "\r\n");
                //    textInput(LineFromReader);
                //}
                else if ((LineFromReader.ToLower().Contains("mode ")) && (LineFromReader.ToLower().Contains(" +o")))
                {
                    string[] tempMods;
                    tempMods = LineFromReader.Split(' ');
                    string tempMod = tempMods[tempMods.Length - 1];
                    userList.Add(tempMod);
                    userList.AddMod(tempMod);
                    updateUserList();
                    //UserList.Add(tempMods[tempMods.Length - 1]);
                    //chat_area.AppendText(LineFromReader + "\r\n");
                    //textInput(LineFromReader);
                }
                else if (LineFromReader.Contains("PART"))
                {
                    var tempUsername = LineFromReader.Split('!')[0];
                    tempUsername = tempUsername.Substring(1);
                    userList.Remove(tempUsername);
                    if(ShowJoinPart == true)
                        PostText("-Parts- " + tempUsername, JoinPartColor);
                    updateUserList();
                }
                else if (LineFromReader.Contains("JOIN"))
                {
                    var tempUsername = LineFromReader.Split('!')[0];
                    tempUsername = tempUsername.Substring(1);
                    userList.Add(tempUsername);
                    if (ShowJoinPart == true)
                        PostText("-Joins- " + tempUsername, JoinPartColor);
                    //textInput("-Joins- " + tempUsername);
                    updateUserList();
                }
                else
                {
                    //chat_area.AppendText(LineFromReader + "\r\n");
                    //textInput(LineFromReader);
                }
                IsLineRead = true;
            }
        }

        private void ClearText(string text)
        {
            var user = text.Split(' ')[4];
            //var ok = chat_area.Document.Blocks.
            var tempList = new List<Paragraph>();
            foreach (var item in EveryInput.Where(s => s.Value == user))
            {
                tempList.Add(item.Key);
                chat_area.Document.Blocks.Remove(item.Key);
            }
            foreach (var item in tempList)
            {
                EveryInput.Remove(item);
            }
        }

        private void PostText(string text, SolidColorBrush color)
        {
            Paragraph para = new Paragraph();

            para.Inlines.Add(text);


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


            TextRange tr = new TextRange(para.ContentStart, para.ContentEnd);
            tr.ApplyPropertyValue(TextElement.ForegroundProperty, color);
            //tr.Text = text;
            para.LineHeight = 1;
            chat_area.Document.Blocks.Add(para);
            chat_area.ScrollToEnd();
        }

        private void WordSplitter()
        {
            try
            {
                words = LineFromReader.Split(chatSeperator, 4);
                ReplyingUser = words[0].Remove(words[0].IndexOf('!')).TrimStart(':');
                //FormatedMessage = words[3].TrimStart(':');
                FormatedMessage = words[3].Remove(0, 1);
                //chat_area.AppendText("<" + replyingUser + "> " + formatedMessage + "\r\n");
                textInput(FormatedMessage, ReplyingUser);
                if (FlashOnText)
                    this.FlashWindow();
                else if (FormatedMessage.ToLower().Contains(Nick) && FlashOnUser)
                    this.FlashWindow();
                //KeywordDetector();
            }
            catch (Exception)
            {
                Console.WriteLine("hello");
            }
        }

        #endregion

        #region irc methods

        private void PingHandler()
        {
            words = LineFromReader.Split(chatSeperator);
            if (words[0] == "PING")
            {
                DataSend("PONG", words[1]);
            }
        }

        private void ReadIn()
        {
            while (true && isOnline)
            {
                if (IsLineRead && reader != null)
                {
                    LineFromReader = reader.ReadLine();
                    //Console.WriteLine(LineFromReader);
                    if (LineFromReader != null)
                    {
                        IsLineRead = false;
                    }
                }
                Thread.Sleep(Timer.Interval);
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

        private void Join()
        {
            if (text_pass.Password.Contains("oauth"))
            {
                Nick = text_user.Text.ToLower();
                Password = text_pass.Password.ToString();
                Channel = "#" + text_chan.Text.ToString();
                if (Nick.Length > 1 && Password.Length > 1 && Channel.Length > 1)
                {
                    isOnline = true;
                    Connect();
                    DataSend("jtvclient", null);
                }
                Timer.Start();
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

        #region window commands

        private void button_join_Click(object sender, RoutedEventArgs e)
        {
            Join();
        }

        private void button_part_Click(object sender, RoutedEventArgs e)
        {
            Part();
        }

        private void Part()
        {
            if (isOnline)
            {
                isOnline = false;
                IsLineRead = false;
                DataSend("PART", Channel);
                userList.Clear();
                Text_UserList.Document.Blocks.Clear();
                ReadStreamThread.Abort();
                Timer.Stop();
                writer.Flush();
                writer.Close();
                reader.Close();
                writer = null;
                reader = null;
            }
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            Console.WriteLine("stuff");
            if (e.Key == Key.Enter && isOnline)
            {
                if (!string.IsNullOrWhiteSpace(textBox1.Text))
                {
                    SendMessage(textBox1.Text);
                    if (IsCurrent && Current != "")
                        Up.Push(Current);
                    MoveDownToUp();
                    IsCurrent = false;
                    Up.Push(textBox1.Text);
                    KeepStackUnderMax();
                    textBox1.Clear();
                }
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
            chat_area.Document.Blocks.Clear();
        }


        #endregion

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Part();
            Application.Current.Shutdown();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Part();
            Application.Current.Shutdown();
        }

        private void Window_KeyUpJoin(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !isOnline)
            {
                Join();
            }
        }

        private void UserClick(object sender, RoutedEventArgs e)
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
            SendMessage(".ban " + ContextMenuUser);
        }

        void t600_Click(object sender, RoutedEventArgs e)
        {
            SendMessage(".timeout " + ContextMenuUser + " 600");
        }

        void t300_Click(object sender, RoutedEventArgs e)
        {
            SendMessage(".timeout " + ContextMenuUser + " 300");
        }

        void t60_Click(object sender, RoutedEventArgs e)
        {
            SendMessage(".timeout " + ContextMenuUser + " 60");
        }

        void t1_Click(object sender, RoutedEventArgs e)
        {
            SendMessage(".timeout " + ContextMenuUser + " 1");
        }

        void demote_Click(object sender, RoutedEventArgs e)
        {
            SendMessage(".unmod " + ContextMenuUser);
        }

        void promote_Click(object sender, RoutedEventArgs e)
        {
            SendMessage(".mod " + ContextMenuUser);
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
            ApplySettings();
        }


        private void Window_Activated(object sender, EventArgs e)
        {
            this.StopFlashingWindow();
        }

    }
}

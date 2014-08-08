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
        //const string RegexHyperLink = @"(https?:\/\/)?([\da-z\.-]+)\.([a-z\.]{2,6})([\/\w \.-]*)*.*";
        const string RegexHyperLink = @"(https?:\/\/)?([\da-z\.-]+)\.([a-z\.]{2,6})([\/\w\.-]*)";

        public MainWindow()
        {
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
                start.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.White);
            }
            TextRange timeStamp = new TextRange(para.ContentStart, para.ContentStart);
            var tempDate = DateTime.Now;
            if (DateFormat.Text != "")
            {
                try
                {
                    timeStamp.Text = tempDate.ToString(DateFormat.Text);
                }
                catch (Exception) { }
            }
            para = addImageAndHyperLinks(text, para);
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
                    if(ShowJoinPart.IsChecked == true)
                        PostText("-Parts- " + tempUsername, Brushes.LightGreen);
                    updateUserList();
                }
                else if (LineFromReader.Contains("JOIN"))
                {
                    var tempUsername = LineFromReader.Split('!')[0];
                    tempUsername = tempUsername.Substring(1);
                    userList.Add(tempUsername);
                    if (ShowJoinPart.IsChecked == true)
                        PostText("-Joins- " + tempUsername, Brushes.LightGreen);
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
            if (DateFormat.Text != "")
            {
                try
                {
                    timeStamp.Text = tempDate.ToString(DateFormat.Text);
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
                Nick = text_user.Text.ToString();
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
            if (e.Key == Key.Enter && isOnline)
            {
                //textInput(textBox1.Text);
                SendMessage(textBox1.Text);
                textBox1.Clear();
                //way to open links
                //Process.Start("http://www.google.com");
            }
        }

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


    }
}

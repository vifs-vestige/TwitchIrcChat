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
    class TabWindow : TabItem
    {
        private MainWindow Main;
        private RichTextBox Rtb;
        public UserList UserList;
        public string Channel;
        private StreamReader reader = null;
        private StreamWriter writer = null;
        private bool IsLineRead = true;
        private DispatcherTimer Timer;
        private bool IsOnline = false;
        private bool IsDisconnected = false;
        private Thread ReadStreamThread;
        private string LineFromReader = "";
        private Dictionary<Paragraph, string> EveryInput;
        private string[] words;
        private string ReplyingUser;
        private string FormatedMessage;
        private int Index;

        public void Clear()
        {
            this.Rtb.Document.Blocks.Clear();
        }

        public TabWindow(string channel, MainWindow mainWindow, int index, SolidColorBrush background)
        {
            Index = index;
            UserList = new UserList();
            Channel = channel;
            Header = Channel;
            Rtb = new RichTextBox();
            Rtb.FontSize = 15;
            Main = mainWindow;
            Rtb.Background = Brushes.Black;
            Rtb.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
            Rtb.IsReadOnly = true;
            Rtb.IsDocumentEnabled = true;
            Rtb.Background = background;
            this.AddChild(Rtb);
            Timer = new DispatcherTimer();
            Timer.Interval = TimeSpan.FromMilliseconds(200);
            Timer.Tick += new EventHandler(UpdateText_Tick);
            EveryInput = new Dictionary<Paragraph, string>();
            Join();
        }

        public void SetIndex(int index)
        {
            this.TabIndex = index;
        }

        public void UpdateColor(SolidColorBrush background)
        {
            Rtb.Background = background;
        }

        protected override void OnSelected(RoutedEventArgs e)
        {
            base.OnSelected(e);
            this.Foreground = Brushes.Black;
            Main.UpdateSelected(UserList, Index);
        }

        private void WordSplitter()
        {
            try
            {
                words = LineFromReader.Split(Main.ChatSeperator, 4);
                ReplyingUser = words[0].Remove(words[0].IndexOf('!')).TrimStart(':');
                FormatedMessage = words[3].Remove(0, 1);
                ParaOutput(FormatedMessage, ReplyingUser);
                if (Main.FlashOnText && !Main.KeepOnTop)
                    Main.FlashWindow();
                else if (FormatedMessage.ToLower().Contains(Main.Nick) && Main.FlashOnUser && !Main.KeepOnTop)
                    Main.FlashWindow();
                //KeywordDetector();
            }
            catch (Exception)
            {
                Console.WriteLine("hello");
            }
        }

        private void ParaOutput(string input, string user = "")
        {
            var emoteList = Main.EmoteList.CheckTextForEmotes(input);
            Paragraph para = new Paragraph();
            para.LineHeight = 1;
            if (user.Length > 1)
            {
                TextRange end = new TextRange(para.ContentStart, para.ContentStart);
                end.Text = ">";
                TextRange tr = new TextRange(para.ContentStart, para.ContentStart);
                tr.Text = user;
                tr.ApplyPropertyValue(TextElement.ForegroundProperty, UserList.getColor(user));
                TextRange start = new TextRange(para.ContentStart, para.ContentStart);
                start.Text = "<";
                start.ApplyPropertyValue(TextElement.ForegroundProperty, Main.TextColor);
            }
            TextRange timeStamp = new TextRange(para.ContentStart, para.ContentStart);
            var tempDate = DateTime.Now;
            if (Main.DateFormat != "")
            {
                try
                {
                    timeStamp.Text = tempDate.ToString(Main.DateFormat);
                }
                catch (Exception) { }
            }
            para = AddImageAndHyperLinks(input, para);
            if (user != "" && user != "jtv")
            {
                Main.ContextMenuUser = user;
                para.ContextMenuOpening += new ContextMenuEventHandler(Main.UserClick);
            }
            para.Foreground = Main.TextColor;
            EveryInput.Add(para, user);
            Rtb.Document.Blocks.Add(para);
            Rtb.ScrollToEnd();
            ShowUpdated();
        }

        private void ShowUpdated()
        {
            if (!IsSelected)
                this.Foreground = Brushes.OrangeRed;
        }

        private Paragraph AddImageAndHyperLinks(string input, Paragraph para)
        {
            var emoteList = Main.EmoteList.CheckTextForEmotes(input);
            emoteList.Add(MainWindow.RegexHyperLink);
            var paraItems = new List<ParaInfo>();
            foreach (var item in emoteList)
            {
                var newItem = item;
                Regex r;
                if (item != MainWindow.RegexHyperLink)
                {
                    newItem = newItem.Replace("\\", "\\\\").Replace(")", "\\)").Replace("(", "\\(");
                    r = new Regex(newItem);
                }
                else
                {
                    r = new Regex(newItem, RegexOptions.IgnoreCase);
                }
                var matches = r.Matches(input);
                for (int i = 0; i < matches.Count; i++)
                {
                    var tempInfo = new ParaInfo();
                    tempInfo.start = matches[i].Index;
                    tempInfo.end = matches[i].Length + tempInfo.start;
                    if (CheckSpaces(input, tempInfo.start, tempInfo.end))
                    {
                        tempInfo.item = item;
                        if (item == MainWindow.RegexHyperLink)
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
            para.Inlines.Add(input.Substring(tracker, input.Length - tracker));
            return para;
        }

        private bool CheckSpaces(string input, int start, int end)
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

        private Image AddImageToPara(string text)
        {
            var emoteImgSrc = Main.EmoteList.EmoteList[text];
            BitmapImage bitmap = new BitmapImage(new Uri(@"/Emotes/" + emoteImgSrc, UriKind.RelativeOrAbsolute));
            Console.WriteLine(bitmap.UriSource);
            Image image = new Image();
            image.Source = bitmap;
            image.Width = 30;
            Main.image_test.Source = bitmap;
            return image;
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

        private void UpdateText_Tick(object sender, EventArgs e)
        {
            if (IsDisconnected)
            {
                Part();
            }
            if (!IsLineRead && IsOnline)
            {
                Console.WriteLine(LineFromReader);
                if (LineFromReader.Contains("PRIVMSG"))
                {
                    if (LineFromReader.Contains(":USERCOLOR"))
                    {
                        ParseColor(LineFromReader);
                    }
                    else if (LineFromReader.Contains(":CLEAR"))
                    {
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
                        UserList.Add(temp);
                        UpdateIfSelected();
                    }
                }
                else if ((LineFromReader.ToLower().Contains("mode ")) && (LineFromReader.ToLower().Contains(" +o")))
                {
                    string[] tempMods;
                    tempMods = LineFromReader.Split(' ');
                    string tempMod = tempMods[tempMods.Length - 1];
                    UserList.Add(tempMod);
                    UserList.AddMod(tempMod);
                    UpdateIfSelected();
                }
                else if (LineFromReader.Contains("PART"))
                {
                    var tempUsername = LineFromReader.Split('!')[0];
                    tempUsername = tempUsername.Substring(1);
                    UserList.Remove(tempUsername);
                    UpdateIfSelected();
                    if (Main.ShowJoinPart == true)
                        PostText("-Parts- " + tempUsername, Main.JoinPartColor);
                }
                else if (LineFromReader.Contains("JOIN"))
                {
                    var tempUsername = LineFromReader.Split('!')[0];
                    tempUsername = tempUsername.Substring(1);
                    UserList.Add(tempUsername);
                    UpdateIfSelected();
                    if (Main.ShowJoinPart == true)
                        PostText("-Joins- " + tempUsername, Main.JoinPartColor);
                }
                IsLineRead = true;
            }
        }

        private void UpdateIfSelected()
        {
            if (this.IsSelected)
            {
                Main.UpdateSelected(UserList,Index);
            }
        }

        private void PostText(string text, SolidColorBrush color)
        {
            Paragraph para = new Paragraph();
            para.Inlines.Add(text);
            TextRange timeStamp = new TextRange(para.ContentStart, para.ContentStart);
            var tempDate = DateTime.Now;
            if (Main.DateFormat != "")
            {
                try
                {
                    timeStamp.Text = tempDate.ToString(Main.DateFormat);
                }
                //if invaled timestamp, dont put any timestamp
                catch (Exception) { }
            }
            TextRange tr = new TextRange(para.ContentStart, para.ContentEnd);
            tr.ApplyPropertyValue(TextElement.ForegroundProperty, color);
            para.LineHeight = 1;
            Rtb.Document.Blocks.Add(para);
            Rtb.ScrollToEnd();
        }

        public void SendMessage(string message)
        {
            if (message.StartsWith(@"/"))
                DataSend(message.Replace(@"/", ""), null);
            else
                DataSend("PRIVMSG ", Channel + " :" + message);
            ParaOutput(message, Main.Nick);
        }

        private void ParseColor(string input)
        {
            var temp = input.Split(' ');
            var user = temp[4];
            var color = temp.Last();
            UserList.setColor(user, color);
        }

        private void ClearText(string text)
        {
            var user = text.Split(' ')[4];
            var tempList = new List<Paragraph>();
            foreach (var item in EveryInput.Where(s => s.Value == user))
            {
                tempList.Add(item.Key);
                Rtb.Document.Blocks.Remove(item.Key);
            }
            foreach (var item in tempList)
            {
                EveryInput.Remove(item);
            }
        }

        private void PingHandler()
        {
            words = LineFromReader.Split(Main.ChatSeperator);
            if (words[0] == "PING")
            {
                DataSend("PONG", words[1]);
            }
        }

        private void DataSend(string cmd, string param)
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

        private void Join()
        {
            var nick = Main.Nick;
            var password = Main.Password;
            NetworkStream ns = null;
            try
            {
                var ircConnection = new TcpClient(MainWindow.Server, MainWindow.Port);
                ns = ircConnection.GetStream();
                reader = new StreamReader(ns);
                writer = new StreamWriter(ns);
                DataSend("PASS", password);
                DataSend("NICK", nick);
                DataSend("USER", nick);
                DataSend("JOIN", Channel);
                DataSend("jtvclient", null);
                ReadStreamThread = new Thread(new ThreadStart(this.ReadIn));
                ReadStreamThread.Start();
                Timer.Start();
                IsOnline = true;
                IsDisconnected = false;
                Main.textInput("Joined " + Channel);
            }
            catch
            {
                Rtb.AppendText("Communication Error");
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

        private void ReadIn()
        {
            while (true && IsOnline)
            {
                Console.WriteLine("Read in thread running");
                try
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
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    IsDisconnected = true;
                    break;
                }
            }
        }

        public void Part()
        {
            if (IsOnline || IsDisconnected)
            {
                if (IsDisconnected)
                {
                    Console.WriteLine("Part disconnected");
                    //todo later
                }
                else
                {
                    IsLineRead = false;
                    DataSend("PART", Channel);
                    ReadStreamThread.Abort();
                    Timer.Stop();
                    writer.Flush();
                    writer.Close();
                    reader.Close();
                }
                IsOnline = false;
                Main.textInput("Part " + Channel);
            }
        }

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;

namespace TwitchIrcChat
{
    public class User
    {
        static Random random = new Random();
        public string UserName { get; set; }
        public SolidColorBrush Color { get; set; }
        public bool IsMod { get; set; }

        public User(string username)
        {
            IsMod = false;
            UserName = username;
            randomColor();
        }

        public void setColor(SolidColorBrush color)
        {
            Color = color;
        }

        private void randomColor()
        {
            var temp = Brushes.White;
            int randomColor = random.Next(0, 10);
            switch (randomColor)
            {
                case 0:
                    temp = Brushes.Blue;
                    break;
                case 1:
                    temp = Brushes.Green;
                    break;
                case 2:
                    temp = Brushes.Red;
                    break;
                case 3:
                    temp = Brushes.Purple;
                    break;
                case 4:
                    temp = Brushes.Orange;
                    break;
                case 5:
                    temp = Brushes.Yellow;
                    break;
                case 6:
                    temp = Brushes.Gold;
                    break;
                case 7:
                    temp = Brushes.Teal;
                    break;
                case 8:
                    temp = Brushes.Cyan;
                    break;
                case 9:
                    temp = Brushes.LightBlue;
                    break;
                case 10:
                    temp = Brushes.Pink;
                    break;
            }
            Color = temp;
        }
    }

    public class UserList
    {
        public List<User> userList { get; set; }

        public UserList()
        {
            userList = new List<User>();
        }

        public void Add(string userName)
        {
            bool isInList = false;
            foreach (var item in userList)
            {
                if(item.UserName.Equals(userName)){
                    isInList = true;
                    break;
                }
            }
            if (!isInList)
            {
                var tempUser = new User(userName);
                userList.Add(tempUser);
            }
        }

        public void Remove(string userName)
        {
            int userLocation = -1;
            for (int i = 0; i < userList.Count; i++)
            {
                if (userName.Equals(userList[i].UserName))
                {
                    userLocation = i;
                    break;
                }
            }
            try
            {
                userList.RemoveAt(userLocation);
            }
            catch (Exception)
            {
            }
        }

        public SolidColorBrush getColor(string username)
        {
            var temp = Brushes.White;
            foreach (var item in userList)
            {
                if (item.UserName.Equals(username))
                {
                    temp = item.Color;
                }
            }
            return temp;
        }

        public void setColor(string username, string color)
        {
            if(userList.Count(s => s.UserName == username) == 0){
                Add(username);
            }
            var user = userList.First(s => s.UserName == username);
            var converter = new BrushConverter();
            var brush = (SolidColorBrush)converter.ConvertFromString(color);
            user.Color = brush;
        }

        public void Clear()
        {
            userList.Clear();
        }

        public void AddMod(string userName)
        {
            foreach (var item in userList)
            {
                if (item.UserName.Equals(userName))
                {
                    item.IsMod = true;
                }
            }
        }

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace TwitchIrcChat
{
    class Emotes
    {
        public Dictionary<string, string> EmoteList { get; set; }

        public Emotes()
        {
            EmoteList = new Dictionary<string, string>();
            ReadIn();
        }

        private void ReadIn()
        {
            var ok = Properties.Resources.EmoteOutputs.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var item in ok)
            {
                var splits = item.Split(' ');
                EmoteList.Add(splits[1], splits[0]);
            }
            
        }

        public List<string> CheckTextForEmotes(string text)
        {

            List<string> EmotesUsed = new List<string>();

            foreach (var item in EmoteList)
            {
                if (text.Contains(item.Key))
                {
                    EmotesUsed.Add(item.Key);
                }
            }

            return EmotesUsed;
        }
    }
}

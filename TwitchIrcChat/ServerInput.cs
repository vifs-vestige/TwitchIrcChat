using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TwitchIrcChat
{
    class ServerInput
    {
        public MesseageType Type { get; set; }
        public string Messeage { get; set; }
        public string Property { get; set; }
        private string Input;

        public ServerInput(string input)
        {
            Input = input;
            Messeage = "";
            var temp = input.Split(' ');
            if (Input.StartsWith(":tmi.twitch.tv"))
                ServerMesseage();
            else if (Input.Contains("PING"))
                Ping();
            else
                ChannelMesseage();
            
        }

        private void ServerMesseage()
        {
            Type = MesseageType.Server;
            Messeage = Input.Substring(14);
            var temp = Input.Split(' ');
            int n;
            if(int.TryParse(temp[1], out n)){
                temp[0] = "";
                temp[1] = "";
                Messeage = string.Join(" ", temp).Substring(1);
            }
        }

        private void ChannelMesseage()
        {
            Property = Input.Split(' ')[2];
            if (Property == "PRIVMSG")
                Property = Input.Split(' ')[3];
            if (!Property.StartsWith("#"))
                Property = "#" + Property;
            Type = MesseageType.Channel;
        }

        private void Ping()
        {
            Type = MesseageType.Ping;
        }
    }

    enum MesseageType {Server, Channel, Ping};
}

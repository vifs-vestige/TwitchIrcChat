using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace TwitchIrcChat
{
    class MyClient : WebClient
    {
        public bool HeadOnly { get; set; }
        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest req = base.GetWebRequest(address);
            if (HeadOnly && req.Method == "GET")
            {
                req.Method = "HEAD";
            }
            return req;
        }
    }

    public static class Extentions
    {
        public static bool IsValidUrl(this string input)
        {
            try
            {
                using (var client = new MyClient())
                {
                    client.HeadOnly = true;
                    string s1 = client.DownloadString(input);
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    
}

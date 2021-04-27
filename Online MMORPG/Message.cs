using System;
using System.Collections.Generic;
using System.Text;

namespace Online_MMORPG
{
    public class Message
    {
        //uuid should be assigned by server and set for every message
        public string header;
        public int length;
        public string uuid;
        public string messageText = "";
        public string image = "";
        public string color;
    }
}

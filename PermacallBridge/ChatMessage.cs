using System;
using System.Collections.Generic;
using System.Text;

namespace PermacallBridge
{
    public class ChatMessage
    {
        public ChatMessage(string user, string message)
        {
            User = user;
            Message = message;
        }

        public string User { get; set; }
        public string Message { get; set; }
    }
}

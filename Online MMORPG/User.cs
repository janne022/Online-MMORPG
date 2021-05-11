using System;
using System.Collections.Generic;
using System.Text;

namespace Online_MMORPG
{
    class User
    {
        NewUser role;
        private string username;
        private string password;

        public string Username
        {
            get { return username; }
            set { username = value; }
        }
        public string Password
        {
            get { return password; }
            set { password = value; }
        }
    }

    class OwnerUser
    {
        OwnerRole role;
        private string username;
        private string password;
    }
}

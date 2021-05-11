using System;
using System.Collections.Generic;
using System.Text;

namespace Online_MMORPG
{
    public abstract class Role
    {
        public int hierarchyLevel;
        public string roleName;
        public string color;
        public bool canBan = false;
    }
    public class NewUser : Role
    {
    }
    public class OwnerRole : Role
    {
        public OwnerRole()
        {
            hierarchyLevel = 4;
            roleName = "Owner";
            canBan = true;
        }
    }

}

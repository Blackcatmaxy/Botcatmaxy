using System;

namespace Tests.Commands.Attributes
{
    [AttributeUsage(AttributeTargets.Field)]
    public class InsertUserAttribute : Attribute
    {
        public string UserName { get; }
        public string RoleName { get; }

        public InsertUserAttribute(string userName, string roleName = null)
        {
            UserName = userName;
            RoleName = roleName;
        }
    }
}
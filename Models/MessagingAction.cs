using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrayApp.Models
{
    internal class MessagingAction
    {
        public const string State = "state";
        public const string Read = "read";
        public const string Create = "create";
        public const string Update = "update";
        public const string Delete = "delete";
    }

    internal static class MessagingResource
    {
        public const string Credential = "credential";
        public const string Group = "group";
        public const string Session = "session";
    }
}
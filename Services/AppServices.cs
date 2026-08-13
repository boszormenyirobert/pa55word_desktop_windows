using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrayApp.Services
{
    internal class AppServices
    {
        public static QrService Qr { get; } = new QrService();
    }
}

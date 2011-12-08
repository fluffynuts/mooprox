using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace mooprox
{
    public class ProxyInfo
    {
        public string Host { get; set; }
        public string Port { get; set; }
        public string Name { get; set; }
        public ProxyInfo(string name, string host, string port)
        {
            this.Host = host;
            this.Name = name;
            this.Port = port;
        }
    }
}

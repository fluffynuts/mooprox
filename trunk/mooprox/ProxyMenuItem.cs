using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace mooprox
{
    public class ProxyMenuItem : MenuItem
    {
        public ProxyInfo proxy;
        public ProxyMenuItem(string Text, EventHandler onClick, ProxyInfo prx) :
            base(Text, onClick)
        {
            this.proxy = prx;
        }
    }

}

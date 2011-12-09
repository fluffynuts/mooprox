using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using Microsoft.Win32;
using SimpleINI;

namespace mooprox
{
    public partial class frmMain : Form
    {
        private NotifyIcon trayIcon;
        private ContextMenu trayMenu;
        private bool exiting;
        private INI ini;
        private bool _visible = false;
        private string currentEdit = null;
        private static string RegKey = "HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings";
        public frmMain()
        {
            this.trayIcon = null;
            this.trayMenu = null;
            this.InitializeComponent();
            this.InitIcon();
            this.exiting = false;

            this.RefreshProxies();
            this.lvProxies.Resize += this.ResizeColumns;

            this.txtName.GotFocus += this.SelectAllText;
            this.txtHost.GotFocus += this.SelectAllText;
            this.txtPort.GotFocus += this.SelectAllText;
        }

        private void CheckSystemCurrent()
        {
            string current = this.SelectedProxy();
            if (current == null)    // first-time run; try to pull current proxy out of the registry and save it
            {
                try
                {
                    current = Registry.GetValue(RegKey, "ProxyServer", null).ToString();
                    if (current != null)
                    {
                        string[] parts = current.Split(new char[] {':'});
                        // look for proxy in known settings
                        foreach (var s in this.ini.Config.Keys)
                        {
                            if (!this.ini.Config[s].ContainsKey("host") ||
                                !this.ini.Config[s].ContainsKey("port"))
                                continue;
                            if ((this.ini.Config[s]["host"] == parts[0]) &&
                                (this.ini.Config[s]["port"] == parts[1]))
                                return; // already known
                        }
                        if (parts.Length == 2)
                        {
                            this.ini.SetValue("system", "host", parts[0])
                                .SetValue("system", "port", parts[1]);
                            this.ini.WriteFile(this.ConfigFile());
                        }
                    }
                }
                catch
                {
                }
            }
        }

        private void SelectAllText(object sender, EventArgs e)
        {
            var tb = sender as TextBox;
            if (tb == null)
                return;
            tb.SelectAll();
        }

        private void ResizeColumns(Object sender, EventArgs e)
        {
            if (this.lvProxies.Columns.Count == 0)
                return;
            int w = (this.lvProxies.Width / this.lvProxies.Columns.Count) - 2;
            for (var i = 0; i < this.lvProxies.Columns.Count; i++)
                this.lvProxies.Columns[i].Width = w;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            this.ResizeColumns(null, null);
        }

        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(this._visible);
        }

        private bool ProxyEnabled()
        {
            int enabled = Convert.ToInt32(Registry.GetValue(RegKey, "ProxyEnable", "0").ToString());
            return (enabled == 0) ? false : true;
        }

        private string SelectedProxy()
        {
            // resolves the current system proxy to a selected name
            int enabled = Convert.ToInt32(Registry.GetValue(RegKey, "ProxyEnable", "0").ToString());
            if (enabled == 0)
                return null;
            string systemCurrent = Registry.GetValue(RegKey, "ProxyServer", "").ToString();
            string[] parts = systemCurrent.Split(new char[] { ':' });
            if (parts.Length != 2)
                return null;
            // look for a match
            foreach (var s in this.ini.Config.Keys)
            {
                if (!this.ini.Config[s].ContainsKey("host") ||
                    !this.ini.Config[s].ContainsKey("port"))
                    continue;
                if ((this.ini.Config[s]["host"] == parts[0]) &&
                    (this.ini.Config[s]["port"] == parts[1]))
                    return s;
            }
            return null;
        }

        private void RefreshProxies()
        {
            // reloads the proxy list and the menus
            this.lvProxies.Clear();
            this.currentEdit = null;
            this.txtName.Text = "";
            this.txtHost.Text = "";
            this.txtPort.Text = "";


            bool proxyEnabled = this.ProxyEnabled();
            string currentSelected = null;

            if (this.LoadConfig())
            {
                this.CheckSystemCurrent();
                currentSelected = this.SelectedProxy();

                this.lvProxies.View = View.Details;
                this.lvProxies.Columns.Clear();
                this.lvProxies.Columns.Add("Name");
                this.lvProxies.Columns.Add("Host");
                this.lvProxies.Columns.Add("Port");

                foreach (var s in this.ini.Config.Keys)
                {
                    // test item is valid
                    if (!this.ini.Config[s].ContainsKey("host"))
                        continue;
                    if (!this.ini.Config[s].ContainsKey("port"))
                        continue;
                    ListViewItem li = new ListViewItem(s);
                    if (proxyEnabled && (s == currentSelected))
                        li.Selected = true;
                    li.SubItems.Add(this.ini.Config[s]["host"]);
                    li.SubItems.Add(this.ini.Config[s]["port"]);
                    this.lvProxies.Items.Add(li);
                }
            }
            this.btnSave.Enabled = false;
            this.btnDel.Enabled = false;
            this.InitTrayMenu(proxyEnabled, currentSelected);
            this.ResizeColumns(null, null);

            // select current proxy
        }

        private bool LoadConfig()
        {
            this.ini = new INI(this.ConfigFile());
            return this.ini.Loaded;
        }

        private string ConfigFile()
        {
            return String.Join(Path.DirectorySeparatorChar.ToString(), 
                    new string[] { Environment.GetEnvironmentVariable("USERPROFILE"), "mooprox.ini" });
        }

        private void InitIcon()
        {
            this.trayIcon = new NotifyIcon();
            this.trayIcon.Text = "Right-click for quick switch\nClick to open";
            this.trayIcon.Visible = true;
            this.trayIcon.Icon = new Icon(GetType(), "cow.ico");
            this.trayIcon.Click += this.trayClick;
        }

        private void InitTrayMenu(bool proxyEnabled, string currentSelected)
        {
            if (this.trayMenu != null)
                this.trayMenu.MenuItems.Clear();
            // add in proxy switchers
            string d = (proxyEnabled) ? "Direct" : "[ Direct ]";
            this.AddMenu(new MenuItem(d, new System.EventHandler(this.DisableProxy)))
                .AddMenu(new MenuItem("-"));
            if (this.ini.Loaded)
            {
                foreach (var s in this.ini.Config.Keys)
                {
                    var lbl = (proxyEnabled && (s == currentSelected)) ? "[ " + s + " ]" : s;
                    this.AddMenu(new ProxyMenuItem(lbl, new System.EventHandler(this.trayProxyClick),
                                    new ProxyInfo(s, this.ini.Config[s]["host"], this.ini.Config[s]["port"])));
                }
            }
            else
                this.AddMenu(new MenuItem("(No proxies configured yet)"));
            // add in utility menu items
            this.AddMenu(new MenuItem("-"))
                .AddMenu(new MenuItem("Exit", new System.EventHandler(this.Exit)));
            this.trayIcon.ContextMenu = this.trayMenu;
        }

        private void trayProxyClick(Object sender, EventArgs e)
        {
            var m = sender as ProxyMenuItem;
            if (m == null)
                return;
            this.ApplyProxy(m.proxy.Host, m.proxy.Port);
        }

        private frmMain AddMenu(MenuItem m)
        {
            if (this.trayMenu == null)
                this.trayMenu = new ContextMenu();
            this.trayMenu.MenuItems.Add(m);
            return this;
        }

        private void trayClick(Object sender, EventArgs e)
        {
            MouseEventArgs me = e as MouseEventArgs;
            if (me == null)
                return;
            if (me.Button != MouseButtons.Left)
                return;
            this._visible = !this._visible;
            this.Visible = !this.Visible;
            this.Activate();
        }

        private void Exit(Object sender, EventArgs e)
        {
            this.exiting = true;
            this._visible = true;
            this.Visible = true;
            this.Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!this.exiting)
            {
                e.Cancel = true;
                this._visible = false;
                this.Visible = false;
                return;
            }
            this.trayIcon.Dispose();
        }

        private void btnDel_Click(object sender, EventArgs e)
        {
            if (this.currentEdit == null)
                throw new Exception("Bad programmer! no biscuit! this.currentEdit is null");
            if (MessageBox.Show(String.Format("Are you sure you want to delete proxy \"{0}\"", this.currentEdit),
                "Confirm proxy delete", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.Cancel)
                return;
            this.ini.DelSection(this.currentEdit);
            this.ini.WriteFile(this.ConfigFile());
            this.RefreshProxies();
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            this.txtName.Text = "(new proxy)";
            this.txtHost.Text = "(new host)";
            this.txtPort.Text = "8080";
            this.currentEdit = this.txtName.Text;
            this.btnSave.Enabled = true;
        }

        private bool AddProxy(string Name, string Host, string Port)
        {
            if (!this.ValidateProxy(Name, Host, Port))
                return false;
            // test already exists by name
            if (this.ini.Config.ContainsKey(Name) && (this.currentEdit != null) && (this.currentEdit != Name))
            {
                if (MessageBox.Show(String.Format("You already have a proxy with the name {0} defined. Overwrite?", Name),
                    "Confirm replacement", MessageBoxButtons.OKCancel) == DialogResult.Cancel)
                    return false;
            }

            // add the item to the config
            this.ini.SetValue(Name, "host", Host);
            this.ini.SetValue(Name, "port", Port);
            // save the config
            if (!this.ini.WriteFile(this.ConfigFile()))
            {
                this.MBError(String.Format("Unable to update config file at {0}", this.ConfigFile()));
                return false;
            }

            return true;
        }

        private bool ValidateProxy(string name, string host, string port)
        {
            port = port.Trim();
            long lngPort;
            List<string> errors = new List<string>();
            try
            {
                lngPort = Convert.ToInt32(port);
            }
            catch
            {
                errors.Add("The port number you have specified is invalid");
            }
            if (Name != null)
            {
                Name = Name.Trim();
                if (Name.Length == 0)
                    errors.Add("You haven't specified a name for this proxy");
            }
            host = host.Trim();
            if (host.Length == 0)
                errors.Add("You haven't specified a host for this proxy");
            if (errors.Count() > 0)
            {
                this.MBError(String.Join("\n*", errors.ToArray()));
                return false;
            }
            return true;
        }

        private void MBError(string str)
        {
            MessageBox.Show(this, str, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void lvProxies_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this.lvProxies.SelectedItems.Count == 0)
            {
                this.btnSave.Enabled = false;
                this.btnDel.Enabled = false;
                this.currentEdit = null;
                return;
            }
            foreach (ListViewItem item in this.lvProxies.SelectedItems)
            {
                this.txtName.Text = item.Text;
                this.txtHost.Text = item.SubItems[1].Text;
                this.txtPort.Text = item.SubItems[2].Text;
                this.currentEdit = item.Text;
            }
            this.btnSave.Enabled = true;
            this.btnDel.Enabled = true;
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            this.Exit(sender, e);
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (this.currentEdit == null)
                throw new Exception("currentEdit is null; shouldn't happen; go smack your developer");
            if (this.AddProxy(this.txtName.Text, this.txtHost.Text, this.txtPort.Text))
            {
                this.ini.DelSection(this.currentEdit);
                this.currentEdit = null;
                this.ini.WriteFile(this.ConfigFile());
                // reload config
                this.RefreshProxies();
            }
        }

        private void btnApply_Click(object sender, EventArgs e)
        {
            this.ApplyProxy(this.txtHost.Text, this.txtPort.Text);
        }

        private void ApplyProxy(string host, string port)
        {
            if (!this.ValidateProxy(null, host, port))
                return;
            Registry.SetValue(RegKey, "ProxyServer", String.Format("{0}:{1}", host, port));
            this.EnableProxy();
            this.RefreshProxies();
        }

        private void DisableProxy(object sender, EventArgs e)
        {
            Registry.SetValue(RegKey, "ProxyEnable", 0);
            this.txtName.Text = "";
            this.txtHost.Text = "";
            this.txtPort.Text = "";
            this.lvProxies.SelectedItems.Clear();
            this.RefreshProxies();
        }

        private void EnableProxy()
        {
            Registry.SetValue(RegKey, "ProxyEnable", 1);
        }

        private void btnNoProxy_Click(object sender, EventArgs e)
        {
            this.DisableProxy(null, null);
        }

        private void lvProxies_DoubleClick(object sender, EventArgs e)
        {
            foreach (ListViewItem li in this.lvProxies.SelectedItems)
            {
                this.ApplyProxy(li.SubItems[1].Text, li.SubItems[2].Text);
            }
            this.RefreshProxies();
        }
    }
}

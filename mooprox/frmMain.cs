using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.IO;
using Microsoft.Win32;
using PeanutButter.INIFile;

namespace mooprox
{
    public partial class FrmMain : Form
    {
        private NotifyIcon _trayIcon;
        private ContextMenu _trayMenu;
        private bool _exiting;
        private IINIFile _ini = new INIFile(ConfigFilePath);
        private bool _visible = false;
        private string _currentEdit = null;

        private static string _regKey =
            "HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings";

        public FrmMain()
        {
            _trayIcon = null;
            _trayMenu = null;
            InitializeComponent();
            InitIcon();
            _exiting = false;

            RefreshProxies();
            lvProxies.Resize += ResizeColumns;

            txtName.GotFocus += SelectAllText;
            txtHost.GotFocus += SelectAllText;
            txtPort.GotFocus += SelectAllText;
        }

        private void CheckSystemCurrent()
        {
            var current = SelectedProxy();
            if (current == null
            ) // first-time run; try to pull current proxy out of the registry and save it
            {
                try
                {
                    current = Registry.GetValue(_regKey, "ProxyServer", null).ToString();
                    if (current != null)
                    {
                        var parts = current.Split(new char[] {':'});
                        // look for proxy in known settings
                        foreach (var s in _ini.Sections)
                        {
                            if (!_ini[s].ContainsKey("host") ||
                                !_ini[s].ContainsKey("port"))
                                continue;
                            if ((_ini[s]["host"] == parts[0]) &&
                                (_ini[s]["port"] == parts[1]))
                                return; // already known
                        }

                        if (parts.Length == 2)
                        {
                            _ini.SetValue("system", "host", parts[0]);
                            _ini.SetValue("system", "port", parts[1]);
                            _ini.Persist(ConfigFilePath);
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
            if (lvProxies.Columns.Count == 0)
                return;
            var w = (lvProxies.Width / lvProxies.Columns.Count) - 2;
            for (var i = 0; i < lvProxies.Columns.Count; i++)
                lvProxies.Columns[i].Width = w;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ResizeColumns(null, null);
        }

        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(_visible);
        }

        private bool ProxyEnabled()
        {
            var enabled =
                Convert.ToInt32(Registry.GetValue(_regKey, "ProxyEnable", "0").ToString());
            return (enabled != 0);
        }

        private string SelectedProxy()
        {
            // resolves the current system proxy to a selected name
            var enabled =
                Convert.ToInt32(Registry.GetValue(_regKey, "ProxyEnable", "0").ToString());
            if (enabled == 0)
                return null;
            var systemCurrent = Registry.GetValue(_regKey, "ProxyServer", "").ToString();
            var parts = systemCurrent.Split(new char[] {':'});
            if (parts.Length != 2)
                return null;
            // look for a match
            foreach (var s in _ini.Sections)
            {
                if (!_ini[s].ContainsKey("host") ||
                    !_ini[s].ContainsKey("port"))
                    continue;
                if ((_ini[s]["host"] == parts[0]) &&
                    (_ini[s]["port"] == parts[1]))
                    return s;
            }

            return null;
        }

        private void RefreshProxies()
        {
            // reloads the proxy list and the menus
            lvProxies.Clear();
            _currentEdit = null;
            txtName.Text = "";
            txtHost.Text = "";
            txtPort.Text = "";


            var proxyEnabled = ProxyEnabled();
            string currentSelected = null;

            LoadConfig();
            CheckSystemCurrent();
            currentSelected = SelectedProxy();

            lvProxies.View = View.Details;
            lvProxies.Columns.Clear();
            lvProxies.Columns.Add("Name");
            lvProxies.Columns.Add("Host");
            lvProxies.Columns.Add("Port");

            foreach (var s in _ini.Sections)
            {
                // test item is valid
                if (!_ini[s].ContainsKey("host"))
                    continue;
                if (!_ini[s].ContainsKey("port"))
                    continue;
                var li = new ListViewItem(s);
                if (proxyEnabled && (s == currentSelected))
                    li.Selected = true;
                li.SubItems.Add(_ini[s]["host"]);
                li.SubItems.Add(_ini[s]["port"]);
                lvProxies.Items.Add(li);
            }

            btnSave.Enabled = false;
            btnDel.Enabled = false;
            InitTrayMenu(proxyEnabled, currentSelected);
            ResizeColumns(null, null);

            // select current proxy
        }

        private void LoadConfig()
        {
            _ini = new INIFile(ConfigFilePath);
        }

        private static string ConfigFilePath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                                "mooprox.ini");

        private void InitIcon()
        {
            _trayIcon = new NotifyIcon();
            _trayIcon.Text = "Right-click for quick switch\nClick to open";
            _trayIcon.Visible = true;
            _trayIcon.Icon = new Icon(GetType(), "cow.ico");
            _trayIcon.Click += TrayClick;
        }

        private void InitTrayMenu(bool proxyEnabled, string currentSelected)
        {
            if (_trayMenu != null)
                _trayMenu.MenuItems.Clear();
            // add in proxy switchers
            var d = (proxyEnabled)
                ? "Direct"
                : "[ Direct ]";
            AddMenu(new MenuItem(d, new EventHandler(DisableProxy)))
                .AddMenu(new MenuItem("-"));
            if (_ini.Sections.Any())
            {
                foreach (var s in _ini.Sections)
                {
                    var lbl = (proxyEnabled && (s == currentSelected))
                        ? "[ " + s + " ]"
                        : s;
                    AddMenu(new ProxyMenuItem(lbl,
                                              TrayProxyClick,
                                              new ProxyInfo(s, _ini[s]["host"], _ini[s]["port"])));
                }
            }
            else
                AddMenu(new MenuItem("(No proxies configured yet)"));

            // add in utility menu items
            AddMenu(new MenuItem("-"))
                .AddMenu(new MenuItem("Exit", new EventHandler(Exit)));
            _trayIcon.ContextMenu = _trayMenu;
        }

        private void TrayProxyClick(Object sender, EventArgs e)
        {
            var m = sender as ProxyMenuItem;
            if (m == null)
                return;
            ApplyProxy(m.proxy.Host, m.proxy.Port);
        }

        private FrmMain AddMenu(MenuItem m)
        {
            if (_trayMenu == null)
                _trayMenu = new ContextMenu();
            _trayMenu.MenuItems.Add(m);
            return this;
        }

        private void TrayClick(Object sender, EventArgs e)
        {
            var me = e as MouseEventArgs;
            if (me == null)
                return;
            if (me.Button != MouseButtons.Left)
                return;
            _visible = !_visible;
            Visible = !Visible;
            Activate();
        }

        private void Exit(Object sender, EventArgs e)
        {
            _exiting = true;
            _visible = true;
            Visible = true;
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_exiting)
            {
                e.Cancel = true;
                _visible = false;
                Visible = false;
                return;
            }

            _trayIcon.Dispose();
        }

        private void btnDel_Click(object sender, EventArgs e)
        {
            if (_currentEdit == null)
                throw new Exception("Bad programmer! no biscuit! this.currentEdit is null");
            if (MessageBox.Show($"Are you sure you want to delete proxy \"{_currentEdit}\"",
                                "Confirm proxy delete",
                                MessageBoxButtons.OKCancel,
                                MessageBoxIcon.Question) == DialogResult.Cancel)
                return;
            _ini.RemoveSection(_currentEdit);
            _ini.Persist(ConfigFilePath);
            RefreshProxies();
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            txtName.Text = "(new proxy)";
            txtHost.Text = "(new host)";
            txtPort.Text = "8080";
            _currentEdit = txtName.Text;
            btnSave.Enabled = true;
        }

        private bool AddProxy(string name, string host, string port)
        {
            if (!ValidateProxy(name, host, port))
                return false;
            // test already exists by name
            if (_ini.HasSection(name) && (_currentEdit != null) && (_currentEdit != name))
            {
                if (MessageBox.Show(
                        $"You already have a proxy with the name {name} defined. Overwrite?",
                        "Confirm replacement",
                        MessageBoxButtons.OKCancel) == DialogResult.Cancel)
                    return false;
            }

            // add the item to the config
            _ini.SetValue(name, "host", host);
            _ini.SetValue(name, "port", port);
            // save the config
            try
            {
                _ini.Persist(ConfigFilePath);
            }
            catch (Exception e)
            {
                MbError($"Unable to update config file at {ConfigFilePath}");
                return false;
            }

            return true;
        }

        private bool ValidateProxy(string name, string host, string port)
        {
            port = port.Trim();
            long lngPort;
            var errors = new List<string>();
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
            if (!errors.Any())
                return true;
            MbError(String.Join("\n*", errors.ToArray()));
            return false;

        }

        private void MbError(string str)
        {
            MessageBox.Show(this, str, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void lvProxies_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lvProxies.SelectedItems.Count == 0)
            {
                btnSave.Enabled = false;
                btnDel.Enabled = false;
                _currentEdit = null;
                return;
            }

            foreach (ListViewItem item in lvProxies.SelectedItems)
            {
                txtName.Text = item.Text;
                txtHost.Text = item.SubItems[1].Text;
                txtPort.Text = item.SubItems[2].Text;
                _currentEdit = item.Text;
            }

            btnSave.Enabled = true;
            btnDel.Enabled = true;
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            Exit(sender, e);
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (_currentEdit == null)
                throw new Exception(
                    "currentEdit is null; shouldn't happen; go smack your developer");
            if (AddProxy(txtName.Text, txtHost.Text, txtPort.Text))
            {
                _ini.RemoveSection(_currentEdit);
                _currentEdit = null;
                _ini.Persist(ConfigFilePath);
                // reload config
                RefreshProxies();
            }
        }

        private void btnApply_Click(object sender, EventArgs e)
        {
            ApplyProxy(txtHost.Text, txtPort.Text);
        }

        private void ApplyProxy(string host, string port)
        {
            if (!ValidateProxy(null, host, port))
                return;
            Registry.SetValue(_regKey, "ProxyServer", $"{host}:{port}");
            EnableProxy();
            RefreshProxies();
        }

        private void DisableProxy(object sender, EventArgs e)
        {
            Registry.SetValue(_regKey, "ProxyEnable", 0);
            txtName.Text = "";
            txtHost.Text = "";
            txtPort.Text = "";
            lvProxies.SelectedItems.Clear();
            RefreshProxies();
        }

        private void EnableProxy()
        {
            Registry.SetValue(_regKey, "ProxyEnable", 1);
        }

        private void btnNoProxy_Click(object sender, EventArgs e)
        {
            DisableProxy(null, null);
        }

        private void lvProxies_DoubleClick(object sender, EventArgs e)
        {
            foreach (ListViewItem li in lvProxies.SelectedItems)
            {
                ApplyProxy(li.SubItems[1].Text, li.SubItems[2].Text);
            }

            RefreshProxies();
        }
    }
}
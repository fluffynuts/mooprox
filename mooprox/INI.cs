using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace SimpleINI
{
    class INI
    {
        public Dictionary<string, Dictionary<string, string>> Config;
        public bool Loaded { get { return this._loaded; } }
        private bool _loaded;
        public INI()
        {
            this._loaded = false;
            this.Config = new Dictionary<string,Dictionary<string,string>>();
        }
        public INI(string loadFilePath)
        {
            this.Config = new Dictionary<string,Dictionary<string,string>>();
            this._loaded = false;
            this.LoadFile(loadFilePath);
        }

        public INI SetValue(string section, string name, string value)
        {
            if (!this.Config.ContainsKey(section))
                this.Config[section] = new Dictionary<string, string>();
            this.Config[section][name] = value;
            return this;
        }

        public bool LoadFile(string path)
        {
            if (!File.Exists(path))
                return false;
            FileStream fs;
            try
            {
                fs = File.Open(path, FileMode.Open);
            }
            catch
            {
                return false;
            }
            StreamReader rdr = new StreamReader(fs);

            // reused splitters
            char[] commentDelimiter = new char[] {';'};
            char[] itemDelimiter = new char[] {'='};
            char[] sectionTrim = new char[] {'[', ']', ' ', '\t', '\n', '\r'};

            string currentSection = "";
            while (rdr.Peek() > 0)
            {
                string line = rdr.ReadLine().Trim();
                if (line.Length == 0)   // ignore an empty line
                    continue;
                // split out comments
                string[] parts = line.Split(commentDelimiter);
                if (parts[0].Length == 0)   // ignore a comment-only line
                    continue;
                if (parts[0][0] == '[') // start of a section
                {
                    currentSection = parts[0].Trim(sectionTrim);
                    continue;
                }
                parts = parts[0].Split(itemDelimiter);
                if (parts.Length < 2)
                    continue;
                string name = parts[0].Trim();

                if (!this.Config.ContainsKey(currentSection))
                    this.Config[currentSection] = new Dictionary<string, string>();
                if (this.Config[currentSection].ContainsKey(name))
                    continue;   // already specified higher up

                List<string> tmp = new List<string>();
                for (var i = 1; i < parts.Length; i++)
                    tmp.Add(parts[i]);
                this.Config[currentSection][name] = String.Join("=", tmp.ToArray()).Trim();
            }

            rdr.Close();
            fs.Close();

            this._loaded = true;
            return this._loaded;
        }

        public INI DelSection(string section)
        {
            if (this.Config.ContainsKey(section))
                this.Config.Remove(section);
            return this;
        }
        public INI DelSetting(string section, string setting)
        {
            if (this.Config.ContainsKey(section))
            {
                if (this.Config[section].ContainsKey(setting))
                {
                    this.Config[section].Remove(setting);
                }
            }
            return this;
        }

        public bool WriteFile(string path)
        {
            FileStream fs;
            try
            {
                fs = File.Open(path, FileMode.Create);
            }
            catch
            {
                return false;
            }
            StreamWriter wtr = new StreamWriter(fs);
            foreach (var s in this.Config.Keys)
            {
                wtr.WriteLine(String.Format("[{0}]", s));
                foreach (var n in this.Config[s].Keys)
                {
                    wtr.WriteLine(String.Format("{0}={1}", n, this.Config[s][n]));
                }
                wtr.WriteLine("");
            }

            wtr.Close();
            fs.Close();
            return true;
        }
    }
}

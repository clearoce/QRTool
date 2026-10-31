using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace QRTool
{
    public class AppConfig
    {
        public int version { get; set; }
        public string printer_name { get; set; }
        public double label_width_mm { get; set; }
        public double label_height_mm { get; set; }
        public double gap_mm { get; set; }
        public int dpi { get; set; }
        public string ecc { get; set; }
        public int cell_width { get; set; }
        public int copies { get; set; }
        public string encoding { get; set; }
        public bool use_model2 { get; set; }
        public bool keep_newlines { get; set; }
        public bool compat_mode { get; set; }
        public string last_export_dir { get; set; }

        public AppConfig()
        {
            version = 1;
            printer_name = "";
            label_width_mm = 40.0;
            label_height_mm = 30.0;
            gap_mm = 2.0;
            dpi = 300;
            ecc = "M";
            cell_width = 0;
            copies = 1;
            encoding = "utf-8";
            use_model2 = true;
            keep_newlines = true;
            compat_mode = false;
            last_export_dir = "";
        }

        public void Normalize()
        {
            version = 1;
            if (printer_name == null) printer_name = "";
            printer_name = printer_name.Trim();
            label_width_mm = Clamp(label_width_mm, 20.0, 98.0, 40.0);
            label_height_mm = Clamp(label_height_mm, 5.0, 300.0, 30.0);
            gap_mm = Clamp(gap_mm, 0.0, 25.4, 2.0);
            if (dpi != 203 && dpi != 300 && dpi != 600) dpi = 300;
            if (ecc != "L" && ecc != "M" && ecc != "Q" && ecc != "H") ecc = "M";
            if (cell_width < 0 || cell_width > 10) cell_width = 0;
            if (copies < 1 || copies > 999) copies = 1;
            if (encoding != "utf-8" && encoding != "gb18030") encoding = "utf-8";
            if (last_export_dir == null) last_export_dir = "";
        }

        private static double Clamp(double value, double min, double max, double fallback)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return fallback;
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public static string DataDir()
        {
            string appData = Environment.GetEnvironmentVariable("APPDATA");
            if (string.IsNullOrEmpty(appData))
                appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "QRTool");
        }

        public static string ConfigPath()
        {
            return Path.Combine(DataDir(), "config.json");
        }

        public static AppConfig Load()
        {
            string path = ConfigPath();
            try
            {
                if (!File.Exists(path)) return new AppConfig();
                string json = File.ReadAllText(path, new UTF8Encoding(false));
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                AppConfig cfg = serializer.Deserialize<AppConfig>(json);
                if (cfg == null) cfg = new AppConfig();
                cfg.Normalize();
                return cfg;
            }
            catch (Exception)
            {
                Quarantine(path);
                return new AppConfig();
            }
        }

        private static void Quarantine(string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                string backup = path + ".bad";
                if (File.Exists(backup)) backup = path + ".bad-" + DateTime.Now.ToString("yyyyMMddHHmmss");
                File.Move(path, backup);
            }
            catch (Exception) { }
        }

        public void Save()
        {
            Normalize();
            string path = ConfigPath();
            string dir = Path.GetDirectoryName(path);
            if (dir != null) Directory.CreateDirectory(dir);
            string tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            string json = serializer.Serialize(this);
            try
            {
                File.WriteAllText(tmp, json, new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
            }
            finally
            {
                if (File.Exists(tmp))
                {
                    try { File.Delete(tmp); } catch (Exception) { }
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MyPixivUtils.Shared
{
    public class LocalSetting
    {
        static string _configFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "userconfig.json");
        private static JObject _settings = LoadSetting();
        private static LocalSetting _ = new LocalSetting();
        public static LocalSetting Instance => _;

        private static JObject LoadSetting()
        {
            JObject setting;
            if (!File.Exists(_configFile))
            {
                File.Create(_configFile).Dispose();
            }
            try
            {
                var configText = File.ReadAllText(_configFile);
                setting = JObject.Parse(configText);
            }
            catch
            {
                setting = new JObject();
            }
            setting.PropertyChanged += _settings_PropertyChanged;
            return setting;
        }

        private static void _settings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            lock (_)
            {
                File.WriteAllText(_configFile, _settings.ToString(Formatting.Indented));
            }
        }

        public dynamic this[string key, Type type=null]
        {
            get
            {
                var x = _settings[key];
                if (type != null)
                {
                    return x.ToObject(type);
                }
                return x;
            }
            set => _settings[key] = JToken.FromObject(value);
        }
    }
}

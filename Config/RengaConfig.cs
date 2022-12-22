using Newtonsoft.Json;
using System.Reflection;

namespace Renga.Config
{
    public class RengaConfig
    {
        private Dictionary<string, object> _config;

        public RengaConfig()
        {
            try
            {
                string confText = File.ReadAllText("settings.json");
                _config = JsonConvert.DeserializeObject<Dictionary<string, object>>(confText);
            }
            catch(FileNotFoundException e)
            {
                _config = GenerateDefaultSettings();
            }
        }

        private static Dictionary<string, object> GenerateDefaultSettings()
        {
            string confText = Properties.Resources.DefaultConfig;
            File.WriteAllText("settings.json", confText);
            return JsonConvert.DeserializeObject<Dictionary<string, object>>(confText);
        }



        public T GetProperty<T>(string key, T defaultValue)
        {
            if(!_config.Keys.Contains(key))
            {
                SetProperty(key, defaultValue);
                return defaultValue;
            }
            return (T)_config[key];
        }

        public void SetProperty<T>(string key, T value)
        {
            _config[key] = value;
            SaveSettings();
        }



        private void SaveSettings()
        {
            string confText = JsonConvert.SerializeObject(_config);
            File.WriteAllText("settings.json", confText);
        }
    }
}

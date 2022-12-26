using Newtonsoft.Json;
using System.Reflection;

#pragma warning disable CS8601 // Possible null reference assignment.
#pragma warning disable CS8603 // Possible null reference return.
#pragma warning disable CS8604 // Possible null reference argument.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace Renga.Config
{
    public class RengaConfig
    {
        private Dictionary<string, object> _config;
        private static readonly string ConfigPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "settings.json");

        public RengaConfig()
        {
            try
            {
                string confText = File.ReadAllText(ConfigPath);
                _config = JsonConvert.DeserializeObject<Dictionary<string, object>>(confText);
            }
            catch(FileNotFoundException)
            {
                _config = GenerateDefaultSettings();
            }
        }

        private static Dictionary<string, object> GenerateDefaultSettings()
        {
            string confText = Properties.Resources.DefaultConfig;
            File.WriteAllText(ConfigPath, confText);
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
            File.WriteAllText(ConfigPath, confText);
        }
    }
}

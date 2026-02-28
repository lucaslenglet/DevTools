using DevTools.Helpers;
using DevTools.Models;

namespace DevTools.Services;

class ConfigurationManager(AppContext context)
{
    private const string Folder = "DevTools";
    private const string FileName = "config.yml";

    public static AppContext InitializeAppContext()
    {
        var configFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Folder,
            FileName
        );

        var config = Load(configFilePath);

        return new AppContext
        {
            ConfigFilePath = configFilePath,
            Config = config
        };
    }

    public void Save()
    {
        var directory = Path.GetDirectoryName(context.ConfigFilePath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var yml = SerdeHelper.Serialize(context.Config);
        File.WriteAllText(context.ConfigFilePath, yml);
    }

    private static Config Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new Config();
        }

        var yml = File.ReadAllText(filePath);
        var config = SerdeHelper.Deserialize<Config>(yml);

        if (config is null)
        {
            throw new Exception("User preference failed to load.");
        }

        return config;
    }
}
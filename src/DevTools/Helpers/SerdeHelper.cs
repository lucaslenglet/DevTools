using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DevTools.Helpers;

static class SerdeHelper
{
    private static INamingConvention NamingConvention => CamelCaseNamingConvention.Instance;

    private static ISerializer Serializer { get; } = new SerializerBuilder()
        .WithNamingConvention(NamingConvention)
        .Build();

    private static IDeserializer Deserializer { get; } = new DeserializerBuilder()
        .WithNamingConvention(NamingConvention)
        .Build();

    public static T Deserialize<T>(string content) => Deserializer.Deserialize<T>(content);

    public static string Serialize<T>(T @object) => Serializer.Serialize(@object);
}
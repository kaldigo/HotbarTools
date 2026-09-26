// Only the config contract is needed to exercise the real config with Dalamud's serializer.
namespace Dalamud.Configuration;
public interface IPluginConfiguration { int Version { get; set; } }

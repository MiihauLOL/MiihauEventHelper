using StardewModdingAPI;

namespace MiihauEventHelper
{
    public interface IContentPatcherAPI
    {
        void RegisterToken(IManifest mod, string name, object token);
    }
}

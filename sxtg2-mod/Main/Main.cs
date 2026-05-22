using MelonLoader;
using sxtg2.Features;

[assembly: MelonInfo(typeof(sxtg2.Main), "sxtg2", "1.0.0", "화영왕")]
[assembly: MelonGame("Lyrebird Studio", "Sixtar Gate STARTRAIL")]
[assembly: MelonColor(128, 0, 255, 255)] // Purple (R, G, B, A)

namespace sxtg2
{
    public partial class Main : MelonMod
    {
        private static SceneDetector _sceneDetector;
    }
}

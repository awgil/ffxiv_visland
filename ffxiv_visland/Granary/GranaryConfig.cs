using System.ComponentModel;
using visland.Helpers;

namespace visland.Granary;

public class GranaryConfig : Configuration.Node
{
    public enum CollectStrategy
    {
        [Description("Manual")]
        Manual,

        [Description("Automatic, if not overcapping")]
        NoOvercap,

        [Description("Automatic, allow overcap")]
        FullAuto,
    }

    public enum UpdateStrategy
    {
        [Description("Manual")]
        Manual,

        [Description("Max out, keep same destination")]
        MaxCurrent,

        [Description("Max out and select expedition bringing rare resources with two lowest counts")]
        BestDifferent,

        [Description("Max out and select expedition bringing rare resource with lowest count in both granaries")]
        BestSame,
    }

    public CollectStrategy Collect = CollectStrategy.Manual;
    public UpdateStrategy Reassign = UpdateStrategy.Manual;
}

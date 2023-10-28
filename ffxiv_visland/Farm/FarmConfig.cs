using visland.Helpers;

namespace visland.Farm;

public class FarmConfig : Configuration.Node
{
    public CollectStrategy Collect = CollectStrategy.Manual;
}

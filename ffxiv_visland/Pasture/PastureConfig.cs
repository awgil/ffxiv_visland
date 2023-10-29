using visland.Helpers;

namespace visland.Pasture;

public class PastureConfig : Configuration.Node
{
    public CollectStrategy Collect = CollectStrategy.Manual;
}

using visland.Helpers;

namespace visland.Export;

public class ExportConfig : Configuration.Node
{
    public bool AutoSell = false;
    public int NormalLimit = 900;
    public int GranaryLimit = 900;
    public int FarmLimit = 900;
    public int PastureLimit = 900;
}

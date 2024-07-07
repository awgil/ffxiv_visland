using visland.Helpers;

namespace visland.Gathering;
public unsafe class GatherDebug(GatherRouteExec exec)
{
    private readonly UITree _tree = new();
    private GatherRouteExec exec = exec;

    public void Draw() { }

}

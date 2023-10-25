using FFXIVClientStructs.FFXIV.Client.UI;

namespace visland.Gathering;

public unsafe class OverrideAFK
{
    public void ResetTimers()
    {
        var module = UIModule.Instance()->GetInputTimerModule();
        module->AfkTimer = 0;
        module->ContentInputTimer = 0;
        module->InputTimer = 0;
        module->Unk1C = 0;
    }
}

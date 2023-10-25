using System;

namespace visland.Helpers;

class Throttle
{
    private DateTime _nextAllowed;

    public bool Exec(Action action, float throttle = 0.5f)
    {
        var now = DateTime.Now;
        if (now < _nextAllowed)
            return false;

        action();
        _nextAllowed = now.AddSeconds(throttle);
        return true;
    }
}

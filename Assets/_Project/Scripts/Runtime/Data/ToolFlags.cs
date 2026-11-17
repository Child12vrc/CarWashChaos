using System;

namespace CarWash.Data
{
    [Flags]
    public enum ToolFlags
    {
        None = 0,
        Spray = 1 << 0,
        Foam = 1 << 1,
        Wipe = 1 << 2,
        Vacuum = 1 << 3,
        Wax = 1 << 4,
        Capture = 1 << 5,
        Extinguish = 1 << 6,
        Blower = 1 << 7,
    }
}

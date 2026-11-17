using System;

namespace CarWash.Player
{
    [Flags]
    public enum StatusFlags : byte { None = 0, Knocked = 1, Stung = 2, Soaked = 4, Carrying = 8, Crouching = 16, InFoam = 32 }
}

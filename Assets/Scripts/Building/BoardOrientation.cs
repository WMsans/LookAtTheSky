using System;

namespace Building
{
    [Flags]
    public enum BoardOrientation
    {
        None = 0,
        X = 1,  // XY plane: (x,y,z) to (x+1,y+1,z)
        Y = 2,  // YZ plane: (x,y,z) to (x,y+1,z+1)
        Z = 4   // XZ plane: (x,y,z) to (x+1,y,z+1)
    }
}

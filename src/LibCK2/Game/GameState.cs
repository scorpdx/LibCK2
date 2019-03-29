using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text;

namespace LibCK2.Game
{
    public sealed class GameState : DynamicObject
    {
        internal GameState(IReadOnlyList<(bool nested, string name, object value)> tokens)
        {

        }
    }
}

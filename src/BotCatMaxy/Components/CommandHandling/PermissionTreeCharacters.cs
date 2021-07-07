using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BotCatMaxy.Components.CommandHandling
{
    public static class PermissionTreeCharacters
    {
        public readonly static char Intersect = '├';
        public readonly static char Bar = '│';
        public readonly static char End = '└';
        public readonly static char Dash = '─';

        // In order:  (Intersect, Bar, End, Dash)
        public static (char, char, char, char) GetChars()
        {
            return (Intersect, Bar, End, Dash);
        }
    }
}

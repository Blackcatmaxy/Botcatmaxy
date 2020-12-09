using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BotCatMaxy.Utilities
{
    //will contain enumerable utilities because close enough in purpose, maybe split later if too big
    public static class CollectionUtilities
    {
        public static bool IsNullOrEmpty(this ICollection list)
        {
            if (list == null || list.Count == 0) return true;
            else return false;
        }

        public static bool NotEmpty<T>(this IEnumerable<T> list, int needAmount = 0)
        {
            if (list == null || list.ToArray().Length <= needAmount) return false;
            else return true;
        }

        public static string ListItems(this IEnumerable<string> list, string joiner = " ")
        {
            string items = null;
            if (list.NotEmpty())
            {
                (list as ICollection<string>)?.RemoveNullEntries();
                foreach (string item in list)
                {
                    if (items == null) items = "";
                    else items += joiner;
                    items += item;
                }
            }
            return items;
        }
    }
}

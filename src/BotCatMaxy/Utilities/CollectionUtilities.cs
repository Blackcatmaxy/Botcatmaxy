using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BotCatMaxy
{
    //will contain enumerable utilities because close enough in purpose, maybe split later if too big
    public static class CollectionUtilities
    {
        /// <summary>
        /// Joins a collection of strings into one string
        /// </summary>
        public static string ListItems(this IEnumerable<string> list, string joiner = " ")
        {
            string items = null;
            var count = (list as ICollection)?.Count ?? list?.Count();
            if (count is not null or 0)
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
        
        /// <summary>
        /// Removes all null entries in a collection
        /// </summary>
        public static void RemoveNullEntries<T>(this ICollection<T> list)
        {
            if (list?.Count is not null or 0)
                foreach (T thing in list)
                    if (thing == null)
                        list.Remove(thing);
        }
    }
}

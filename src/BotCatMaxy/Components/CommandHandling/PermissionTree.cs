using BotCatMaxy.Components.CommandHandling;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace BotCatMaxy.Components.CommandHandling
{
    public class TreeNode : IEnumerable<TreeNode>
    {
        public readonly string Name;

        /// <summary>
        /// Recursively checks parents name and appends to own name to be formatted as Parent.CurrentName
        /// </summary>
        public readonly string FullName;

        public readonly TreeNode Parent;

        public List<TreeNode> children { get; } = new();

        public TreeNode(string name, TreeNode parent = null)
        {
            Name = name;
            Parent = parent;

            FullName = "";

            //Recurses so that bottom will get full name and bottom will just be name
            if (Parent is TreeNode)
                FullName = Parent.FullName + ".";

            FullName += Name;
        }

        public TreeNode this[int i]
            => children[i];

        public TreeNode AddChild(string nodeName)
        {
            TreeNode child = new(nodeName, this);
            children.Add(child);
            return child;
        }

        /// <summary>
        /// Recursively goes through children of children to get nodes with no children 
        /// </summary>
        /// <returns>All dead ends in the tree including children of children</returns>
        public List<TreeNode> GetAllRoots()
        {
            List<TreeNode> result = new();

            foreach (TreeNode child in children)
            {
                if (child.children.Count == 0)
                    result.Add(child);
                else
                    result.AddRange(child.GetAllRoots());
            }

            return result;
        }

        /// <summary>
        /// Recursively goes through children of children to return all the possible FullNames without children
        /// </summary>
        /// <returns>All dead ends in the tree as strings by their FullName</returns>
        public List<string> GetAllRootsAsStrings()
            => GetAllRoots().Select(i => i.FullName).ToList();

        /// <summary>
        /// Recursively goes through children of children to return a tree map of all the nodes in this tree
        /// </summary>
        /// <returns>A string (with newlines) containing an ASCII tree map of all the nodes in this tree</returns>
        public string GetAllRootsAsAsciiTree(int indent = 0)
        {
            string result = "";

            var (intersect, bar, dash, end) = PermissionTreeCharacters.GetChars();

            int index = 0;

            foreach (TreeNode node in children)
            {
                index++;

                if (index is not 0)
                {
                    if (indent == 0)
                        result += intersect + dash + ' ';
                    else
                        result += bar.ToString().PadRight(3 * indent);
                }

                if (indent is not 0)
                {
                    if (index == children.Count())
                        result += end + dash + ' ';
                    else
                        result += intersect + dash + ' ';
                }

                result += $"{node.Name}\n";

                if (node.children.Count() > 0)
                    result += node.GetAllRootsAsAsciiTree(indent + 1);
            }

            return result;
        }

        public IEnumerator<TreeNode> GetEnumerator()
            => children.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => children.GetEnumerator();
    }
}
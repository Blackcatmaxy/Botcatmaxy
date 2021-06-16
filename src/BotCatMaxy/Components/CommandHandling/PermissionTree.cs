using System;
using System.Collections;
using System.Collections.Generic;

namespace BotCatMaxy.Components.CommandHandling
{
    public class TreeNode : IEnumerable<TreeNode>
    {
        public string Name { get; }
        public readonly List<TreeNode> children = new();
        public TreeNode Parent { get; }
        
        /// <summary>
        /// Recursively checks parents name and appends to own name to be formatted as Parent.CurrentName
        /// </summary>
        public string FullName { get; } = "";

        public TreeNode(string name, TreeNode parent)
        {
            Name = name;
            Parent = parent;
            //Recurses so that bottom will get full name and bottom will just be name
            if (parent != null) 
                FullName = Parent.FullName + ".";
            FullName += Name;
        }

        public TreeNode this[int i]
            => children[i];

        public TreeNode AddChild(string name)
        {
            var child = new TreeNode(name, this);
            children.Add(child);
            return child;
        }

        /// <summary>
        /// Recursively goes through children of children to add nodes with no children 
        /// </summary>
        /// <returns>All dead ends in the tree including children of children</returns>
        public List<TreeNode> GetAllRoots()
        {
            var result = new List<TreeNode>();
            foreach (var child in children)
            {
                if (child.children.Count == 0)
                    result.Add(child);
                else
                    result.AddRange(child.GetAllRoots());
            }

            return result;
        }
        
        public IEnumerator<TreeNode> GetEnumerator()
        {
            return children.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return children.GetEnumerator();
        }
    }
}
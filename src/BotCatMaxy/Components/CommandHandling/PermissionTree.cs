using System;
using System.Collections;
using System.Collections.Generic;

namespace BotCatMaxy.Components.CommandHandling
{
    public class TreeNode : IEnumerable<TreeNode>
    {
        public string Name { get; }
        public readonly List<TreeNode> children = new();

        public TreeNode(string name)
        {
            Name = name;
        }

        public TreeNode this[int i]
            => children[i];

        public TreeNode AddChild(string name)
        {
            var child = new TreeNode(name);
            children.Add(child);
            return child;
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
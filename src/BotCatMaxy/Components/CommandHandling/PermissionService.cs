using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord.Addons.Hosting;
using Discord.Commands;

namespace BotCatMaxy.Components.CommandHandling
{
    public class PermissionService
    {
        public IList<TreeNode> Parents { get; private set; } = new List<TreeNode>(8);

        /// <summary>
        /// Set up with commands once <see cref="CommandService"/> has been initialized with modules
        /// </summary>
        public void SetUp(CommandService commandService)
        {
            foreach (var command in commandService.Commands)
            {
                Console.WriteLine("Saving command");
                var dynamicPrecondition = command.Preconditions
                    .FirstOrDefault(precondition => precondition is DynamicPermissionAttribute) as DynamicPermissionAttribute;
                if (dynamicPrecondition == null)
                    continue;
                
                var node = dynamicPrecondition.Node;
                var parts = node.Split('.');
                var currentList = Parents;
                TreeNode lastNode = null;
                foreach (var part in parts)
                {
                    lastNode = SearchAndAdd(currentList, part, lastNode);
                    currentList = lastNode.children;
                }
            }
        }

        public static TreeNode SearchAndAdd(IList<TreeNode> nodes, string name, TreeNode parent)
        {
            var node = nodes.FirstOrDefault(treeNode 
                => name.Equals(treeNode?.Name, StringComparison.InvariantCultureIgnoreCase));
            if (node is null)
            {
                node = new TreeNode(name, parent);
                nodes.Add(node);
            }
            return node;
        }

        /// <summary>
        /// Verify if Node is valid
        /// </summary>
        /// <param name="node">Command Permission node like Moderation.User.Kick</param>
        /// <returns>A string about the failed result, or null on success</returns>
        public string TryVerifyNode(string node)
        {
            if (node[0] == '.')
                return "`.` cannot be the first character.";
            TreeNode lastNode = null;
            string[] split = node.Split('.');
            for (var i = 0; i < split.Length; i++)
            {
                string part = split[i];
                //Verifying valid use of wildcard as only part of substring (for now?) and as last part
                if (part.Contains('*'))
                    if (part.Length > 1 || i != split.Length - 1)
                        return "Invalid use of `*` wildcard.";
                    else
                        break;

                if (i == 0)
                    lastNode = Parents.FirstOrDefault(p =>
                        p.Name.Equals(part, StringComparison.InvariantCultureIgnoreCase));
                else
                    lastNode = lastNode.children.FirstOrDefault(p =>
                        p.Name.Equals(part, StringComparison.InvariantCultureIgnoreCase));

                if (lastNode == null)
                    return "Node does not correspond to any valid command.";
            }

            return null;
        }
    }
}
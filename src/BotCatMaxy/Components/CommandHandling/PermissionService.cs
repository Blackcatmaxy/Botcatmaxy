using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord.Addons.Hosting;
using Discord.Commands;

namespace BotCatMaxy.Components.CommandHandling
{
    public class PermissionService : InitializedService
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

        public override Task InitializeAsync(CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }
    }
}
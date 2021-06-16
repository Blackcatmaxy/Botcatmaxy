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
                if (!Parents.Any(treeNode => parts[0].Equals(treeNode?.Name, StringComparison.InvariantCultureIgnoreCase))) 
                    Parents.Add(new TreeNode(parts[0]));
                var parent = Parents[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    parent = parent.AddChild(parts[i]);
                }
            }
        }
        
        public override Task InitializeAsync(CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }
    }
}
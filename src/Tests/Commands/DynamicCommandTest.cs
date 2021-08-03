using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BotCatMaxy.Components.CommandHandling;
using BotCatMaxy.Data;
using BotCatMaxy.Models;
using Discord;
using Discord.Commands;
using Tests.Mocks.Guild;
using Xunit;

namespace Tests.Commands
{
    public class DynamicCommandTest : CommandTests, IAsyncLifetime
    {
        public PermissionService PermissionService { get; }

        public DynamicCommandTest()
        {
            PermissionService = new PermissionService();
            PermissionService.SetUp(Service);
        }

        public new async Task InitializeAsync()
        {
            await base.InitializeAsync();
            await Guild.DownloadUsersAsync();
            FieldInfo[] fields = GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Public |
                                                     BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(IRole))
                {
                    var roleAttribute = field.GetCustomAttribute<InsertRoleAttribute>();
                    if (roleAttribute == null)
                        throw new InvalidOperationException();

                    var role = await Guild.CreateRoleAsync(roleAttribute.Name, roleAttribute.Permissions, isMentionable: false);
                    field.SetValue(this, role);
                    if (roleAttribute.CommandNodes == null)
                        continue;
                    var commandPermissions = Guild.LoadFromFile<CommandPermissions>(true);
                    foreach (string node in roleAttribute.CommandNodes)
                    {
                        if (PermissionService.TryVerifyNode(node) is not null and var error)
                            throw new ArgumentException(error);
                        commandPermissions.AddNodeToRole(role.Id, node);
                    }
                }
                else if (field.FieldType == typeof(IGuildUser))
                {
                    var userAttribute = field.GetCustomAttribute<InsertUserAttribute>();
                    if (userAttribute == null)
                        throw new InvalidOperationException();

                    var user = new MockGuildUser(userAttribute.UserName, Guild);
                    Guild.AddUser(user);
                    field.SetValue(this, user);
                    if (userAttribute.RoleName == null)
                        continue;
                    var role = Guild.Roles.FirstOrDefault(r => r.Name == userAttribute.RoleName);
                    if (role == null)
                        throw new ArgumentException();
                    await user.AddRoleAsync(role);
                }
            }
        }
    }
}
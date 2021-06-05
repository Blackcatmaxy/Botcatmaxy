using System;
using System.Linq;
using System.Threading.Tasks;
using Discord.Commands;

namespace BotCatMaxy.TypeReaders
{
    public class CommandTypeReader : TypeReader
    {
        private CommandService _service;
        
        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input,
            IServiceProvider services)
        {
            _service ??= services.GetService(typeof(CommandService)) as CommandService;
            //Search won't work with prefix included so remove it
            if (input[0] == '!')
                input = input[1..];
            
            SearchResult result = _service.Search(context, input);
            if (!result.IsSuccess)
                return Task.FromResult(TypeReaderResult.FromError(result));
            return Task.FromResult(TypeReaderResult.FromSuccess(result.
                Commands.Select(match => match.Command).ToArray()));
        }
    }
}
﻿using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Spectre.CommandLine
{
    public abstract class Command<TSettings> : ICommand<TSettings>
        where TSettings : class
    {
        protected abstract int Execute(TSettings settings, ILookup<string, string> remaining);

        Task<int> ICommand.Execute(object settings, ILookup<string, string> remaining)
        {
            Debug.Assert(settings is TSettings, "Command settings is of unexpected type.");
            return Task.FromResult(Execute((TSettings)settings, remaining));
        }

        Task<int> ICommand<TSettings>.Execute(TSettings settings, ILookup<string, string> remaining)
        {
            return Task.FromResult(Execute(settings, remaining));
        }
    }
}

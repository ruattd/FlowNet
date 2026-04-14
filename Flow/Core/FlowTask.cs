using System;
using System.Threading.Tasks;
using Void = Flow.Core.ComponentModel.Void;

namespace Flow.Core;

partial class Flow
{
    public static Task InvokeTask(string globalIdentifier)
        => InvokeTask<Void, Void>(globalIdentifier, default);

    public static Task<TReturn> InvokeTask<TReturn>(string globalIdentifier)
        => InvokeTask<TReturn, Void>(globalIdentifier, default);

    public static Task InvokeTask<TArgument>(string globalIdentifier, TArgument argument)
        => InvokeTask<Void, TArgument>(globalIdentifier, argument);

    public static Task<TReturn> InvokeTask<TReturn, TArgument>(string globalIdentifier, TArgument argument)
    {
        throw new NotImplementedException();
    }
}

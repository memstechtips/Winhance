namespace Winhance.Core.Features.Common.Exceptions;

public class ExecutionPolicyException : InvalidOperationException
{
    public ExecutionPolicyException(string message) : base(message) { }
    public ExecutionPolicyException(string message, Exception inner) : base(message, inner) { }
}

using NodeAgent.Models;

namespace NodeAgent.Services;

//All methods of the interface are thread-safe.
public interface IJobTaskFilter
{
    Task<StartJobAndTaskArgsTuple> OnJobStart(StartJobAndTaskArgsTuple args);

    Task<StartTaskArgsTuple> OnTaskStart(StartTaskArgsTuple args);

    Task<EndJobArgs> OnJobEnd(EndJobArgs args);
}

//TODO: Implement this.
public class JobTaskFilter : IJobTaskFilter
{
    public Task<StartJobAndTaskArgsTuple> OnJobStart(StartJobAndTaskArgsTuple args)
    {
        return Task.FromResult(args);
    }

    public Task<StartTaskArgsTuple> OnTaskStart(StartTaskArgsTuple args)
    {
        return Task.FromResult(args);
    }

    public Task<EndJobArgs> OnJobEnd(EndJobArgs args)
    {
        return Task.FromResult(args);
    }
}

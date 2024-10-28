using NodeAgent.Models;

namespace NodeAgent.Services;

//All methods of the interface are thread-safe.
public interface IJobTaskFilter
{
    Task<StartJobAndTaskArgsTuple> OnJobStart(StartJobAndTaskArgsTuple args);

    Task<StartTaskArgsTuple> OnTaskStart(StartTaskArgsTuple args);

    Task<EndJobArgs> OnJobEnd(EndJobArgs args);
}

public class JobTaskFilter : IJobTaskFilter
{
    public Task<StartJobAndTaskArgsTuple> OnJobStart(StartJobAndTaskArgsTuple args)
    {
        throw new NotImplementedException();
    }

    public Task<StartTaskArgsTuple> OnTaskStart(StartTaskArgsTuple args)
    {
        throw new NotImplementedException();
    }

    public Task<EndJobArgs> OnJobEnd(EndJobArgs args)
    {
        throw new NotImplementedException();
    }
}

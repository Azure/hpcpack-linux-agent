using NodeAgent.Models;

namespace NodeAgent.Services;

public interface IJobTaskTable
{
    NodeInfo GetNodeInfo();

    void RequestResync();
}

public class JobTaskTable : IJobTaskTable
{
    public NodeInfo GetNodeInfo()
    {
        throw new NotImplementedException();
    }

    public void RequestResync()
    {
        throw new NotImplementedException();
    }
}

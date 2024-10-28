namespace NodeAgent.Services;

public interface IResyncFlag
{
    //Trigger a resync on the scheduler by a heartbeat report
    bool RequestResync { get; set; }
}

public class ResyncFlag : IResyncFlag
{
    private int _flag = 1;

    public bool RequestResync
    {
        get
        {
            return _flag == 1;
        }

        set
        {
            Interlocked.Exchange(ref _flag, value ? 1 : 0);
        }
    }
}

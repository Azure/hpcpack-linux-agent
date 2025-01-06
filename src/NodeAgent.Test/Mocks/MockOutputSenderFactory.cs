using NodeAgent.Services;

namespace NodeAgent.Test.Mocks;

public class MockOutputSenderFactory : IOutputSenderFactory
{
    public IOutputSender Create(string uri)
    {
        throw new NotSupportedException();
    }
}

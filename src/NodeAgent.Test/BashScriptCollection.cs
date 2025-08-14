namespace NodeAgent.Test;

public class BashScriptCollectionFixture : IDisposable
{
    public BashScriptCollectionFixture()
    {
        Environment.SetEnvironmentVariable("CGroupVersion", "v1");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("CGroupVersion", null);
    }
}

[CollectionDefinition(nameof(BashScriptCollection))]
public class BashScriptCollection : ICollectionFixture<BashScriptCollectionFixture>
{
}

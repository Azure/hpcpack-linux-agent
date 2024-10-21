namespace NodeAgent.Models;

public class StartJobAndTaskArgs
{
    public int JobId { get; set; }

    public int TaskId { get; set; }

    public ProcessStartInfo StartInfo { get; set; }

    public string UserName { get; set; }

    public string Password { get; set; }

    //TODO
    //std::vector<unsigned char> Certificate;

    public string PrivateKey { get; set; }

    public string PublicKey { get; set; }
}

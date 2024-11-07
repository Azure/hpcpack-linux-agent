using System.ComponentModel.DataAnnotations;

namespace NodeAgent.Models;

//The tuple input is really bad! But we have to keep it for compatibility.
public class StartJobAndTaskArgsTuple
{
    [Required]
    public JobIdAndTaskId m_item1 { get; set; } = default!;

    [Required]
    public ProcessStartInfo m_item2 { get; set; } = default!;

    [Required]
    public string m_item3 { get; set; } = default!;

    [Required]
    public string m_item4 { get; set; } = default!;

    public string? m_item5 { get; set; }

    public string? m_item6 { get; set; }

    public StartJobAndTaskArgs ToStartJobAndTaskArgs()
    {
        return new StartJobAndTaskArgs()
        {
            JobId = m_item1.JobId,
            TaskId = m_item1.TaskId,
            StartInfo = m_item2,
            UserName = m_item3,
            Password = m_item4,
            PrivateKey = m_item5,
            PublicKey = m_item6,
        };
    }

    //TODO: For logging
    public override string? ToString()
    {
        return base.ToString();
    }
}

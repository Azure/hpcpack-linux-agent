using System.ComponentModel.DataAnnotations;

namespace NodeAgent.Models;

//The tuple input is really bad! But we have to keep it for compatibility.
public class StartTaskArgsTuple
{
    [Required]
    public JobIdAndTaskId m_item1 { get; set; } = default!;

    [Required]
    public ProcessStartInfo m_item2 { get; set; } = default!;

    public StartTaskArgs ToStartTaskArgs()
    {
        return new StartTaskArgs()
        {
            JobId = m_item1.JobId,
            TaskId = m_item1.TaskId,
            StartInfo = m_item2,
        };
    }

    //TODO: For logging
    public override string? ToString()
    {
        return base.ToString();
    }
}

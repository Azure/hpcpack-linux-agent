using System.Text.Json;

namespace NodeAgent.Models;

public class DiagBase
{
    public override string ToString()
    {
        var options = new JsonSerializerOptions() {  WriteIndented = true };
        return JsonSerializer.Serialize(this, GetType(), options);
    }
}

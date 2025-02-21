using System.ComponentModel.DataAnnotations;

namespace NodeAgent.Models;

public class NodeManagerConfig : DiagBase
{
    public string? TrustedCAFile { get; set; }

    public string? TrustedCAPath { get; set; }

    //No use anywhere?
    public bool UseDefaultCA { get; set; }

    public string? ClusterAuthenticationKey { get; set; }

    [Required]
    public string HeartbeatUri { get; set; } = default!;

    public string? TaskCompletionUri { get; set; }

    public string? MetricInstanceIdsUri { get; set; }

    public string? MetricUri { get; set; }

    [Required]
    public string RegisterUri { get; set; } = default!;

    [Required]
    //Server certificate public key file
    public string CertificateChainFile { get; set; } = default!;

    public string? PrivateKeyFile { get; set; }

    [Required]
    public string ListeningUri { get; set; } = default!;

    public bool Debug { get; set; }

    public int LogLevel { get; set; }

    [Required]
    public string[] NamingServiceUri { get; set; } = default!;

    [Required]
    public string DefaultServiceName { get; set; } = default!;

    [Required]
    public string UdpMetricServiceName { get; set; } = default!;

    public string? AzureInstanceMetaDataUri { get; set; }

    public int? HostsFetchInterval { get; set; }

    [Required]
    public string HostsFileUri { get; set; } = default!;

    public int HttpRequestTimeoutSeconds { get; set; }
}


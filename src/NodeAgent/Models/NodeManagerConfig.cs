using System.ComponentModel.DataAnnotations;

namespace NodeAgent.Models;

//TODO: Review the nullability of the properties
public class NodeManagerConfig : DiagBase
{
    public string TrustedCAFile { get; set; } = default!;

    public string TrustedCAPath { get; set; } = default!;

    public bool UseDefaultCA { get; set; }

    public string ClusterAuthenticationKey { get; set; } = default!;

    [Required]
    public string HeartbeatUri { get; set; } = default!;

    public string? TaskCompletionUri { get; set; }

    public string MetricInstanceIdsUri { get; set; } = default!;

    public string MetricUri { get; set; } = default!;

    [Required]
    public string RegisterUri { get; set; } = default!;

    [Required]
    //Server certificate public key file
    public string CertificateChainFile {  get; set; } = default!;

    public string PrivateKeyFile { get; set; } = default!;

    public string ListeningUri { get; set; } = default!;

    public bool Debug { get; set; }

    public int LogLevel { get; set; }

    [Required]
    public string[] NamingServiceUri { get; set; } = default!;

    [Required]
    public string DefaultServiceName { get; set; } = default!;

    [Required]
    public string UdpMetricServiceName { get; set; } = default!;

    public string AzureInstanceMetaDataUri { get; set; } = default!;

    public int? HostsFetchInterval { get; set; }

    public string? HostsFileUri { get; set; }

    public int HttpRequestTimeoutSeconds { get; set; }
}


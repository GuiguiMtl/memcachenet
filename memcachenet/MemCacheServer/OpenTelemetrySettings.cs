namespace memcachenet.MemCacheServer;

/// <summary>
/// Configuration settings for OpenTelemetry.
/// </summary>
public class OpenTelemetrySettings
{
    /// <summary>
    /// The service name for telemetry.
    /// </summary>
    public string ServiceName { get; set; } = "MemCacheNet";
    
    /// <summary>
    /// The service version for telemetry.
    /// </summary>
    public string ServiceVersion { get; set; } = "1.0.0";
    
    /// <summary>
    /// Whether tracing is enabled.
    /// </summary>
    public bool TracingEnabled { get; set; } = true;
    
    /// <summary>
    /// Exporter configuration.
    /// </summary>
    public ExporterSettings Exporters { get; set; } = new();
    
    /// <summary>
    /// Sampling configuration.
    /// </summary>
    public SamplingSettings Sampling { get; set; } = new();
}

/// <summary>
/// Configuration for telemetry exporters.
/// </summary>
public class ExporterSettings
{
    /// <summary>
    /// Console exporter settings.
    /// </summary>
    public ConsoleExporterSettings Console { get; set; } = new();
    
    /// <summary>
    /// OTLP exporter settings.
    /// </summary>
    public OtlpExporterSettings OTLP { get; set; } = new();
}

/// <summary>
/// Configuration for console exporter.
/// </summary>
public class ConsoleExporterSettings
{
    /// <summary>
    /// Whether the console exporter is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Console output targets (Console, Debug).
    /// </summary>
    public string Targets { get; set; } = "Console";
    
    /// <summary>
    /// Whether to include formatted message.
    /// </summary>
    public bool IncludeFormattedMessage { get; set; } = true;
    
    /// <summary>
    /// Whether to include scopes in the output.
    /// </summary>
    public bool IncludeScopes { get; set; } = true;
    
    /// <summary>
    /// Timestamp format for console output.
    /// </summary>
    public string TimestampFormat { get; set; } = "yyyy-MM-dd HH:mm:ss.fff";
}

/// <summary>
/// Configuration for OTLP exporter.
/// </summary>
public class OtlpExporterSettings
{
    /// <summary>
    /// Whether the OTLP exporter is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;
    
    /// <summary>
    /// The OTLP endpoint URL.
    /// </summary>
    public string Endpoint { get; set; } = "http://localhost:17011";
}

/// <summary>
/// Configuration for trace sampling.
/// </summary>
public class SamplingSettings
{
    /// <summary>
    /// The sampling type: AlwaysOn, AlwaysOff, or TraceIdRatio.
    /// </summary>
    public string Type { get; set; } = "AlwaysOn";
    
    /// <summary>
    /// The sampling ratio (0.0 to 1.0) when using TraceIdRatio sampling.
    /// </summary>
    public double Ratio { get; set; } = 1.0;
}
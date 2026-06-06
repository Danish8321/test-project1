namespace FundManagement.Api;

public class ObservabilityOptions
{
    public SerilogOptions Serilog { get; init; } = new();
    public OpenTelemetryOptions OpenTelemetry { get; init; } = new();
    public ApplicationInsightsOptions ApplicationInsights { get; init; } = new();
}

public class SerilogOptions
{
    public bool Enabled { get; init; }
}

public class ApplicationInsightsOptions
{
    public bool Enabled { get; init; }
    public string ConnectionString { get; init; } = string.Empty;
}

public class OpenTelemetryOptions
{
    public bool Enabled { get; init; }
    public string Endpoint { get; init; } = "http://localhost:4317";
    public string Protocol { get; init; } = "Grpc";
}

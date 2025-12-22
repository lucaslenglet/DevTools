namespace DevTools.Models;

class ConfigCommand
{
    public string ProcessName { get; init; } = default!;
    public string Name { get => field ?? ProcessName; init; }
    public string? WorkingDirectory { get; init; }
    public string? Arguments { get; init; }
    public string? Color { get; init; }
}
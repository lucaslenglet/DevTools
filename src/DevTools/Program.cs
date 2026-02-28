using System.Text;
using DevTools.Models;
using DevTools.Screens;
using DevTools.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

var context = ConfigurationManager.InitializeAppContext();
if (context.Config.Version != Config.CurrentVersion)
{
  AnsiConsole.MarkupLineInterpolated($"[red]Config version doesn't match tool version. ({context.Config.Version} != {Config.CurrentVersion})[/]");
  AnsiConsole.MarkupLineInterpolated($"[dim]{context.ConfigFilePath}[/]");
  return;
}

AnsiConsole.Clear();
AnsiConsole.Console.Profile.Encoding = Encoding.UTF8;

var cancellationSource = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) =>
{
  e.Cancel = true;
  cancellationSource.Cancel();
};

var services = new ServiceCollection()
  .AddSingleton(context)
  .AddSingleton<ConfigurationManager>()
  .AddSingleton<RepositoryScanner>()
  .AddSingleton(TimeProvider.System)
  .AddSingleton(AnsiConsole.Console)
  // Screens
  .AddTransient<RepositoriesScreen>()
  .AddTransient<RepositoryCommandsScreen>()
  .AddTransient<RepoPathsScreen>()
  .BuildServiceProvider();

// Run application
if (context.Config.RepoPaths.Count == 0)
{
  await services
    .GetRequiredService<RepoPathsScreen>()
    .ShowSettingsAsync(cancellationSource.Token)
    .ConfigureAwait(false);
}

await services
  .GetRequiredService<RepositoriesScreen>()
  .ShowForeverAsync(cancellationSource.Token)
  .ConfigureAwait(false);
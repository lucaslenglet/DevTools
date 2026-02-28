using System.Text;
using DevTools.Menus;
using DevTools.Models;
using DevTools.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

var context = ConfigurationManager.InitializeAppContext();
if (context == null) return;
if (context.Config.Version != Config.CurrentVersion)
{
  AnsiConsole.Markup($"[red]Config version doesn't match tool version. ({context.Config.Version} != {Config.CurrentVersion})[/]");
  return;
}

AnsiConsole.Clear();
AnsiConsole.Console.Profile.Encoding = Encoding.UTF8;

var services = new ServiceCollection()
  .AddSingleton(context)
  .AddSingleton<ConfigurationManager>()
  .AddSingleton<RepositoryScanner>()
  .AddSingleton(TimeProvider.System)
  .AddSingleton(AnsiConsole.Console)
  // Menus
  .AddSingleton<RepositoriesScreen>()
  .AddSingleton<RepositoryActionsMenu>()
  .BuildServiceProvider();

  // Run application
var cancellationSource = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) =>
{
    e.Cancel = true;
    cancellationSource.Cancel();
};
var console = services.GetRequiredService<IAnsiConsole>();
await services
  .GetRequiredService<RepositoriesScreen>()
  .ShowAsync(console, true, cancellationSource.Token)
  .ConfigureAwait(false);
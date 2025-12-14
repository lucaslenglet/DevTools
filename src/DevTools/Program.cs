using DevTools.Menus;
using DevTools.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

var context = ConfigurationManager.InitializeAppContext();
if (context == null) return;

AnsiConsole.Clear();

var services = new ServiceCollection()
  .AddSingleton(context)
  .AddSingleton<ConfigurationManager>()
  .AddSingleton<RepositoryScanner>()
  .AddSingleton(TimeProvider.System)
  .AddSingleton(AnsiConsole.Console)
  // Menus
  .AddSingleton<RepositoriesMenu>()
  .AddSingleton<RepositoryActionsMenu>()
  .BuildServiceProvider();

  // Run application
await services
  .GetRequiredService<RepositoriesMenu>()
  .ShowAsync()
  .ConfigureAwait(false);
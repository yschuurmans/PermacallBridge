using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;


namespace PermacallBridge
{
    class Program
    {
        //static void Main(string[] args)
        //{
        //    Bridge b = new Bridge();
        //    b.Loop()
        //    Console.WriteLine("Hello World!");
        //}


        public static async Task Main(string[] args)
        {
            var builder = new HostBuilder()
              .ConfigureAppConfiguration((hostingContext, config) =>
              {
                  config.AddJsonFile("appsettings.json", optional: true);
                  config.AddEnvironmentVariables();

                  if (args != null)
                  {
                      config.AddCommandLine(args);
                  }
              })
              .ConfigureServices((hostContext, services) =>
              {
                  services.AddOptions();

                  services.AddSingleton(new CommandService());
                  services.AddSingleton(new DiscordSocketClient());
                                    
                  services.AddSingleton<IHostedService, Bridge>();
              })
              .ConfigureLogging((hostingContext, logging) => {
                  logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                  logging.AddConsole();
              });
            await Task.Delay(1000);
            await builder.RunConsoleAsync();
        }

    }
}

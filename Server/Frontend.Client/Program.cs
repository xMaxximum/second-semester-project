using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

namespace Frontend.Client
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);

            builder.Services.AddMudServices();
            builder.Services.AddLocalization();
            builder.Services.AddMudLocalization();

            await builder.Build().RunAsync();
        }
    }
}

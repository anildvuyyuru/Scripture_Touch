using Microsoft.Extensions.DependencyInjection;
using ScriptureProjection.Interfaces;

namespace ScriptureProjection.Services
{
    public static class StartUp
    {
        static StartUp()
        {

        }
        public static IServiceCollection BuildContainer()
        {
            var serviceCollection = new ServiceCollection();
            return ConfigureServices(serviceCollection);

        }

        private static IServiceCollection ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<IGpioService, GpioService>();
            serviceCollection.AddTransient<IGpioSimulatorService, GpioServiceSimulator>();
            serviceCollection.AddTransient<IFileService, FileService>();
            serviceCollection.AddTransient<ITimerService, TimerService>();
            serviceCollection.AddTransient<IScriptureService, ScriptureService>();
            return serviceCollection;
        }
    }
}

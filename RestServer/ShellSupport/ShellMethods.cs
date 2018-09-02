using FmShell;
using RestServer.Model.Config;
using RestServer.Util;
using RestServer.Infrastructure.Shell;
using RestServer.Infrastructure.AspNetCore;
using System.Threading.Tasks;
using System.Linq;
using LiterCast;
using NLog;

namespace RestServer.ShellSupport
{
    internal sealed class ShellMethods
    {
        private static readonly ILogger LOGGER = LogManager.GetCurrentClassLogger();

        private ServerInfo Context { get; set; }
        private ServerController Controller { get; set; }
        private RadioCastServer RadioCastServer { get; set; }

        public ShellMethods(ServerInfo context, ServerController controller, RadioCastServer radioCastServer)
        {
            Context = context;
            Controller = controller;
            RadioCastServer = radioCastServer;
        }

        public void Stop(FmShellArguments args)
        {
            Task.Run( () => args.Shell.Stop() );
        }

        public string ShowApiUri(FmShellArguments args)
        {
            return Context.HostUri;
        }

        public string ShowRadioUri(FmShellArguments args)
        {
            return RadioCastServer.Endpoint.ToString();
        }

        public string Reload(FmShellArguments args)
        {
            Controller.ReloadConfiguration();
            return "Configuration reloaded successfully!";
        }

        public void RunFullGC(FmShellArguments args)
        {
            System.GC.Collect(System.Int32.MaxValue, System.GCCollectionMode.Forced, false);
        }

        public string Help(FmShellArguments args)
        {
            return $"Commands: " + typeof(ShellMethods).GetMethods().Where(mi => mi.IsPublic && mi.GetParameters().Length > 0 && mi.GetParameters().First().ParameterType == typeof(FmShellArguments)).Select(mi => mi.Name).Aggregate("", (one, acc) => one + "\n" + acc);
        }

        public void AddTrack(FmShellArguments args)
        {
            LOGGER.Info("Adding track by file path");
            var audioSource = new FileAudioSource(args.Args[0].ToString());
            RadioCastServer.AddTrack(audioSource);
        }
    }
}
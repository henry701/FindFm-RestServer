using FmShell;
using RestServer.Model.Config;
using RestServer.Util;
using RestServer.Infrastructure.Shell;
using RestServer.Infrastructure.AspNetCore;
using System.Threading.Tasks;
using System.Linq;

namespace RestServer.Shell
{
    internal sealed class ShellMethods
    {
        private readonly ServerInfo Context;
        private readonly ServerController Controller;

        public ShellMethods(ServerInfo context, ServerController controller)
        {
            Context = context;
            Controller = controller;
        }

        public void Stop(FmShellArguments args)
        {
            Task.Run( () => args.Shell.Stop() );
        }

        public string ShowUri(FmShellArguments args)
        {
            return Context.HostUri;
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
    }
}
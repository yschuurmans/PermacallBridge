using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PermacallBridge
{
    interface IVoiceApp
    {
        List<string> Users { get; }
        bool IsRunning { get; }

        Task<bool> AnyoneOnline();
        void PostNames(List<string> users);
        Task Quit();
        Task Run();
    }
}

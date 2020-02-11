using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PermacallBridge
{
    interface IVoiceApp
    {
        bool IsRunning { get; }

        Task<bool> AnyoneOnline();
        Task Quit();
        Task Run();
    }
}

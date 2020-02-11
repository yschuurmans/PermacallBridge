using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace PermacallBridge
{
    public class CompactLogger : ILoggerProvider
    {
        public void Dispose() { }

        public ILogger CreateLogger(string categoryName)
        {
            return new CustomConsoleLogger(categoryName);
        }

        public class CustomConsoleLogger : ILogger
        {
            private readonly string _categoryName;

            public CustomConsoleLogger(string categoryName)
            {
                _categoryName = categoryName;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                if (!IsEnabled(logLevel))
                {
                    return;
                }

                Console.WriteLine($"{logLevel.ToString().Substring(0,1)}: {formatter(state, exception)}");
                //Console.WriteLine($"{logLevel.ToString().Substring(0,1)}: {_categoryName}[{eventId.Id}]: {formatter(state, exception)}");
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public IDisposable BeginScope<TState>(TState state)
            {
                return null;
            }
        }
    }
}

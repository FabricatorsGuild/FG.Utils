using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace FG.Utils.BuildTools.Tests
{
    public class ConsoleDebugLogger : ILogger
    {
        private readonly bool _verbose;

        public ConsoleDebugLogger(bool verbose)
        {
            _verbose = verbose;
        }

        public void LogMessage(string message)
        {
            Console.WriteLine(message);
        }

        public void LogProgress()
        {
            if (!_verbose)
            {
                Console.Write(".");
            }
        }

        public void LogInformation(String message)
        {
            if (_verbose)
            {
                Console.WriteLine(message);
            }
        }
    }

    public class DebugLogger : ILogger
    {
        private readonly bool _verbose;

        public DebugLogger(bool verbose)
        {
            _verbose = verbose;
        }

        public void LogMessage(string message)
        {
            Debug.WriteLine(message);
        }

        public void LogProgress()
        {
            if (!_verbose)
            {
                Debug.Write(".");
            }
        }

        public void LogInformation(String message)
        {
            if (_verbose)
            {
                Debug.WriteLine(message);
            }
        }
    }

    public static class DebugHelper
    {
        public static IEnumerable<T> Debug<T>(this IEnumerable<T> that, ILogger logger = null)
        {
            logger = logger ?? new DebugLogger(true);
            var output = new StringBuilder();
            output.AppendLine($"Obj: {that.GetType().Name}:{that.GetHashCode()}");
            foreach (var item in that)
            {
                output.AppendLine($"\t{item.GetType().Name}:{item.GetHashCode()} - {item}");
            }
            logger.LogInformation(output.ToString());

            return that;
        }
    }
}
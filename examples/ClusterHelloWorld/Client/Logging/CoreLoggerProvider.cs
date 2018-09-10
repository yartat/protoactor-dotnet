using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BetLab.Common.Logging;
using BetLab.Logging.Fast;
using Microsoft.Extensions.Logging;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using BetLabLogLevel = BetLab.Common.Logging.LogLevel;

namespace Client.Logging
{
    /// <summary>
    /// Core logger provider to use Core log and standard .NET Core logger
    /// </summary>
    public class CoreLoggerProvider : ILoggerProvider
    {
        private readonly LogLevel _betlabLogLevel;

        /// <summary>
        /// Initializes new instance of the <see cref="CoreLoggerProvider"/> class.
        /// </summary>
        /// <param name="environmentName">The environment name.</param>
        /// <param name="loggerOptions">The logger options from configuration.</param>
        public CoreLoggerProvider(string environmentName, LoggerOptions loggerOptions)
        {
            var logstashFactory = new FastFileLoggerFactory("core", "test")
                .AddContextProperty("Environment", environmentName);
            Logger.SetLoggerFactory(logstashFactory);

            _betlabLogLevel = GetBetLabLogLevel(loggerOptions);
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }

        /// <inheritdoc />
        public ILogger CreateLogger(string categoryName) =>
            new CoreLogger(categoryName, _betlabLogLevel);

        private static LogLevel GetBetLabLogLevel(LoggerOptions loggerOptions)
        {
            LogLevel defaultLevel = LogLevel.Information;
            loggerOptions?.LogLevels?.TryGetValue("Default", out defaultLevel);
            return defaultLevel;
        }
    }

    /// <summary>
    /// Describes Core logger
    /// </summary>
    public class CoreLogger : ILogger
    {
        private readonly LogLevel _logLevel;
        private readonly ILog _log;

        /// <summary>
        /// Initializes new instance of the <see cref="CoreLogger"/> class
        /// </summary>
        /// <param name="categoryName">The logger category name.</param>
        /// <param name="betlabLogLevel">The log level from configuration.</param>
        public CoreLogger(string categoryName, LogLevel betlabLogLevel)
        {
            _logLevel = betlabLogLevel;
            _log = Logger.Create(Type.GetType(categoryName));
        }

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) =>
            logLevel >= _logLevel;

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId,
            TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel) || logLevel == LogLevel.None)
                return;
            if (formatter == null)
                throw new ArgumentNullException(nameof(formatter));

            var msg = formatter(state, exception);
            _log.Log(GetBetLabLogLevel(logLevel), exception, msg);
        }

        /// <inheritdoc />
        public IDisposable BeginScope<TState>(TState state)
        {
            return
                GetScopeContext(
                    state as string,
                    x => _log.Context.PushProperty("state", x)) ??
                GetScopeContextTuple(
                    state,
                    x => _log.Context.PushProperty(x.key, x.value)) ??
                GetScopeContext(
                    state as IEnumerable<(string key, object value)>,
                    x => _log.Context.PushProperty(x.key, x.value)) ??
                GetScopeContext(
                    state as IEnumerable<(string key, string value)>,
                    x => _log.Context.PushProperty(x.key, x.value)) ??
                GetScopeContext(
                    state as IEnumerable<KeyValuePair<string, object>>,
                    x => _log.Context.PushProperty(x.Key, x.Value)) ??
                GetScopeContext(
                    state as IEnumerable<KeyValuePair<string, string>>,
                    x => _log.Context.PushProperty(x.Key, x.Value));
        }

        private static IDisposable GetScopeContext(string state, Func<string, IDisposable> prop) =>
            string.IsNullOrEmpty(state) || prop == null
                ? null
                : new CoreLoggerScopeContext(new[] { prop(state) });

        private static IDisposable GetScopeContext<TState>(IEnumerable<TState> state, Func<TState, IDisposable> prop)
        {
            if (state == null || prop == null)
                return null;
            var properties = state.Select(prop).ToArray();
            return properties.Any()
                ? new CoreLoggerScopeContext(properties)
                : null;
        }

        private static IDisposable GetScopeContextTuple<TState>(TState state, Func<(string key, string value), IDisposable> prop)
        {
            if (state == null || prop == null)
                return null;

            var tupleFuncsDict = new Dictionary<Type, Func<(string, string)>>
            {
                [typeof((string, object))] = () => GetTuple<(string key, object value)>(state, x => x.key, x => x.value.ToString()),
                [typeof((string, string))] = () => GetTuple<(string key, string value)>(state, x => x.key, x => x.value),
                [typeof((string, bool))] = () => GetTuple<(string key, bool value)>(state, x => x.key, x => x.value.ToString()),
                [typeof((string, char))] = () => GetTuple<(string key, char value)>(state, x => x.key, x => x.value.ToString()),
                [typeof((string, sbyte))] = () => GetTuple<(string key, sbyte value)>(state, x => x.key, x => x.value.ToString()),
                [typeof((string, short))] = () => GetTuple<(string key, short value)>(state, x => x.key, x => x.value.ToString()),
                [typeof((string, int))] = () => GetTuple<(string key, int value)>(state, x => x.key, x => x.value.ToString()),
                [typeof((string, long))] = () => GetTuple<(string key, long value)>(state, x => x.key, x => x.value.ToString()),
                [typeof((string, byte))] = () => GetTuple<(string key, byte value)>(state, x => x.key, x => x.value.ToString()),
                [typeof((string, uint))] = () => GetTuple<(string key, uint value)>(state, x => x.key, x => x.value.ToString()),
                [typeof((string, ushort))] = () => GetTuple<(string key, ushort value)>(state, x => x.key, x => x.value.ToString()),
                [typeof((string, ulong))] = () => GetTuple<(string key, ulong value)>(state, x => x.key, x => x.value.ToString()),
                [typeof((string, float))] = () => GetTuple<(string key, float value)>(state, x => x.key, x => x.value.ToString(CultureInfo.InvariantCulture)),
                [typeof((string, double))] = () => GetTuple<(string key, double value)>(state, x => x.key, x => x.value.ToString(CultureInfo.InvariantCulture)),
                [typeof((string, decimal))] = () => GetTuple<(string key, decimal value)>(state, x => x.key, x => x.value.ToString(CultureInfo.InvariantCulture)),
            };

            if (tupleFuncsDict.TryGetValue(state.GetType(), out var getTuple))
                return new CoreLoggerScopeContext(new[] { prop(getTuple()) });

            return null;
        }

        private static (string, string) GetTuple<TTuple>(object obj, Func<TTuple, string> getKey, Func<TTuple, string> getValue)
            where TTuple : struct
        {
            TTuple tuple = (TTuple)Convert.ChangeType(obj, typeof(TTuple));
            return (getKey?.Invoke(tuple), getValue?.Invoke(tuple));
        }

        private static BetLabLogLevel GetBetLabLogLevel(LogLevel logLevel)
        {
            var logLevelsMap = new Dictionary<LogLevel, BetLabLogLevel>
            {
                [LogLevel.Trace] = BetLabLogLevel.Verbose,
                [LogLevel.Debug] = BetLabLogLevel.Debug,
                [LogLevel.Information] = BetLabLogLevel.Info,
                [LogLevel.Warning] = BetLabLogLevel.Warn,
                [LogLevel.Error] = BetLabLogLevel.Error,
                [LogLevel.Critical] = BetLabLogLevel.Fatal,
            };

            return !logLevelsMap.TryGetValue(logLevel, out var betlabLogLevel) ? 
                BetLabLogLevel.Verbose : 
                betlabLogLevel;
        }
    }

    internal class CoreLoggerScopeContext : IDisposable
    {
        private readonly IEnumerable<IDisposable> _properties;

        /// <summary>
        /// Initializes a new instance of the <see cref="CoreLoggerScopeContext"/> class.
        /// </summary>
        /// <param name="properties">The properties.</param>
        public CoreLoggerScopeContext(IEnumerable<IDisposable> properties) =>
            _properties = properties;

        /// <inheritdoc />
        public void Dispose()
        {
            if (_properties == null)
                return;

            foreach (var property in _properties)
            {
                property?.Dispose();
            }
        }
    }
}
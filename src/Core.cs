using Harmony;
using HBS.Logging;
using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;

namespace BattleValue
{
    public class Core
    {
        private const string ModName = "BattleValue";
        private const string LogPrefix = "[BattleValue]";

        private static ILog? logger;
        private static FileLogAppender? logAppender;

        internal static ModSettings Settings { get; private set; } = new ModSettings();

        public static void Init(string directory, string settingsJson)
        {
            logger = Logger.GetLogger(ModName, LogLevel.Debug);
            SetupLogging(directory);

            Settings = JsonConvert.DeserializeObject<ModSettings>(settingsJson) ?? new ModSettings();
            Log($"Settings : {JsonConvert.SerializeObject(Settings, Formatting.Indented)}");

            var harmonyInstance = HarmonyInstance.Create("bhtrail.battlevalue");
            harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
        }

        #region Logging
        internal static void SetupLogging(string Directory)
        {
            var logFilePath = Path.Combine(Directory, "log.txt");

            try
            {
                ShutdownLogging();
                AddLogFileForLogger(logFilePath);
            }
            catch (Exception e)
            {
                logger.Log($"{ModName}: can't create log file", e);
            }
        }

        internal static void ShutdownLogging()
        {
            if (logAppender == null)
            {
                return;
            }

            try
            {
                HBS.Logging.Logger.ClearAppender(ModName);
                logAppender.Flush();
                logAppender.Close();
            }
            catch
            {
            }
        }

        private static void AddLogFileForLogger(string logFilePath)
        {
            try
            {
                logAppender = new FileLogAppender(logFilePath, FileLogAppender.WriteMode.INSTANT);
                Logger.AddAppender(ModName, logAppender);

            }
            catch (Exception e)
            {
                logger.Log($"{ModName}: can't create log file", e);
            }
        }

        public static void Log(string message)
        {
            logger.Log(LogPrefix + message);
        }

        public static void LogError(string message)
        {
            logger.LogError(LogPrefix + message);
        }

        public static void LogError(string message, Exception e)
        {
            logger.LogError(LogPrefix + message, e);
        }

        public static void LogError(Exception e)
        {
            logger.LogError(LogPrefix, e);
        }
        #endregion
    }
}

using NLog;
using NLog.Config;
using NLog.Targets;

namespace BlockLimiter.Settings
{
    public static class LoggingConfig
    {

        public static void Set()
        {
            var rules = LogManager.Configuration.LoggingRules;


            for (int i = rules.Count - 1; i >= 0; i--) {

                var rule = rules[i];

                if (rule.LoggerNamePattern != "BlockLimiter")continue;
                rules.RemoveAt(i);
            }

            var config = BlockLimiterConfig.Instance;

            if (string.IsNullOrEmpty(config.LogFileName))
            {
                LogManager.Configuration.Reload();
                return;
            }

            var logTarget = new FileTarget
            {
                FileName = "Logs/" + config.LogFileName,
                Layout ="${var:logStamp} ${var:logContent}"
            };
            
            var fullRule = new LoggingRule("BlockLimiter",LogLevel.Debug, logTarget){Final = true};
            
            LogManager.Configuration.LoggingRules.Insert(0,fullRule);
            LogManager.Configuration.Reload();
        }
    }
}
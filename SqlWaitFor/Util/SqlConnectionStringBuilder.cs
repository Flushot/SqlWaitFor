using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SqlWaitFor.Util
{
    /// <summary>
    /// SQL Server connection string builder
    /// </summary>
    public class SqlConnectionStringBuilder
    {
        private Dictionary<String, String> config = new Dictionary<String, String>();
        private SqlConnectionStringBuilder failoverPartner;

        private void SetConfig(String key, bool value)
        {
            SetConfig(key, value ? "true" : "false");
        }
        private void SetConfig(String key, String value)
        {
            if (config.ContainsKey(key))
                config.Remove(key);

            config.Add(key, value);
        }

        public SqlConnectionStringBuilder Instance(String instance)
        {
            SetConfig("#instance", instance);
            return this;
        }

        public SqlConnectionStringBuilder Port(int port)
        {
            if (port == 0)
            {
                if (config.ContainsKey("#port"))
                    config.Remove("#port");
            }
            else
                SetConfig("#port", port.ToString());

            return this;
        }

        public SqlConnectionStringBuilder Username(String username)
        {
            if (username == null)
            {
                if (config.ContainsKey("#username"))
                    config.Remove("#username");
            }
            else
                SetConfig("#username", username);

            return this;
        }

        public SqlConnectionStringBuilder Password(String password)
        {
            SetConfig("#password", password);
            return this;
        }

        public SqlConnectionStringBuilder Database(String database)
        {
            SetConfig("Initial Catalog", database);
            return this;
        }

        public SqlConnectionStringBuilder Application(String application)
        {
            SetConfig("Application Name", application);
            return this;
        }

        public SqlConnectionStringBuilder MinPoolSize(int minPoolSize)
        {
            SetConfig("Min Pool Size", minPoolSize.ToString());
            return this;
        }

        public SqlConnectionStringBuilder MaxPoolSize(int maxPoolSize)
        {
            SetConfig("Max Pool Size", maxPoolSize.ToString());
            return this;
        }

        public SqlConnectionStringBuilder MultipleActiveResultSets(bool mars)
        {
            SetConfig("MultipleActiveResultSets", mars);
            return this;
        }

        public SqlConnectionStringBuilder FailoverPartner(String instance)
        {
            SetConfig("#partner.instance", instance);
            failoverPartner = null;
            return this;
        }

        public SqlConnectionStringBuilder FailoverPartner(SqlConnectionStringBuilder builder)
        {
            if (config.ContainsKey("#partner.instance"))
                config.Remove("#partner.instance");
            failoverPartner = builder;
            return this;
        }

        public String Build()
        {
            var connStr = new StringBuilder();

            // Instance
            if (!config.ContainsKey("#instance"))
                throw new ArgumentException("Instance is required");
            connStr.Append("Server=" + config["#instance"]);
            if (config.ContainsKey("#port"))
            {
                connStr.Append(",");
                connStr.Append(config["#port"]);
            }

            // Failover Partner
            if (config.ContainsKey("#partner.instance"))
            {
                connStr.Append("; Failover Partner=" + config["#partner.instance"]);
                if (config.ContainsKey("#partner.port"))
                {
                    connStr.Append(",");
                    connStr.Append(config["#partner.port"]);
                }
            }
            else if (failoverPartner != null)
            {
                if (!failoverPartner.config.ContainsKey("#instance"))
                    throw new ArgumentException("FailoverPartner has no Instance");
                connStr.Append("; Failover Partner=" + failoverPartner.config["#instance"]);
                if (failoverPartner.config.ContainsKey("#port"))
                {
                    connStr.Append(",");
                    connStr.Append(failoverPartner.config["#port"]);
                }
            }

            // Credentials
            if (config.ContainsKey("#username"))
            {
                connStr.Append("; User Id=" + config["#username"]);
                connStr.Append("; Password=" + config["#password"]);
            }
            else
                connStr.Append("; Integrated Security=true");

            // Misc
            foreach (String key in config.Keys)
            {
                if (key.StartsWith("#"))
                    continue;

                connStr.Append("; ");
                connStr.Append(key);
                connStr.Append("=");
                connStr.Append(config[key]);
            }

            return connStr.ToString();
        }

        public override string ToString()
        {
            return Build();
        }
    }
}

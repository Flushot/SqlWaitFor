using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.Data;
using System.Reflection;
using SqlWaitFor.Util;

namespace SqlWaitFor.Sql
{
    /// <summary>
    /// 
    /// </summary>
    public class SqlProcess
    {
        public int SPID { get; private set; }
        public int ECID { get; private set; }
        public String Login { get; private set; }
        public String Status { get; private set; }
        public String HostName { get; private set; }
        public String ProgramName { get; private set; }
        public String LastWait { get; private set; }
        public int BlockedBy { get; private set; }
        public int CPU { get; private set; }
        public int PhysicalIO { get; private set; }
        public int Memory { get; private set; }
        public int Transactions { get; private set; }
        public String Database { get; private set; }
        public String Command { get; private set; }
        public int RequestID { get; private set; }

        public bool IsSleeping
        {
            get
            {
                return String.Compare(Status, "sleeping", true) == 0;
            }
        }

        public override string ToString()
        {
            return String.Format("Process-{0}", SPID);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="loginName">One of: 'login' | session ID | 'ACTIVE'</param>
        /// <returns></returns>
        public static List<SqlProcess> GetProcessList(SqlConnection conn, String loginName)
        {
            var result = new List<SqlProcess>();

            var cmd = new SqlCommand(String.Concat(
                "select p.spid, p.status, p.loginame, p.hostname, p.program_name, p.blocked, p.lastwaittype, ",
	                "p.cpu, p.physical_io, p.memusage, p.open_tran, d.name as dbname, q.text ", 
	                "from sys.sysprocesses p ",
                    "left join sys.databases d on d.database_id = p.dbid ",
	                "cross apply sys.dm_exec_sql_text(sql_handle) as q",
                    loginName != null ?
                        " where p.loginame = @loginame" : ""
                ), conn);
            if (loginName != null)
                cmd.Parameters.AddWithValue("@loginame", loginName);

            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    result.Add(
                        new SqlProcess
                        {
                            SPID = reader.GetInt16(0),
                            Status = DbUtils.GetCol<String>("status", reader).Trim(),
                            Login = DbUtils.GetCol<String>("loginame", reader).Trim(),
                            HostName = DbUtils.GetCol<String>("hostname", reader).Trim(),
                            ProgramName = DbUtils.GetCol<String>("program_name", reader).Trim(),
                            LastWait = DbUtils.GetCol<String>("lastwaittype", reader).Trim(),
                            BlockedBy = DbUtils.GetCol<int>("blocked", reader),
                            CPU = DbUtils.GetCol<int>("cpu", reader),
                            PhysicalIO = DbUtils.GetCol<int>("physical_io", reader),
                            Memory = DbUtils.GetCol<int>("memusage", reader),
                            Transactions = DbUtils.GetCol<int>("open_tran", reader),
                            Database = DbUtils.GetCol<String>("dbname", reader).Trim(),
                            Command = DbUtils.GetCol<String>("text", reader).Trim(),
                        });
                }
            }

            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="conn"></param>
        /// <returns></returns>
        public static List<SqlProcess> GetProcessList(SqlConnection conn)
        {
            return GetProcessList(conn, null);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="spid"></param>
        /// <returns></returns>
        public static SqlProcess GetProcess(SqlConnection conn, int spid)
        {
            return GetProcessList(conn)[spid];
        }
    }
}

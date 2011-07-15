using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.Data;
using System.Threading;
using System.Net.Mail;
using System.Net;
using SqlWaitFor.Sql;
using Gnu.Getopt;
using System.Reflection;
using System.Diagnostics;
using SqlWaitFor.Util;

namespace SqlWaitFor
{
    class Program
    {
        private static String AppName
        {
            get
            {
                return AppDomain.CurrentDomain.FriendlyName.ToLower().Split('.')[0];
            }
        }

        static void Banner()
        {
            Console.WriteLine(String.Format("SqlWaitFor v{0}", 
                Assembly.GetExecutingAssembly().GetName().Version));
            Console.WriteLine("Copyright (c)2011 Chris Lyon");
            Console.WriteLine();
        }

        static void Usage()
        {
            Usage(null);
        }
        static void Usage(String usageError)
        {
            Banner();

            if (usageError != null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine(String.Format("{0}: {1}", AppName, usageError));
                Console.ResetColor();
            }

            Console.WriteLine(
                String.Concat(
                    "usage: {0} [-options]\n",
                    "summary:\n",
                    "   monitors the a set of processes (or all processes belonging to a specific\n",
                    "   user on a sql server instance. when the processes finish, the program\n",
                    "   will exit. this may be useful in batch scripts.\n",
                    "\nrequired:\n",
                    "  -S instance           sql server instance to connect to\n",
                    "  -s spid1,spid2,spidN  spid or spids to monitor\n",
                    "  -L login              login name to monitor\n",
                    "\nauthentication (pick one method):\n",
                    "  -E                    use windows authentication (default)\n",
                    "  -u username           username for sql authentication\n",
                    "  -p password           password for sql authentication\n",
                    "\noptional:\n",
                    "  -v              verbose output (includes detailed process/query info)\n",
                    "  -q              quiet mode (no output displayed unless its an error)\n",
                    "  -P port         server port to connect to\n",
                    "  -t seconds      seconds to wait in between checks (default: 5)\n",
                    "  -h              help (this screen)\n",
                    "\nexamples:\n",
                    "  {0} -S server\\instance -E -s 13\n",
                    "  {0} -S server\\instance -u user -p pass -s 12,20,567\n",
                    "  {0} -S server -P 1434 -E -v -L domain\\username\n"), 
                    AppName);

#if DEBUG
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
#endif

            Environment.Exit(2);
        }

        static void Main(string[] args)
        {
            // Get CLI arguments
            bool verbose = false, silent = false, trusted = false;
            String server = null, username = null, password = null, login = null;
            var pids = new List<int>();
            int port = 0, seconds = 5;

            var getopt = new Getopt(AppName, args, "hvqP:Eu:p:s:L:S:t:");
            getopt.Opterr = false; // Suppress warning messages to stdout
            int c;
            while ((c = getopt.getopt()) != -1)
            {
                switch (c)
                {
                    case 'h': // Help
                        Usage();
                        break;
                    case 'v': // Verbose
                        if (silent)
                            Usage("invalid option: -v with -q");
                        verbose = true;
                        break;
                    case 'q': // Quiet
                        if (verbose)
                            Usage("invalid option: -q with -v");
                        silent = true;
                        break;
                    case 'S': // Server
                        server = getopt.Optarg;
                        break;
                    case 'P': // Port
                        port = int.Parse(getopt.Optarg);
                        if (port < 1)
                            Usage(String.Format(String.Format("invalid port for -P -- {0}", getopt.Optarg)));
                        break;
                    case 'E': // Trusted
                        if (username != null)
                            Usage("invalid option: -E with -u");
                        if (password != null)
                            Usage("invalid option: -E with -p");
                        trusted = true;
                        break;
                    case 'u': // Username
                        if (trusted)
                            Usage("invalid option: -u with -E");
                        username = getopt.Optarg;
                        break;
                    case 'p': // Password
                        if (trusted)
                            Usage("invalid option: -p with -E");
                        password = getopt.Optarg;
                        break;
                    case 's': // SPID(s) to monitor
                        if (login != null)
                            Usage("invalid option: -x with -L");
                        {
                            var pidList = getopt.Optarg;
                            if (String.IsNullOrEmpty(pidList))
                                Usage("pid or pids are missing");
                            foreach (String pidStr in pidList.Split(','))
                            {
                                var pid = int.Parse(pidStr);
                                if (pid < 1)
                                    Usage(String.Format("invalid spid(s) for -s -- {0}", pidStr));
                                pids.Add(pid);
                            }
                        }
                        break;
                    case 'L': // Login to monitor
                        if (pids.Count > 0)
                            Usage("invalid option: -L with -x");
                        login = getopt.Optarg;
                        break;
                    case 't': // Seconds to wait
                        seconds = int.Parse(getopt.Optarg);
                        if (seconds < 1)
                            Usage(String.Format("invalid timeout for -t -- {0}", getopt.Optarg));
                        break;
                    case '?':
                        Usage(String.Format("invalid argument -{0}", (char)getopt.Optopt));
                        break;
                }
            }

            // Default to trusted authentication
            if (!trusted && username == null)
                trusted = true;

            // Argument presence validation
            if ( (server == null) ||
                 (login == null && pids.Count == 0) )
                Usage("required argument(s) missing");

            if (!silent)
                Banner();

            // Start monitoring
            using (var conn = new SqlConnection(
                new SqlWaitFor.Util.SqlConnectionStringBuilder()
                    .Instance(server)
                    .Port(port)
                    .Username(username)
                    .Password(password)
                    .Application(AppName)
                    .Build()))
            {
#if !DEBUG
                try
                {
#endif
                    conn.Open();
                    var mySPID = DbUtils.GetSPID(conn);
                    if (!silent)
                        Console.WriteLine("SPID {0} is monitoring SQL Server processes (^C cancels)...", mySPID);

                    while (true)
                    {
                        var procs = SqlProcess.GetProcessList(conn, login);
                        var relevantProcs = pids.Count > 0 ?
                            procs.Where(p => 
                                p.SPID != mySPID &&
                                pids.Contains(p.SPID)
                            ) :
                            procs.Where(p => p.SPID != mySPID);
                        var liveProcs = relevantProcs.Where(p => !p.IsSleeping).ToList();
                        if (liveProcs.Count == 0)
                        {
                            if (!silent && verbose)
                            {
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine("*** [FINISH] No active queries found!");
                                Console.ResetColor();
#if DEBUG
                                Console.WriteLine();
                                Console.WriteLine("Press any key to exit...");
                                Console.ReadKey();
#endif
                            }
                            break;
                        }

                        if (!silent && verbose)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("*** [WAIT] {0}/{1} queries active at {2}", liveProcs.Count, relevantProcs.Count(), DateTime.Now);
                            Console.ResetColor();
                            foreach (var liveProc in liveProcs)
                            {
                                Console.ForegroundColor = ConsoleColor.White;
                                Console.WriteLine("SPID {0} -> State: {1}, CPU: {2}, I/O: {3}, Memory: {4}, Database: {5}",
                                    liveProc.SPID,
                                    liveProc.Status.ToLower(),
                                    liveProc.CPU,
                                    liveProc.PhysicalIO,
                                    liveProc.Memory,
                                    liveProc.Database);
                                Console.ForegroundColor = ConsoleColor.Gray;
                                Console.WriteLine(liveProc.Command);
                            }
                            Console.ResetColor();
                            Console.WriteLine();
                        }

                        Thread.Sleep(seconds * 1000);
                    }
#if !DEBUG
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine("*** [ERROR] {0}", ex.Message);
                    Console.ResetColor();
                    Environment.Exit(1);
                }
#endif
            }
        }
    }
}

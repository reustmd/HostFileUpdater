using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace HostFileUpdater
{
	public class Program
	{
		public static void Main(string[] args)
		{
			try
			{
				var nsLookupTarget = ConfigurationManager.AppSettings["NsLookupTarget"];
				var hostFileUrls = ConfigurationManager.AppSettings["HostFileUrls"].Split(',');

				var ipAddress = TryNsLookup(nsLookupTarget);
				AddOrUpdateHostFile(hostFileUrls, ipAddress);
			}
			catch (Exception ex)
			{
				EventLog.WriteEntry("HostFileUpdater", ex.Message, EventLogEntryType.Error);
			}
		}

		//todo: add file lock around entirety of read/write operations
		private static void AddOrUpdateHostFile(IEnumerable<string> urls, string ipAddress)
		{
			var pathToHostFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers/etc/hosts");

			var hostFileLines = File.ReadAllLines(pathToHostFile);
			var linesToMaintain = hostFileLines.Where(hostFileLine => !urls.Any(url => hostFileLine.IndexOf(url, StringComparison.OrdinalIgnoreCase) >= 0));
			
			var newHostFile = linesToMaintain.ToList();
			newHostFile.AddRange(urls.Select(url => string.Concat(ipAddress, " ", url)));

			File.WriteAllLines(pathToHostFile, newHostFile);
		}

		private static string TryNsLookup(string url)
		{
			var startInfo = new ProcessStartInfo("nslookup") { RedirectStandardInput = true, RedirectStandardOutput = true, CreateNoWindow = true, UseShellExecute = false };
			var nslookup = new Process { StartInfo = startInfo };
			nslookup.Start();
			nslookup.StandardInput.WriteLine(url);
			nslookup.StandardInput.Flush();

			while (!nslookup.StandardOutput.EndOfStream)
			{
				var line = nslookup.StandardOutput.ReadLine();
				const string relevantLinePrefix = "Addresses:";

				if (!line.StartsWith(relevantLinePrefix))
					continue;

				return line.Replace(relevantLinePrefix, string.Empty).Trim();
			}

			throw new ApplicationException("Failed to parse nslookup for " + url);
		}
	}
}

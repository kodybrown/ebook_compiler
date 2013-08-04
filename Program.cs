using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace ebook_compiler
{
	public class Program
	{
		public static bool DEBUG;
		public static bool DEBUG2;
		public static bool PAUSE;
		public static bool RESET;

		public static int Main( string[] args )
		{
			string p;
			string tmp;
			List<FileInfo> mdfiles;
			FileInfo fil;
			List<string> htmlfiles;
			StringBuilder res;
			string templatefile;
			string header;
			string footer;
			string title;
			List<string> exts;
			string outfile;
			DateTime startTime;
			TimeSpan tspan;

			DEBUG = true;
			DEBUG2 = false;
			PAUSE = false;
			RESET = false;

			foreach (string a in args) {
				if (a.Equals("pause", StringComparison.CurrentCultureIgnoreCase)) {
					PAUSE = true;
				} else if (a.Equals("debug", StringComparison.CurrentCultureIgnoreCase)) {
					DEBUG = true;
					DEBUG2 = true;
				} else if (a.Equals("reset", StringComparison.CurrentCultureIgnoreCase)) {
					RESET = true;
				} else {
					Console.WriteLine("Unknown command-line argument: " + a);
					return 1;
				}
			}

			Console.WriteLine("=> Processing..");

			startTime = DateTime.Now;
			p = Environment.CurrentDirectory;

			if (DEBUG) {
				Console.WriteLine("   Loading _ebook_config.txt..");
			}
			LoadConfig(p, out title, out exts);
			outfile = Path.Combine(p, title + ".html");

			tmp = Path.Combine(Path.GetTempPath(), "ebook_compiler-" + Path.GetFileName(title));
			mdfiles = new List<FileInfo>();
			templatefile = Path.Combine(p, "_ebook_template.txt");
			header = string.Empty;
			footer = string.Empty;

			if (!Directory.Exists(tmp)) {
				Directory.CreateDirectory(tmp);
			}

			if (RESET) {
				Console.WriteLine("   Resetting..");
				foreach (string f in Directory.GetFiles(tmp, "*.*", SearchOption.TopDirectoryOnly)) {
					if (File.Exists(f)) {
						File.SetAttributes(f, FileAttributes.Normal);
						File.Delete(f);
					}
				}
			}

			// Get all of the source files
			foreach (string ext in exts) {
				mdfiles.AddRange(new DirectoryInfo(p).GetFiles(ext, SearchOption.TopDirectoryOnly));
			}

			if (DEBUG) {
				Console.WriteLine("   Sorting input..");
			}
			mdfiles.Sort(delegate( FileInfo a, FileInfo b )
			{
				int result;
				long aa;
				long bb;
				aa = GetNumFromName(a.Name);
				bb = GetNumFromName(b.Name);
				if ((result = aa.CompareTo(bb)) == 0) {
					return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
				}
				return result;
			});

			// Go through all of the source files
			if (DEBUG) {
				Console.WriteLine("   Verifying files..");
			}
			foreach (FileInfo f in mdfiles) {
				fil = new FileInfo(Path.Combine(tmp, Path.GetFileNameWithoutExtension(f.Name) + ".html"));
				// Does a generated html file exist for the current file?
				if (fil.Exists) {
					// Does the file html need to be updated?
					if (f.LastWriteTime != fil.LastWriteTime) {
						// Regenerate html file
						Compile(f, fil);
					} else {
						if (DEBUG2) {
							Console.WriteLine("      File is up to date: " + f.Name);
						}
					}
				} else {
					// Generate html file
					Compile(f, fil);
				}
			}

			// Get all of the generated html files
			htmlfiles = new List<string>();
			htmlfiles.AddRange(Directory.GetFiles(tmp, "*.html", SearchOption.TopDirectoryOnly));

			// Ensure there are no extra (old) html files where the source no longer exists.
			if (DEBUG) {
				Console.WriteLine("   Cleaning up..");
			}
			for (int i = htmlfiles.Count - 1; i >= 0; i--) {
				if (mdfiles.Find(delegate( FileInfo f ) { return Path.GetFileNameWithoutExtension(f.Name).Equals(Path.GetFileNameWithoutExtension(htmlfiles[i]), StringComparison.CurrentCultureIgnoreCase); }) == null) {
					File.SetAttributes(htmlfiles[i], FileAttributes.Normal);
					File.Delete(htmlfiles[i]);
					htmlfiles.RemoveAt(i);
				}
			}

			// Sort the generated html files (just like the .md files)
			if (DEBUG) {
				Console.WriteLine("   Sorting output..");
			}
			htmlfiles.Sort(delegate( string a, string b )
			{
				int result;
				long aa;
				long bb;
				aa = GetNumFromName(Path.GetFileName(a));
				bb = GetNumFromName(Path.GetFileName(b));
				if ((result = aa.CompareTo(bb)) == 0) {
					return string.Compare(a, b, StringComparison.CurrentCultureIgnoreCase);
				}
				return result;
			});

			// Add the template file to the top of our output file
			if (File.Exists(templatefile)) {
				int pos;
				if (DEBUG) {
					Console.WriteLine("   Applying template..");
				}
				header = File.ReadAllText(templatefile).Trim();
				if ((pos = header.IndexOf("@SPLIT@")) > -1) {
					footer = "\r\n\r\n\r\n" + header.Substring(pos + "@SPLIT@".Length).Trim();
					header = header.Substring(0, pos).Trim() + "\r\n\r\n\r\n";
				} else {
					footer = string.Empty;
				}
			}

			// Write to the final output file!
			if (DEBUG) {
				Console.WriteLine("\r\n   Creating final output:");
				Console.WriteLine("----> " + Path.GetFileName(outfile));
			}
			res = new StringBuilder();
			res.AppendLine(header);

			foreach (string f in htmlfiles) {
				res.AppendLine(File.ReadAllText(f))
					.AppendLine().AppendLine().AppendLine();
			}

			res.AppendLine(footer);

			if (File.Exists(outfile)) {
				File.SetAttributes(outfile, FileAttributes.Normal);
				File.Delete(outfile);
			}
			File.WriteAllText(outfile, res.ToString());

			Console.WriteLine("\r\n   Process is complete.");
			//Thread.Sleep(250);

			if (PAUSE) {
				Console.Write("   Press any key to continue ");
				Console.ReadKey(true);
				Console.WriteLine();
			}

			tspan = DateTime.Now - startTime;
			if (tspan.TotalMilliseconds < 500) {
				Thread.Sleep(500 - (int)tspan.TotalMilliseconds);
			}

			return 0;
		}

		private static void LoadConfig( string p, out string title, out List<string> exts )
		{
			string file;
			string[] ar;
			string arg;

			file = Path.Combine(p, "_ebook_config.txt");
			title = string.Empty;
			exts = new List<string>();

			if (File.Exists(file)) {
				foreach (string l in File.ReadAllLines(file)) {
					if (l.StartsWith(";") || l.StartsWith("#") || l.Length == 0 || l.IndexOf("=") == -1) {
						continue;
					}

					ar = l.Split(new char[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries);
					arg = ar[0].Trim();

					if (arg.Equals("title", StringComparison.CurrentCultureIgnoreCase)) {
						title = ar[1].Trim();
					} else if (arg.Equals("ext", StringComparison.CurrentCultureIgnoreCase)) {
						exts.Add("*." + ar[1].Trim().TrimStart('*', '.'));
					}
				}
			}

			if (title.Length == 0) {
				title = "ebook";
			}
			if (exts.Count == 0) {
				exts.Add("*.md");
			}
		}

		private static long GetNumFromName( string name )
		{
			long result;
			char ch;

			result = 0;

			for (int i = 0; i < name.Length; i++) {
				ch = name[i];
				if (ch >= '0' && ch <= '9') {
					result = (result * 10) + (ch - '0');
				} else {
					break;
				}
			}

			//return (result > 0) ? result : long.MaxValue;
			return result;
		}

		private static void Compile( FileInfo input, FileInfo output )
		{
			Process p;
			ProcessStartInfo info;
			string md;

// my batch, used for other purposes as well, basically does this..
// "c:\Tools\Perl64\bin\perl.exe" "c:\Kody\Root\Markdown.pl" --html4tags "%~1" > "%~2"
			md = @"C:\Kody\Root\Markdown.bat";

			info = new ProcessStartInfo();
			info.FileName = md;
			info.Arguments = "\"" + input.FullName + "\" \"" + output.FullName + "\"";
			info.WorkingDirectory = input.Directory.FullName;
			info.WindowStyle = ProcessWindowStyle.Hidden;

			if (DEBUG) {
				Console.WriteLine("      Compiling: " + input.Name);
			}

			p = Process.Start(info);
			p.WaitForExit(10000);

			if (p.ExitCode != 0) {
				Console.WriteLine("**** Error compiling: " + input.FullName);
			}

			output.CreationTime = input.CreationTime;
			output.LastAccessTime = input.LastAccessTime;
			output.LastWriteTime = input.LastWriteTime;
		}
	}
}

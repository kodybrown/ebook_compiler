using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using Ionic.Zip;

namespace ebook_compiler
{
	public class Program
	{
		static bool DEBUG;
		static bool DEBUG2;
		static bool PAUSE;
		static bool RESET;
		static int TOCLEVELS;

		static bool outputFileNamesAsTitle;

		static string tmp;
		static string md_compiler;

		public static int Main( string[] args )
		{
			string p;
			string configfile;
			List<FileInfo> mdfiles;
			FileInfo fil;
			FileInfo filtoc;
			List<string> htmlfiles;
			StringBuilder res;
			string templatefile;
			string header;
			string footer;
			string title;
			string tocfile;
			List<string> exts;
			string outfile;
			string zipname;
			string zipfile;
			string resfoldername;
			string respath;
			List<string> resources;
			DateTime startTime;
			TimeSpan tspan;

			startTime = DateTime.Now;
			p = Environment.CurrentDirectory;

			Console.WriteLine("=> Processing..");

			DEBUG = true;
			DEBUG2 = false;
			PAUSE = false;
			RESET = false;
			TOCLEVELS = 3;
			outputFileNamesAsTitle = false;

			configfile = string.Empty;

			foreach (string a in args) {
				if (a.Equals("pause", StringComparison.CurrentCultureIgnoreCase)) {
					PAUSE = true;
				} else if (a.Equals("debug", StringComparison.CurrentCultureIgnoreCase)) {
					DEBUG = true;
					DEBUG2 = true;
					outputFileNamesAsTitle = true;
				} else if (a.Equals("reset", StringComparison.CurrentCultureIgnoreCase)) {
					RESET = true;
				} else if (a.StartsWith("config:", StringComparison.CurrentCultureIgnoreCase)) {
					configfile = a.Substring("config:".Length).Trim();
				} else if (a.StartsWith("toc:", StringComparison.CurrentCultureIgnoreCase)) {
					int.TryParse(a.Substring("toc:".Length).Trim(), out TOCLEVELS);
				} else {
					Console.WriteLine("Unknown command-line argument: " + a);
					return 1;
				}
			}

			if (configfile.Length == 0) {
				configfile = Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location) + ".config"; // "_ebook_config.txt";
			}
			if (!Path.IsPathRooted(configfile)) {
				configfile = Path.Combine(p, configfile);
			}

			if (DEBUG) {
				Console.WriteLine("   Loading " + Path.GetFileName(configfile) + "..");
			}

			LoadConfig(p, configfile, out title, out tocfile, out exts, out zipname, out resfoldername, out resources, out md_compiler, out outputFileNamesAsTitle);

			outfile = Path.Combine(p, title + ".html");
			zipfile = Path.Combine(p, zipname);
			respath = Path.Combine(p, resfoldername);

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
				foreach (string f in Directory.GetFiles(tmp, "*.*", SearchOption.AllDirectories)) {
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
				filtoc = new FileInfo(Path.Combine(tmp, Path.GetFileNameWithoutExtension(f.Name) + ".html.toc"));
				// Does a generated html file exist for the current file?
				if (fil.Exists && filtoc.Exists) {
					// Does the file html need to be updated?
					if (f.LastWriteTime != fil.LastWriteTime || f.LastWriteTime != filtoc.LastWriteTime) {
						// Regenerate html file
						Compile(f, filtoc, fil);
					} else {
						if (DEBUG2) {
							Console.WriteLine("      File is up to date: " + f.Name);
						}
					}
				} else {
					// Generate html file
					Compile(f, filtoc, fil);
				}
			}

			// Get all of the generated toc files
			htmlfiles = new List<string>();
			htmlfiles.AddRange(Directory.GetFiles(tmp, "*.html.toc", SearchOption.TopDirectoryOnly));

			// Generate the table of contents
			if (DEBUG) {
				Console.WriteLine("   Creating table of contents..");
			}

			// Sort the generated toc files (just like the .md files)
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

			res = new StringBuilder();
			foreach (string f in htmlfiles) {
				foreach (string l in File.ReadAllLines(f)) {
					if (l.Trim().Length == 0) {
						continue;
					}
					res.AppendLine(l);
				}
			}

			// Write the table of contents out..
			string tocmd = Path.Combine(tmp, "toc.md");
			using (StreamWriter w = File.CreateText(tocmd)) {
				w.Write(res.ToString());
				w.Close();
			}
			if (tocfile.Length > 0) {
				if (!Path.IsPathRooted(tocfile)) {
					tocfile = Path.Combine(Path.GetDirectoryName(tocmd), tocfile);
				}
			} else {
				tocfile = Path.Combine(Path.GetDirectoryName(tocmd), Path.GetFileNameWithoutExtension(tocmd)) + ".htm";
			}
			Compile(new FileInfo(tocmd), null, new FileInfo(tocfile));


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

			if (DEBUG) {
				Console.WriteLine("\r\n   Zipping file and images folder..");
				Console.WriteLine("----> " + Path.GetFileName(zipfile));
			}
			if (File.Exists(zipfile)) {
				File.SetAttributes(zipfile, FileAttributes.Normal);
				File.Delete(zipfile);
			}
			try {
				using (ZipFile zip = new ZipFile()) {
					zip.AddFile(outfile, string.Empty);
					if (Directory.Exists(respath)) {
						foreach (string ext in resources) {
							foreach (string f in Directory.GetFiles(respath, ext, SearchOption.TopDirectoryOnly)) {
								zip.AddFile(f, resfoldername);
							}
						}
					}
					zip.Save(zipfile);
				}
			} catch (Exception ex) {
				Console.Error.WriteLine("Exception occurred while creating zip file: " + ex);
			}


			Console.WriteLine("\r\n   Process is complete.");

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

		private static void LoadConfig( string p, string configfile, out string title, out string tocname, out List<string> exts, out string zipname, out string resfolder, out List<string> resources, out string markdown_compiler, out bool outputFileNamesAsTitle )
		{
			string[] ar;
			string arg;

			title = string.Empty;
			tocname = string.Empty;
			exts = new List<string>();
			zipname = string.Empty;
			resfolder = string.Empty;
			resources = new List<string>();
			markdown_compiler = string.Empty;
			outputFileNamesAsTitle = false;

			if (File.Exists(configfile)) {
				foreach (string l in File.ReadAllLines(configfile)) {
					if (l.StartsWith(";") || l.StartsWith("#") || l.Length == 0 || l.IndexOf("=") == -1) {
						continue;
					}

					ar = l.Split(new char[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries);
					arg = ar[0].Trim();

					if (arg.Equals("title", StringComparison.CurrentCultureIgnoreCase)) {
						title = ar[1].Trim();
					} else if (arg.Equals("toc", StringComparison.CurrentCultureIgnoreCase)) {
						tocname = ar[1].Trim();
					} else if (arg.Equals("ext", StringComparison.CurrentCultureIgnoreCase)) {
						exts.Add(ar[1].Trim());
					} else if (arg.Equals("zip", StringComparison.CurrentCultureIgnoreCase)) {
						zipname = ar[1].Trim();
					} else if (arg.Equals("resource_folder", StringComparison.CurrentCultureIgnoreCase)) {
						resfolder = ar[1].Trim();
					} else if (arg.Equals("resource", StringComparison.CurrentCultureIgnoreCase)) {
						resources.Add(ar[1].Trim());
					} else if (arg.StartsWith("markdown_compiler", StringComparison.CurrentCultureIgnoreCase)) {
						markdown_compiler = ar[1].Trim();
					} else if (arg.StartsWith("output_filenames", StringComparison.CurrentCultureIgnoreCase)) {
						outputFileNamesAsTitle = ar[1].Trim().StartsWith("t", StringComparison.CurrentCultureIgnoreCase);
					}
				}
			}

			if (title.Length == 0) {
				title = "ebook";
			}
			//if (tocname.Length == 0) {
			//	tocname = string.Empty;
			//}
			if (exts.Count == 0) {
				exts.Add("*.md");
			}
			if (zipname.Length == 0) {
				zipname = "ebook.zip";
			}
			if (resfolder.Length == 0) {
				resfolder = "resources";
			}
			if (resources.Count == 0) {
				resources.Add("*.png");
				resources.Add("*.jpg");
			}
			if (markdown_compiler.Length == 0) {
				markdown_compiler = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify) + @"\Root\Markdown.bat";
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

		static string[] padding = new string[] {
			"",
			"",
			"&nbsp;&nbsp;",
			"&nbsp;&nbsp;&nbsp;&nbsp;",
			"&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;",
			"&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;",
			"&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;",
			"&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;"
		};

		private static void Compile( FileInfo input, FileInfo filtoc, FileInfo output )
		{
			Process p;
			ProcessStartInfo info;

			if (filtoc != null) {
				PreProcess(input, filtoc);
			}

			info = new ProcessStartInfo();
			info.FileName = md_compiler;

			if (output != null) {
				info.Arguments = "\"" + input.FullName + "\" \"" + output.FullName + "\"";
			} else {
				info.Arguments = "\"" + input.FullName + "\"";
			}

			info.WorkingDirectory = input.Directory.FullName;
			info.WindowStyle = ProcessWindowStyle.Hidden;

			if (DEBUG) {
				Console.WriteLine("      Compiling: " + input.Name);
			}

			p = Process.Start(info);
			p.WaitForExit(10000);

			if (p.ExitCode != 0) {
				Console.WriteLine("**** Error compiling: " + input.FullName);
				Console.ReadKey(true);
			}

			if (output == null) {
				output = new FileInfo(Path.Combine(input.DirectoryName, Path.GetFileNameWithoutExtension(input.FullName)) + ".html");
			}

			PostProcess(input, output);

			output.CreationTime = input.CreationTime;
			output.LastAccessTime = input.LastAccessTime;
			output.LastWriteTime = input.LastWriteTime;
		}

		private static void PreProcess( FileInfo input, FileInfo filtoc )
		{
			List<string> lines;
			string name;
			string label;
			int index;

			if (filtoc.Exists) {
				filtoc.Delete();
			}

			lines = new List<string>(File.ReadAllLines(input.FullName));
			index = -1;

			// Copy the input file to the temporary folder (before editing it)..
			string srcname = input.FullName;
			FileInfo srcfile;
			string tmpfile = Path.Combine(tmp, input.Name);
			if (File.Exists(tmpfile)) {
				File.SetAttributes(tmpfile, FileAttributes.Normal);
				File.Delete(tmpfile);
			}
			input.MoveTo(tmpfile);
			input.CopyTo(srcname);

			srcfile = new FileInfo(srcname);

			srcfile.CreationTime = input.CreationTime;
			srcfile.LastAccessTime = input.LastAccessTime;
			srcfile.LastWriteTime = input.LastWriteTime;

			using (StreamWriter w = filtoc.CreateText()) {
				for (int i = 0; i < lines.Count; i++) {
					string l = lines[i];

					if (l.StartsWith("#;")) {
						// commented line.. skip it..
						lines[i] = string.Empty;
					} else if (l.StartsWith("#::include::")) {
						// Include another file.
						l = l.Substring("#::include::".Length).Trim();
						if (l.StartsWith("\"") && l.EndsWith("\"")) {
							l = l.Trim(' ', '\t', '"');
						}
						if (File.Exists(l)) {
							lines[i] = File.ReadAllText(l);
						} else {
							Console.WriteLine("Included file was not found: '" + l + "'");
						}
					} else if (l.StartsWith("#")) {
						// Create named anchors for all headings.
						if (l.StartsWith("######")) {
							index = 6;
						} else if (l.StartsWith("#####")) {
							index = 5;
						} else if (l.StartsWith("####")) {
							index = 4;
						} else if (l.StartsWith("###")) {
							index = 3;
						} else if (l.StartsWith("##")) {
							index = 2;
						} else if (l.StartsWith("#")) {
							index = 1;
						} else {
							continue;
						}

						//int k = l.IndexOf("<");
						//if (k > -1) {
						//	label = l.Substring(index, k - index).Trim();
						//} else {
						label = l.Substring(index).Trim();
						//}
						//if (label.Length == 0) {
						//	// The item already has an anchor in it!
						//	continue;
						//}
						name = label.Replace(" ", "_")
							.Replace("\"", "_").Replace("`", "_").Replace("'", "_")
							.Replace("/", "-").Replace("\\", "-")
							.Replace(",", ".");

						//## <a name="Table_of_Contents"></a> Table of Contents
						lines[i] = new string('#', index) + " <a name=\"" + name + "\"></a> " + label;

						if (index <= TOCLEVELS) {
							//&nbsp;&nbsp;&nbsp;&nbsp;[Table of Contents](#Table_of_Contents) <br/>
							w.WriteLine(padding[index] + "[" + label + "](#" + name + ")<br/>");
						}
					} else if (l.StartsWith(" #")) {
						lines[i] = l.TrimStart();
					}
				}
				w.Close();
			}

			//if (DEBUG) {
			//	lines.Insert(0, "<div title=\"" + input.Name + "\">");
			//	lines.Add("</div>");
			//}
			File.WriteAllLines(input.FullName, lines);

			filtoc.CreationTime = input.CreationTime;
			filtoc.LastAccessTime = input.LastAccessTime;
			filtoc.LastWriteTime = input.LastWriteTime;
		}

		private static void PostProcess( FileInfo input, FileInfo output )
		{
			if (DEBUG) {
				string s = File.ReadAllText(output.FullName);
				File.WriteAllText(output.FullName, "<div title=\"" + input.Name + "\">" + s + "</div>");
			}
		}
	}
}

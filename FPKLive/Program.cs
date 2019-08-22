using PIEBALD.Dialogs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

// 
// FPK is combination of git revision + modified files
// 
// When creating for the first time locally modified files need to be recorded as part of the FPK
// So FPK token is git revision + list of files modified when the FPK was made
// Files to keep lose are the list of files from the FPK token + all files listed in a dif between the git revision and HEAD
// 
//
// Some git stuff:
//      Diff different revisions "git --no-pager diff --name-only 777820db8d8490c113df5fc9db8809ff0a7610e6 66965b11a772c636e14a0340f0166010a9c6b6a0"
//      List recent revisions "git log --pretty=format:%H -n 10"
//      List locally modified files "git --no-pager diff --name-only HEAD"
//      Get HEAD revision: "git rev-parse HEAD"
//      Get untracked files: "git ls-files --others"
// 

namespace FPKLive
{
    class Program
    {
        class FPKToken
        {
            public string GitRevision;
            public string[] ModifiedFiles;

            public bool IsValid => GitRevision != null;
        }

        [STAThread]
        static int Main(string[] args)
        {
            try
            {
                var gitRootDir = Path.GetFullPath(Path.Combine(GetWorkingDir(), ".."));
                var assetsDir = Path.Combine(gitRootDir, "Assets");
                var toolsDir = Path.Combine(gitRootDir, "Tools");
                var unpackedArtDir = Path.Combine(gitRootDir, "UnpackedArt");
                var tempFPKDir = Path.Combine(gitRootDir, "FPKTemp");
                var fpkTokenFile = Path.Combine(assetsDir, "fpklive_token.txt");

                FPKToken token = ReadTokenFile(fpkTokenFile);
                if (token == null)
                {
                    // Instantiate it (this example uses an anonymous method)
                    ProgressDialog dlg = null;
                    dlg = new ProgressDialog(
                        "Creating FPKs for the first time, this may take a while...",
                        ProgressBarStyle.Continuous, false,
                        delegate (object[] Params)
                        {
                            // Calculate the FPK token
                            dlg.RaiseUpdateProgress(10, "FPK Live: Gathering files...");

                            token = CreateToken(gitRootDir);
                            if (!token.IsValid)
                            {
                                Log($"FPK token couldn't be calculated, are you sure you are in a git repository?");
                            }

                            RecreateDirectory(tempFPKDir);

                            dlg.RaiseUpdateProgress(50, "FPK Live: Building FPKs...");

                            ExecuteCommand("PakBuild", $"/I=\"{unpackedArtDir}\" /O=\"{tempFPKDir}\" /F /S=256 /R=C2C", toolsDir);

                            dlg.RaiseUpdateProgress(70, "FPK Live: Deleting old FPKs...");

                            // Delete old FPKs
                            foreach (var fpk in Directory.GetFiles(assetsDir, "*.fpk"))
                            {
                                File.Delete(fpk);
                            }

                            dlg.RaiseUpdateProgress(80, "FPK Live: Moving new FPKs into place...");

                            // Move new FPKs to assets dir
                            foreach (var fpk in Directory.GetFiles(tempFPKDir, "*.fpk"))
                            {
                                File.Move(fpk, Path.Combine(assetsDir, Path.GetFileName(fpk)));
                            }

                            dlg.RaiseUpdateProgress(90, "FPK Live: Cleaning up...");

                            // Cleanup
                            Directory.Delete(tempFPKDir, recursive: true);

                            // Save token (could do this as json or something but this is fine for now)
                            File.WriteAllLines(fpkTokenFile, new[] { token.GitRevision }.Concat(token.ModifiedFiles));

                            dlg.RaiseUpdateProgress(100, "FPK Live: Done!");

                            return null;
                        }
                    );

                    // Then all you need to do is 
                    dlg.ShowDialog();
                }

                // Delete old Patch FPKs
                foreach (var fpk in Directory.GetFiles(assetsDir, "C2CPatch*.fpk"))
                {
                    File.Delete(fpk);
                }

                // var currGitRev = ExecuteGitCommand("rev-parse HEAD", unpackedArtDir).FirstOrDefault();
                // Now we find the files that changed between the token and current working directory and copy those to the art directory, along with all the biks!
                var modifiedFiles = ResolveGitPathList(
                    GetChangedArtFilesRev(gitRootDir, $"{token.GitRevision} HEAD")
                    .Concat(token.ModifiedFiles).Select(p => $"UnpackedArt/{p}")
                    .Distinct(),
                    gitRootDir).ToList();

                if (modifiedFiles.Count > 0)
                {
                    // Instantiate it (this example uses an anonymous method)
                    ProgressDialog dlg = null;
                    dlg = new ProgressDialog(
                        "Patching FPKs...",
                        ProgressBarStyle.Continuous, false,
                        delegate (object[] Params)
                        {
                            // Make sure the temp dir is clean and exists
                            RecreateDirectory(tempFPKDir);
                            dlg.RaiseUpdateProgress(10, "FPK Live: Gathering files...");

                            // Copy all modified files into temporary patch art folder
                            foreach (var file in modifiedFiles.Where(f => File.Exists(f)))
                            {
                                CopyFileRobust(file, Path.Combine(tempFPKDir, GetRelativePath(file, unpackedArtDir)));
                            }

                            // Build patch FPK file
                            dlg.RaiseUpdateProgress(50, "FPK Live: Building Patch FPK...");
                            ExecuteCommand("PakBuild", $"/I=\"{tempFPKDir}\" /O=\"{tempFPKDir}\" /F /S=256 /R=C2CPatch", toolsDir);

                            dlg.RaiseUpdateProgress(80, "FPK Live: Cleaning up ...");
                            // Move new FPK patch files to assets dir
                            foreach (var fpk in Directory.GetFiles(tempFPKDir, "*.fpk"))
                            {
                                File.Move(fpk, Path.Combine(assetsDir, Path.GetFileName(fpk)));
                            }

                            // Cleanup
                            Directory.Delete(tempFPKDir, recursive: true);

                            dlg.RaiseUpdateProgress(100, "FPK Live: Done!");

                            return null;
                        }
                    );

                    // Then all you need to do is 
                    dlg.ShowDialog();
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show($"An exception occurred:\n{ex.Message}\n\nPlease report this with a screenshot of this dialog on the forums (@billw2015) or on discord (@billw)!\n\nFull exception details:\n{ex}", "FPK Live: Exception occurred!");
            }
            return 0;
        }

        private static void CopyFileRobust(string file, string targetFile)
        {
            var targetDir = Path.GetDirectoryName(targetFile);
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }
            File.Copy(file, targetFile);
        }

        static FPKToken ReadTokenFile(string fpkTokenFile)
        {
            if (File.Exists(fpkTokenFile))
            {
                var lines = File.ReadAllLines(fpkTokenFile);
                if (lines.Length == 0)
                {
                    Log($"FPK Token file at {fpkTokenFile} is invalid, recreating FPKs");
                }
                else
                {
                    return new FPKToken
                    {
                        GitRevision = lines[0],
                        ModifiedFiles = lines.Skip(1).ToArray()
                    };
                }
            }
            return null;
        }

        static FPKToken CreateToken(string rootDir) => new FPKToken { GitRevision = ExecuteGitCommand("rev-parse HEAD", rootDir).FirstOrDefault(), ModifiedFiles = GetChangedArtFiles(rootDir) };

        private static string[] GetChangedArtFiles(string rootDir) => GetChangedArtFilesRev(rootDir, "HEAD");

        private static string[] GetChangedArtFilesRev(string rootDir, string rev)
        {
            var changedFiles = ExecuteGitCommand($"diff --name-only {rev} -- UnpackedArt/", rootDir);
            var newFiles = ExecuteGitCommand("ls-files --others -- UnpackedArt/", rootDir);
            return changedFiles.Concat(newFiles).Select(p => p.Replace("UnpackedArt/", "")).ToArray();
        }

        static string[] ExecuteGitCommand(string command, string workingDirectory) => ExecuteCommand("git", "--no-pager " + command, workingDirectory);

        static string[] ExecuteCommand(string exe, string args, string workingDirectory)
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                WorkingDirectory = workingDirectory,
                Arguments = args,
                RedirectStandardOutput = true,
                UseShellExecute = false
            });
            var results = process.StandardOutput.ReadToEnd().Split(new []{'\n'}, StringSplitOptions.RemoveEmptyEntries);
            process.WaitForExit();
            return results;
        }

        static string GetRelativePath(string filespec, string folder)
        {
            Uri pathUri = new Uri(filespec);
            // Folders must end in a slash
            if (!folder.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                folder += Path.DirectorySeparatorChar;
            }
            Uri folderUri = new Uri(folder);
            return Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString().Replace('/', Path.DirectorySeparatorChar));
        }

        static string CanonicalPath(string path) => path.ToLower().Replace('/', '\\');
        static IEnumerable<string> ResolveGitPathList(IEnumerable<string> list, string gitRootDir) => list.Select(p => Path.Combine(gitRootDir, p));
        static IEnumerable<string> FilterPathList(IEnumerable<string> list, string directory) => list.Where(p => CanonicalPath(p).StartsWith(CanonicalPath(directory)));

        static void Log(string msg)
        {
            Console.WriteLine(msg);
        }

        static void RecreateDirectory(string dir)
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
            Directory.CreateDirectory(dir);
        }

        static string GetWorkingDir()
        {
            var location = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            if (Path.GetFileName(location).ToLower() != "tools")
            {
                if (Path.GetFileName(Environment.CurrentDirectory).ToLower() != "tools")
                {
                    throw new Exception("Expected to be directly inside the tools directory, or run with that as the working directory");
                }
                return Environment.CurrentDirectory;
            }
            return location;
        }

    }
}

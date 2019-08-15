using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        static void Log(string msg)
        {
            Console.WriteLine(msg);
        }

        static int Main(string[] args)
        {
            //if(args.Length != 1)
            //{
            //    Console.WriteLine("Usage: FPKLive <Mods\\Caveman2Cosmos directory>");
            //    return 1;
            //}

            //var modDir = args[0];
            var gitRootDir = Path.GetFullPath(Path.Combine(GetWorkingDir(), ".."));
            var assetsDir = Path.Combine(gitRootDir, "Assets");
            var toolsDir = Path.Combine(gitRootDir, "Tools");
            var unpackedArtDir = Path.Combine(gitRootDir, "UnpackedArt");
            var fpkTokenFile = Path.Combine(assetsDir, "fpklive_token.txt");

            FPKToken token = ReadTokenFile(fpkTokenFile);
            if(token == null)
            {
                // Calculate the FPK token
                token = CreateToken(unpackedArtDir);
                if(!token.IsValid)
                {
                    Log($"FPK token couldn't be calculated, are you sure you are in a git repository?");
                }

                // Create the FPKs themselves in temp dir
                var tempFPKDir = Path.Combine(gitRootDir, "FPKTemp");
                Directory.CreateDirectory(tempFPKDir);
                ExecuteCommand("PakBuild", $"/I=\"{unpackedArtDir}\" /O=\"{tempFPKDir}\" /F /S=100 /R=C2C /X=bik", toolsDir);

                // Delete old FPKs
                foreach (var fpk in Directory.GetFiles(assetsDir, "*.fpk"))
                {
                    File.Delete(fpk);
                }

                // Move new FPKs to assets dir
                foreach (var fpk in Directory.GetFiles(tempFPKDir, "*.fpk"))
                {
                    File.Move(fpk, Path.Combine(assetsDir, Path.GetFileName(fpk)));
                }

                // Cleanup
                Directory.Delete(tempFPKDir);

                // Save token (could do this as json or something but this is fine for now)
                File.WriteAllLines(fpkTokenFile, new[] { token.GitRevision }.Concat(token.ModifiedFiles));
            }

            // var currGitRev = ExecuteGitCommand("rev-parse HEAD", unpackedArtDir).FirstOrDefault();

            // Now we find the files that changed between the token and current working directory and copy those to the art directory, along with all the biks!
            var modifiedFiles = ResolveGitPathList(
                FilterPathList(ExecuteGitCommand($"diff --name-only {token.GitRevision} HEAD", unpackedArtDir), "UnpackedArt")
                .Concat(token.ModifiedFiles), gitRootDir);

            // Delete existing art patch folder
            var artPatchFolder = Path.Combine(assetsDir, "art");
            if (Directory.Exists(artPatchFolder))
            {
                Directory.Delete(artPatchFolder, recursive: true);
            }

            // Copy all modified files and bik files into the Mod art folder
            foreach(var file in modifiedFiles.Concat(Directory.GetFiles(unpackedArtDir, "*.bik", SearchOption.AllDirectories)))
            {
                File.Copy(file, Path.Combine(assetsDir, GetRelativePath(file, unpackedArtDir)));
            }
            return 0;
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

        static FPKToken CreateToken(string rootDir)
        {
            return new FPKToken {
                GitRevision = ExecuteGitCommand("rev-parse HEAD", rootDir).FirstOrDefault(),
                ModifiedFiles = FilterPathList(ExecuteGitCommand("diff --name-only HEAD", rootDir), "UnpackedArt").ToArray()
            };
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
            process.WaitForExit();
            return process.StandardOutput.ReadToEnd().Split(new []{'\n'}, StringSplitOptions.RemoveEmptyEntries);
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

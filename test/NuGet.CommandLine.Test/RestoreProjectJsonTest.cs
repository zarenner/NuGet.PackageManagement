﻿using System;
using System.Collections.Concurrent;
using System.IO;
using NuGet.ProjectModel;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class RestoreProjectJsonTest : IDisposable
    {
        [Fact]
        public void RestoreProjectJson_SolutionFileWithAllProjectsInOneFolder()
        {
            // Arrange
            var tempPath = Path.GetTempPath();
            var guid = Guid.NewGuid();
            var workingPath = Path.Combine(tempPath, guid.ToString());
            var repositoryPath = Path.Combine(workingPath, Guid.NewGuid().ToString());
            var currentDirectory = Directory.GetCurrentDirectory();
            var projectDir = Path.Combine(workingPath, "abc");
            var nugetexe = Util.GetNuGetExePath();

            _dirs.TryAdd(workingPath, false);

            Util.CreateDirectory(workingPath);
            Util.CreateDirectory(repositoryPath);
            Util.CreateDirectory(projectDir);
            Util.CreateDirectory(Path.Combine(workingPath, ".nuget"));
            var packageA = Util.CreateTestPackageBuilder("packageA", "1.1.0-beta-01");
            var targetContent = "<?xml version=\"1.0\" encoding=\"utf-8\"?><Project ToolsVersion=\"12.0\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\"></Project>";

            var targetA = Util.CreatePackageFile("build/uap/packageA.targets", targetContent);
            var libA = Util.CreatePackageFile("lib/uap/a.dll", "a");

            packageA.Files.Add(targetA);
            packageA.Files.Add(libA);

            Util.CreateConfigForGlobalPackagesFolder(workingPath);
            Util.CreateTestPackage(packageA, repositoryPath);

            Util.CreateFile(projectDir, "testA.project.json",
                                            @"{
                                            'dependencies': {
                                            'packageA': '1.1.0-beta-*'
                                            },
                                            'frameworks': {
                                                        'uap10.0': { }
                                                    }
                                            }");

            Util.CreateFile(projectDir, "testB.project.json",
                                            @"{
                                            'dependencies': {
                                            'packageA': '1.1.0-beta-*'
                                            },
                                            'frameworks': {
                                                        'uap10.0': { }
                                                    }
                                            }");

            Util.CreateFile(projectDir, "packages.testC.config",
                                            @"<packages>
                                <package id=""packageA"" version=""1.1.0-beta-01"" targetFramework=""net45"" />
                            </packages>");

            Util.CreateFile(projectDir, "testA.csproj", CSProjXML); // testA.project.json
            Util.CreateFile(projectDir, "testB.csproj", CSProjXML); // testB.project.json
            Util.CreateFile(projectDir, "testC.csproj", CSProjXML); // packages.testC.config
            Util.CreateFile(projectDir, "testD.csproj", CSProjXML); // Non-nuget

            var slnPath = Path.Combine(workingPath, "xyz.sln");

            Util.CreateFile(workingPath, "xyz.sln",
                       @"
                        Microsoft Visual Studio Solution File, Format Version 12.00
                        # Visual Studio 14
                        VisualStudioVersion = 14.0.23107.0
                        MinimumVisualStudioVersion = 10.0.40219.1
                        Project(""{AAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""testA"", ""abc\testA.csproj"", ""{6A6279C1-B5EE-4C6B-9FA3-A794CE195136}""
                        EndProject
                        Project(""{ABE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""testB"", ""abc\testB.csproj"", ""{6A6279C1-B5EE-4C6B-9FA3-A794CE195136}""
                        EndProject
                        Project(""{ACE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""testC"", ""abc\testC.csproj"", ""{6A6279C1-B5EE-4C6B-9FA3-A794CE195136}""
                        EndProject
                        Project(""{ADE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""testD"", ""abc\testD.csproj"", ""{6A6279C1-B5EE-4C6B-9FA3-A794CE195136}""
                        EndProject
                        Global
                            GlobalSection(SolutionConfigurationPlatforms) = preSolution
                                Debug|Any CPU = Debug|Any CPU
                                Release|Any CPU = Release|Any CPU
                            EndGlobalSection
                            GlobalSection(ProjectConfigurationPlatforms) = postSolution
                                {6A6279C1-B5EE-4C6B-9FA3-A794CE195136}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                                {6A6279C1-B5EE-4C6B-9FA3-A794CE195136}.Debug|Any CPU.Build.0 = Debug|Any CPU
                                {6A6279C1-B5EE-4C6B-9FA3-A794CE195136}.Release|Any CPU.ActiveCfg = Release|Any CPU
                                {6A6279C1-B5EE-4C6B-9FA3-A794CE195136}.Release|Any CPU.Build.0 = Release|Any CPU
                            EndGlobalSection
                            GlobalSection(SolutionProperties) = preSolution
                                HideSolutionNode = FALSE
                            EndGlobalSection
                        EndGlobal
                        ");

            string[] args = new string[] {
                "restore",
                "-Source",
                repositoryPath,
                "-solutionDir",
                workingPath,
                slnPath
            };

            var targetFileA = Path.Combine(projectDir, "testA.nuget.targets");
            var targetFileB = Path.Combine(projectDir, "testB.nuget.targets");
            var lockFileA = Path.Combine(projectDir, "testA.project.lock.json");
            var lockFileB = Path.Combine(projectDir, "testB.project.lock.json");

            // Act
            var r = CommandRunner.Run(
                nugetexe,
                workingPath,
                string.Join(" ", args),
                waitForExit: true);

            // Assert
            Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
            Assert.True(File.Exists(targetFileA));
            Assert.True(File.Exists(targetFileB));
            Assert.True(File.Exists(lockFileA));
            Assert.True(File.Exists(lockFileB));
            Assert.True(File.Exists(Path.Combine(workingPath, "packages/packageA.1.1.0-beta-01/packageA.1.1.0-beta-01.nupkg")));
        }

        [Fact]
        public void RestoreProjectJson_GenerateFilesWithProjectNameFromCSProj()
        {
            // Arrange
            var tempPath = Path.GetTempPath();
            var guid = Guid.NewGuid();
            var workingPath = Path.Combine(tempPath, guid.ToString());
            var repositoryPath = Path.Combine(workingPath, Guid.NewGuid().ToString());
            var currentDirectory = Directory.GetCurrentDirectory();
            var nugetexe = Util.GetNuGetExePath();

            _dirs.TryAdd(workingPath, false);

            Util.CreateDirectory(workingPath);
            Util.CreateDirectory(repositoryPath);
            Util.CreateDirectory(Path.Combine(workingPath, ".nuget"));
            var packageA = Util.CreateTestPackageBuilder("packageA", "1.1.0-beta-01");
            var targetContent = "<?xml version=\"1.0\" encoding=\"utf-8\"?><Project ToolsVersion=\"12.0\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\"></Project>";
            var targetA = Util.CreatePackageFile("build/uap/packageA.targets", targetContent);
            var libA = Util.CreatePackageFile("lib/uap/a.dll", "a");
            packageA.Files.Add(targetA);
            packageA.Files.Add(libA);

            Util.CreateConfigForGlobalPackagesFolder(workingPath);
            Util.CreateTestPackage(packageA, repositoryPath);

            Util.CreateFile(workingPath, "test.project.json",
                                            @"{
                                            'dependencies': {
                                            'packageA': '1.1.0-beta-*'
                                            },
                                            'frameworks': {
                                                        'uap10.0': { }
                                                    }
                                            }");

            Util.CreateFile(workingPath, "test.csproj",
                                            @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <Project ToolsVersion=""14.0"" DefaultTargets=""Build""
                        xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
        <Target Name=""NuGet_GetProjectsReferencingProjectJson""></Target>
        </Project>");

            var csprojPath = Path.Combine(workingPath, "test.csproj");

            string[] args = new string[] {
                "restore",
                "-Source",
                repositoryPath,
                "-solutionDir",
                workingPath,
                csprojPath
            };

            var targetFilePath = Path.Combine(workingPath, $"test.nuget.targets");
            var lockFilePath = Path.Combine(workingPath, $"test.project.lock.json");

            // Act
            var r = CommandRunner.Run(
                nugetexe,
                workingPath,
                string.Join(" ", args),
                waitForExit: true);

            // Assert
            Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
            Assert.True(File.Exists(lockFilePath));
            Assert.True(File.Exists(targetFilePath));
        }

        [Fact]
        public void RestoreProjectJson_GenerateTargetsFileFromSln()
        {
            // Arrange
            var tempPath = Path.GetTempPath();
            var guid = Guid.NewGuid();
            var workingPath = Path.Combine(tempPath, guid.ToString());
            var repositoryPath = Path.Combine(workingPath, Guid.NewGuid().ToString());
            var currentDirectory = Directory.GetCurrentDirectory();
            var projectDir = Path.Combine(workingPath, "abc");
            var nugetexe = Util.GetNuGetExePath();

            _dirs.TryAdd(workingPath, false);

            Util.CreateDirectory(workingPath);
            Util.CreateDirectory(repositoryPath);
            Util.CreateDirectory(projectDir);
            Util.CreateDirectory(Path.Combine(workingPath, ".nuget"));
            Util.CreateConfigForGlobalPackagesFolder(workingPath);
            var packageA = Util.CreateTestPackageBuilder("packageA", "1.1.0-beta-01");
            var targetContent = "<?xml version=\"1.0\" encoding=\"utf-8\"?><Project ToolsVersion=\"12.0\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\"></Project>";

            var targetA = Util.CreatePackageFile("build/uap/packageA.targets", targetContent);
            var libA = Util.CreatePackageFile("lib/uap/a.dll", "a");

            packageA.Files.Add(targetA);
            packageA.Files.Add(libA);

            Util.CreateTestPackage(packageA, repositoryPath);

            Util.CreateFile(projectDir, "project.json",
                                            @"{
                                            'dependencies': {
                                            'packageA': '1.1.0-beta-*'
                                            },
                                            'frameworks': {
                                                        'uap10.0': { }
                                                    }
                                            }");

            Util.CreateFile(projectDir, "test.csproj",
                                            @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <Project ToolsVersion=""14.0"" DefaultTargets=""Build""
                        xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
        <Target Name=""NuGet_GetProjectsReferencingProjectJson""></Target>
        </Project>");

            var slnPath = Path.Combine(workingPath, "xyz.sln");

            Util.CreateFile(workingPath, "xyz.sln",
                       @"
                        Microsoft Visual Studio Solution File, Format Version 12.00
                        # Visual Studio 14
                        VisualStudioVersion = 14.0.23107.0
                        MinimumVisualStudioVersion = 10.0.40219.1
                        Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""test"", ""abc\test.csproj"", ""{6A6279C1-B5EE-4C6B-9FA3-A794CE195136}""
                        EndProject
                        Global
                            GlobalSection(SolutionConfigurationPlatforms) = preSolution
                                Debug|Any CPU = Debug|Any CPU
                                Release|Any CPU = Release|Any CPU
                            EndGlobalSection
                            GlobalSection(ProjectConfigurationPlatforms) = postSolution
                                {6A6279C1-B5EE-4C6B-9FA3-A794CE195136}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                                {6A6279C1-B5EE-4C6B-9FA3-A794CE195136}.Debug|Any CPU.Build.0 = Debug|Any CPU
                                {6A6279C1-B5EE-4C6B-9FA3-A794CE195136}.Release|Any CPU.ActiveCfg = Release|Any CPU
                                {6A6279C1-B5EE-4C6B-9FA3-A794CE195136}.Release|Any CPU.Build.0 = Release|Any CPU
                            EndGlobalSection
                            GlobalSection(SolutionProperties) = preSolution
                                HideSolutionNode = FALSE
                            EndGlobalSection
                        EndGlobal
                        ");

            var csprojPath = Path.Combine(projectDir, "test.csproj");

            string[] args = new string[] {
                "restore",
                "-Source",
                repositoryPath,
                "-solutionDir",
                workingPath,
                slnPath
            };

            var targetFilePath = Path.Combine(projectDir, $"test.nuget.targets");

            // Act
            var r = CommandRunner.Run(
                nugetexe,
                workingPath,
                string.Join(" ", args),
                waitForExit: true);

            // Assert
            Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
            Assert.True(File.Exists(targetFilePath));

            var targetsFile = File.OpenText(targetFilePath).ReadToEnd();
            Assert.True(targetsFile.IndexOf(@"build\uap\packageA.targets") > -1);
        }


        [Fact]
        public void RestoreProjectJson_GenerateTargetsFileFromCSProj()
        {
            // Arrange
            var tempPath = Path.GetTempPath();
            var guid = Guid.NewGuid();
            var workingPath = Path.Combine(tempPath, guid.ToString());
            var repositoryPath = Path.Combine(workingPath, Guid.NewGuid().ToString());
            var currentDirectory = Directory.GetCurrentDirectory();
            var nugetexe = Util.GetNuGetExePath();

            _dirs.TryAdd(workingPath, false);

            Util.CreateDirectory(workingPath);
            Util.CreateDirectory(repositoryPath);
            Util.CreateConfigForGlobalPackagesFolder(workingPath);
            Util.CreateDirectory(Path.Combine(workingPath, ".nuget"));
            var packageA = Util.CreateTestPackageBuilder("packageA", "1.1.0-beta-01");
            var packageB = Util.CreateTestPackageBuilder("packageB", "2.2.0-beta-02");

            var targetContent = "<?xml version=\"1.0\" encoding=\"utf-8\"?><Project ToolsVersion=\"12.0\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\"></Project>";

            var targetA = Util.CreatePackageFile("build/uap/packageA.targets", targetContent);
            var libA = Util.CreatePackageFile("lib/uap/a.dll", "a");

            packageA.Files.Add(targetA);
            packageA.Files.Add(libA);

            var targetB = Util.CreatePackageFile("build/uap/packageB.targets", targetContent);
            var libB = Util.CreatePackageFile("lib/uap/b.dll", "b");

            packageB.Files.Add(targetB);
            packageB.Files.Add(libB);

            Util.CreateTestPackage(packageA, repositoryPath);
            Util.CreateTestPackage(packageB, repositoryPath);

            Util.CreateFile(workingPath, "project.json",
                                            @"{
                                            'dependencies': {
                                            'packageA': '1.1.0-beta-*',
                                            'packageB': '2.2.0-beta-*'
                                            },
                                            'frameworks': {
                                                        'uap10.0': { }
                                                    }
                                            }");

            Util.CreateFile(workingPath, "test.csproj",
                                            @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <Project ToolsVersion=""14.0"" DefaultTargets=""Build""
                        xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
        <Target Name=""NuGet_GetProjectsReferencingProjectJson""></Target>
        </Project>");

            var csprojPath = Path.Combine(workingPath, "test.csproj");

            string[] args = new string[] {
                "restore",
                "-Source",
                repositoryPath,
                "-solutionDir",
                workingPath,
                csprojPath
            };

            var targetFilePath = Path.Combine(workingPath, $"test.nuget.targets");

            // Act
            var r = CommandRunner.Run(
                nugetexe,
                workingPath,
                string.Join(" ", args),
                waitForExit: true);

            // Assert
            Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
            Assert.True(File.Exists(targetFilePath));

            var targetsFile = File.OpenText(targetFilePath).ReadToEnd();
            Assert.True(targetsFile.IndexOf(@"build\uap\packageA.targets") > -1);
            Assert.True(targetsFile.IndexOf(@"build\uap\packageB.targets") > -1);
        }

        [Fact]
        public void RestoreProjectJson_GenerateTargetsFileWithFolder()
        {
            // Arrange
            var tempPath = Path.GetTempPath();
            var guid = Guid.NewGuid();
            var workingPath = Path.Combine(tempPath, guid.ToString());
            var repositoryPath = Path.Combine(workingPath, Guid.NewGuid().ToString());
            var currentDirectory = Directory.GetCurrentDirectory();
            var nugetexe = Util.GetNuGetExePath();

            _dirs.TryAdd(workingPath, false);

            Util.CreateDirectory(workingPath);
            Util.CreateDirectory(repositoryPath);
            Util.CreateDirectory(Path.Combine(workingPath, ".nuget"));
            Util.CreateConfigForGlobalPackagesFolder(workingPath);
            var packageA = Util.CreateTestPackageBuilder("packageA", "1.1.0-beta-01");
            var packageB = Util.CreateTestPackageBuilder("packageB", "2.2.0-beta-02");

            var targetContent = "<?xml version=\"1.0\" encoding=\"utf-8\"?><Project ToolsVersion=\"12.0\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\"></Project>";

            var targetA = Util.CreatePackageFile("build/uap/packageA.targets", targetContent);
            var libA = Util.CreatePackageFile("lib/uap/a.dll", "a");

            packageA.Files.Add(targetA);
            packageA.Files.Add(libA);

            var targetB = Util.CreatePackageFile("build/uap/packageB.targets", targetContent);
            var libB = Util.CreatePackageFile("lib/uap/b.dll", "b");

            packageB.Files.Add(targetB);
            packageB.Files.Add(libB);

            Util.CreateTestPackage(packageA, repositoryPath);
            Util.CreateTestPackage(packageB, repositoryPath);

            Util.CreateFile(workingPath, "project.json",
                                            @"{
                                            'dependencies': {
                                            'packageA': '1.1.0-beta-*',
                                            'packageB': '2.2.0-beta-*'
                                            },
                                            'frameworks': {
                                                        'uap10.0': { }
                                                    }
                                            }");

            string[] args = new string[] {
                "restore",
                "-Source",
                repositoryPath,
                "-solutionDir",
                workingPath,
                "project.json"
            };

            var targetFilePath = Path.Combine(workingPath, $"{guid}.nuget.targets");

            // Act
            var r = CommandRunner.Run(
                nugetexe,
                workingPath,
                string.Join(" ", args),
                waitForExit: true);

            // Assert
            Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
            Assert.True(File.Exists(targetFilePath));

            var targetsFile = File.OpenText(targetFilePath).ReadToEnd();
            Assert.True(targetsFile.IndexOf(@"build\uap\packageA.targets") > -1);
            Assert.True(targetsFile.IndexOf(@"build\uap\packageB.targets") > -1);
        }

        [Fact]
        public void RestoreProjectJson_IsLockedTrueAfterRestore()
        {
            // Arrange
            var tempPath = Path.GetTempPath();
            var workingPath = Path.Combine(tempPath, Guid.NewGuid().ToString());
            var repositoryPath = Path.Combine(workingPath, Guid.NewGuid().ToString());
            var currentDirectory = Directory.GetCurrentDirectory();
            var nugetexe = Util.GetNuGetExePath();

            _dirs.TryAdd(workingPath, false);

            Util.CreateDirectory(workingPath);
            Util.CreateDirectory(repositoryPath);
            Util.CreateDirectory(Path.Combine(workingPath, ".nuget"));
            Util.CreateTestPackage("packageA", "1.1.0", repositoryPath);
            Util.CreateTestPackage("packageB", "2.2.0", repositoryPath);
            Util.CreateConfigForGlobalPackagesFolder(workingPath);
            Util.CreateFile(workingPath, "project.json",
                                            @"{
                                            'dependencies': {
                                            'packageA': '1.1.0',
                                            'packageB': '2.2.0'
                                            },
                                            'frameworks': {
                                                        'uap10.0': { }
                                                    }
                                            }");

            string[] args = new string[] {
                "restore",
                "-Source",
                repositoryPath,
                "-solutionDir",
                workingPath,
                "project.json"
            };

            // Restore once to get a lock file
            var r = CommandRunner.Run(
                nugetexe,
                workingPath,
                string.Join(" ", args),
                waitForExit: true);

            // Set IsLocked=true
            var lockFilePath = Path.Combine(workingPath, "project.lock.json");
            var lockFileFormat = new LockFileFormat();
            var lockFile = lockFileFormat.Read(lockFilePath);
            lockFile.IsLocked = true;
            lockFileFormat.Write(lockFilePath, lockFile);

            // Act
            // Restore using the locked lock file
            r = CommandRunner.Run(
                nugetexe,
                workingPath,
                string.Join(" ", args),
                waitForExit: true);

            var lockFileAfter = lockFileFormat.Read(lockFilePath);

            // Assert
            Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
            Assert.True(lockFileAfter.IsLocked);
            Assert.True(lockFile.Equals(lockFileAfter));
        }

        [Fact]
        public void RestoreProjectJson_CorruptedLockFile()
        {
            // Arrange
            var tempPath = Path.GetTempPath();
            var workingPath = Path.Combine(tempPath, Guid.NewGuid().ToString());
            var repositoryPath = Path.Combine(workingPath, Guid.NewGuid().ToString());
            var currentDirectory = Directory.GetCurrentDirectory();
            var nugetexe = Util.GetNuGetExePath();

            _dirs.TryAdd(workingPath, false);

            Util.CreateDirectory(workingPath);
            Util.CreateDirectory(repositoryPath);
            Util.CreateDirectory(Path.Combine(workingPath, ".nuget"));
            Util.CreateConfigForGlobalPackagesFolder(workingPath);
            Util.CreateTestPackage("packageA", "1.1.0", repositoryPath);
            Util.CreateTestPackage("packageB", "2.2.0", repositoryPath);
            Util.CreateFile(workingPath, "project.json",
                                            @"{
                                            'dependencies': {
                                            'packageA': '1.1.0',
                                            'packageB': '2.2.0'
                                            },
                                            'frameworks': {
                                                        'uap10.0': { }
                                                    }
                                            }");

            string[] args = new string[] {
                "restore",
                "-Source",
                repositoryPath,
                "-solutionDir",
                workingPath,
                "project.json"
            };

            var lockFilePath = Path.Combine(workingPath, "project.lock.json");
            var lockFileFormat = new LockFileFormat();
            using (var writer = new StreamWriter(lockFilePath))
            {
                writer.WriteLine("{ \"CORRUPTED!\": \"yep\"");
            }

            // Act
            var r = CommandRunner.Run(
                nugetexe,
                workingPath,
                string.Join(" ", args),
                waitForExit: true);

            var lockFile = lockFileFormat.Read(lockFilePath);

            // Assert
            // If the library count can be obtained then a new lock file was created
            Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
            Assert.Equal(2, lockFile.Libraries.Count);
        }

        private const string CSProjXML = @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <Project ToolsVersion=""14.0"" DefaultTargets=""Build""
                        xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
        <Target Name=""NuGet_GetProjectsReferencingProjectJson""></Target>
        </Project>";

        /// <summary>
        /// Store all directories used by the unit tests and clean them up at the end during Dispose()
        /// </summary>
        private ConcurrentDictionary<string, bool> _dirs = new ConcurrentDictionary<string, bool>();

        public void Dispose()
        {
            foreach (var dir in _dirs.Keys)
            {
                try
                {
                    Util.DeleteDirectory(dir);
                }
                catch
                {

                }
            }
        }
    }
}

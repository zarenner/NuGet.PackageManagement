﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using Moq;

namespace NuGet.CommandLine.Test
{
    public static class Util
    {
        /// <summary>
        /// Creates a test package.
        /// </summary>
        /// <param name="packageId">The id of the created package.</param>
        /// <param name="version">The version of the created package.</param>
        /// <param name="path">The directory where the package is created.</param>
        /// <returns>The full path of the created package file.</returns>
        public static string CreateTestPackage(string packageId, string version, string path, Uri licenseUrl = null)
        {
            var packageBuilder = new PackageBuilder
            {
                Id = packageId,
                Version = new SemanticVersion(version)
            };
            packageBuilder.Description = string.Format(
                CultureInfo.InvariantCulture,
                "desc of {0} {1}",
                packageId, version);

            if (licenseUrl != null)
            {
                packageBuilder.LicenseUrl = licenseUrl;
            }

            packageBuilder.Files.Add(CreatePackageFile(@"content\test1.txt"));
            packageBuilder.Authors.Add("test author");

            var packageFileName = string.Format("{0}.{1}.nupkg", packageId, version);
            var packageFileFullPath = Path.Combine(path, packageFileName);
            using (var fileStream = File.Create(packageFileFullPath))
            {
                packageBuilder.Save(fileStream);
            }

            return packageFileFullPath;
        }

        /// <summary>
        /// Creates a basic package builder for unit tests.
        /// </summary>
        public static PackageBuilder CreateTestPackageBuilder(string packageId, string version)
        {
            var packageBuilder = new PackageBuilder
            {
                Id = packageId,
                Version = new SemanticVersion(version)
            };
            packageBuilder.Description = string.Format(
                CultureInfo.InvariantCulture,
                "desc of {0} {1}",
                packageId, version);

            packageBuilder.Authors.Add("test author");

            return packageBuilder;
        }

        public static string CreateTestPackage(PackageBuilder packageBuilder, string directory)
        {
            var packageFileName = string.Format("{0}.{1}.nupkg", packageBuilder.Id, packageBuilder.Version);
            var packageFileFullPath = Path.Combine(directory, packageFileName);
            using (var fileStream = File.Create(packageFileFullPath))
            {
                packageBuilder.Save(fileStream);
            }

            return packageFileFullPath;
        }

        /// <summary>
        /// Creates the specified directory. If it exists, it's first deleted before
        /// it's created. Thus, the directory is guaranteed to be empty.
        /// </summary>
        /// <param name="directory">The directory to be created.</param>
        public static void CreateDirectory(string directory)
        {
            Util.DeleteDirectory(directory);
            Directory.CreateDirectory(directory);
        }

        /// <summary>
        /// Deletes the specified directory.
        /// </summary>
        /// <param name="packageDirectory">The directory to be deleted.</param>
        public static void DeleteDirectory(string directory)
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        /// <summary>
        /// Creates a file with the specified content.
        /// </summary>
        /// <param name="directory">The directory of the created file.</param>
        /// <param name="fileName">The name of the created file.</param>
        /// <param name="fileContent">The content of the created file.</param>
        public static void CreateFile(string directory, string fileName, string fileContent)
        {
            var fileFullName = Path.Combine(directory, fileName);
            using (var writer = new StreamWriter(fileFullName))
            {
                writer.Write(fileContent);
            }
        }

        private static IPackageFile CreatePackageFile(string name)
        {
            var file = new Mock<IPackageFile>();
            file.SetupGet(f => f.Path).Returns(name);
            file.Setup(f => f.GetStream()).Returns(new MemoryStream());

            string effectivePath;
            var fx = VersionUtility.ParseFrameworkNameFromFilePath(name, out effectivePath);
            file.SetupGet(f => f.EffectivePath).Returns(effectivePath);
            file.SetupGet(f => f.TargetFramework).Returns(fx);

            return file.Object;
        }

        public static IPackageFile CreatePackageFile(string path, string content)
        {
            var file = new Mock<IPackageFile>();
            file.SetupGet(f => f.Path).Returns(path);
            file.Setup(f => f.GetStream()).Returns(new MemoryStream(Encoding.UTF8.GetBytes(content)));

            string effectivePath;
            var fx = VersionUtility.ParseFrameworkNameFromFilePath(path, out effectivePath);
            file.SetupGet(f => f.EffectivePath).Returns(effectivePath);
            file.SetupGet(f => f.TargetFramework).Returns(fx);

            return file.Object;
        }

        /// <summary>
        /// Creates a mock server that contains the specified list of packages
        /// </summary>
        public static MockServer CreateMockServer(string mockServerEndPoint, IList<IPackage> packages)
        {
            var server = new MockServer(mockServerEndPoint);

            server.Get.Add("/nuget/$metadata", r =>
                   MockServerResource.NuGetV2APIMetadata);
            server.Get.Add("/nuget/FindPackagesById()", r =>
                new Action<HttpListenerResponse>(response =>
                {
                    response.ContentType = "application/atom+xml;type=feed;charset=utf-8";
                    string feed = server.ToODataFeed(packages, "FindPackagesById");
                    MockServer.SetResponseContent(response, feed);
                }));

            foreach (var package in packages)
            {
                var url = string.Format(
                    CultureInfo.InvariantCulture,
                    "/nuget/Packages(Id='{0}',Version='{1}')",
                    package.Id,
                    package.Version);
                server.Get.Add(url, r =>
                    new Action<HttpListenerResponse>(response =>
                    {
                        response.ContentType = "application/atom+xml;type=entry;charset=utf-8";
                        var p1 = server.ToOData(package);
                        MockServer.SetResponseContent(response, p1);
                    }));

                // download url
                url = string.Format(
                    CultureInfo.InvariantCulture,
                    "/package/{0}/{1}",
                    package.Id,
                    package.Version);
                server.Get.Add(url, r =>
                    new Action<HttpListenerResponse>(response =>
                    {
                        response.ContentType = "application/zip";
                        using (var stream = package.GetStream())
                        {
                            var content = stream.ReadAllBytes();
                            MockServer.SetResponseContent(response, content);
                        }
                    }));
            }

            server.Get.Add("/nuget", r => "OK");
            return server;
        }

        public static string GetNuGetExePath()
        {
            var targetDir = ConfigurationManager.AppSettings["TargetDir"] ?? Directory.GetCurrentDirectory();
            var nugetexe = Path.Combine(targetDir, "nuget.exe");
            return nugetexe;
        }
    }
}
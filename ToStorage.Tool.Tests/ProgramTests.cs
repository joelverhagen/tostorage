﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Knapcode.Procommand;
using Knapcode.ToStorage.Core.Tests;
using Xunit;

namespace Knapcode.ToStorage.Tool.Tests
{
    public class ProgramTests
    {
        [Fact]
        public async Task StdinCanBeUploadedToDirectAndLatestWithShortOptions()
        {
            // Arrange
            using (var tc = new TestContext())
            {
                // Act
                var actual = tc.ExecuteCommand(
                    new[]
                    {
                        "-s", tc.ConnectionString,
                        "-c", tc.Container,
                        "-f", tc.PathFormat
                    },
                    tc.Content);

                // Assert
                await tc.VerifyContent(actual, direct: true, latest: true);
            }
        }

        [Fact]
        public async Task StdinCanBeUploadedToDirectAndLatestWithLongOptions()
        {
            // Arrange
            using (var tc = new TestContext())
            {
                // Act
                var actual = tc.ExecuteCommand(
                    new[]
                    {
                        "--connection-string", tc.ConnectionString,
                        "--container", tc.Container,
                        "--path-format", tc.PathFormat
                    },
                    tc.Content);

                // Assert
                await tc.VerifyContent(actual, direct: true, latest: true);
            }
        }

        [Fact]
        public async Task StdinCanBeUploadedJustToDirectWithShortOptions()
        {
            // Arrange
            using (var tc = new TestContext())
            {
                // Act
                var actual = tc.ExecuteCommand(
                    new[]
                    {
                        "-s", tc.ConnectionString,
                        "-c", tc.Container,
                        "-f", tc.PathFormat,
                        "-l", "false"
                    },
                    tc.Content);

                // Assert
                await tc.VerifyContent(actual, direct: true, latest: false);
            }
        }

        [Fact]
        public async Task StdinCanBeUploadedJustToDirectWithWithLongOptions()
        {
            // Arrange
            using (var tc = new TestContext())
            {
                // Act
                var actual = tc.ExecuteCommand(
                    new[]
                    {
                        "--connection-string", tc.ConnectionString,
                        "--container", tc.Container,
                        "--path-format", tc.PathFormat,
                        "--update-latest", "false"
                    },
                    tc.Content);

                // Assert
                await tc.VerifyContent(actual, direct: true, latest: false);
            }
        }

        [Fact]
        public async Task StdinCanBeUploadedJustToLatestWithShortOptions()
        {
            // Arrange
            using (var tc = new TestContext())
            {
                // Act
                var actual = tc.ExecuteCommand(
                    new[]
                    {
                        "-s", tc.ConnectionString,
                        "-c", tc.Container,
                        "-f", tc.PathFormat,
                        "-d", "false"
                    },
                    tc.Content);

                // Assert
                await tc.VerifyContent(actual, direct: false, latest: true);
            }
        }

        [Fact]
        public async Task StdinCanBeUploadedJustToLatestWithLongOptions()
        {
            // Arrange
            using (var tc = new TestContext())
            {
                // Act
                var actual = tc.ExecuteCommand(
                    new[]
                    {
                        "--connection-string", tc.ConnectionString,
                        "--container", tc.Container,
                        "--path-format", tc.PathFormat,
                        "--update-direct", "false"
                    },
                    tc.Content);

                // Assert
                await tc.VerifyContent(actual, direct: false, latest: true);
            }
        }

        [Fact]
        public void StdinUploadedOnlyWhenUniqueWithShortOptions()
        {
            // Arrange
            using (var tc = new TestContext())
            {
                var initial = tc.ExecuteCommand(
                    new[]
                    {
                        "-s", tc.ConnectionString,
                        "-c", tc.Container,
                        "-f", tc.PathFormat
                    },
                    tc.Content);

                // Act
                var actual = tc.ExecuteCommand(
                    new[]
                    {
                        "-s", tc.ConnectionString,
                        "-c", tc.Container,
                        "-f", tc.PathFormat,
                        "-u", "true"
                    },
                    tc.Content);

                // Assert
                tc.VerifyCommandResult(actual);
                Assert.Equal("Gettings the existing latest... exactly the same! No upload required.", actual.Output.TrimEnd());
            }
        }

        [Fact]
        public void StdinUploadedOnlyWhenUniqueWithLongOptions()
        {
            // Arrange
            using (var tc = new TestContext())
            {
                var initial = tc.ExecuteCommand(
                    new[]
                    {
                        "-s", tc.ConnectionString,
                        "-c", tc.Container,
                        "-f", tc.PathFormat
                    },
                    tc.Content);

                // Act
                var actual = tc.ExecuteCommand(
                    new[]
                    {
                        "--connection-string", tc.ConnectionString,
                        "--container", tc.Container,
                        "--path-format", tc.PathFormat,
                        "--only-unique", "true"
                    },
                    tc.Content);

                // Assert
                tc.VerifyCommandResult(actual);
                Assert.Equal("Gettings the existing latest... exactly the same! No upload required.", actual.Output.TrimEnd());
            }
        }

        [Fact]
        public async Task StdinContentTypeCanBeSetWithShortOptions()
        {
            // Arrange
            using (var tc = new TestContext())
            {
                tc.Content = "{\"foo\": 5}";
                tc.ContentType = "application/json";

                // Act
                var actual = tc.ExecuteCommand(
                    new[]
                    {
                        "-s", tc.ConnectionString,
                        "-c", tc.Container,
                        "-f", tc.PathFormat,
                        "-t", tc.ContentType
                    },
                    tc.Content);

                // Assert
                await tc.VerifyContent(actual, direct: true, latest: true);
            }
        }

        [Fact]
        public async Task StdinContentTypeCanBeSetWithLongOptions()
        {
            // Arrange
            using (var tc = new TestContext())
            {
                tc.Content = "{\"foo\": 5}";
                tc.ContentType = "application/json";

                // Act
                var actual = tc.ExecuteCommand(
                    new[]
                    {
                        "--connection-string", tc.ConnectionString,
                        "--container", tc.Container,
                        "--path-format", tc.PathFormat,
                        "--content-type", tc.ContentType
                    },
                    tc.Content);

                // Assert
                await tc.VerifyContent(actual, direct: true, latest: true);
            }
        }

        [Fact]
        public void RequiresConnectionStringOption()
        {
            // Arrange
            using (var tc = new TestContext())
            {
                // Act
                var actual = tc.ExecuteCommand(
                    new[]
                    {
                        "--container", tc.Container,
                        "--path-format", tc.PathFormat
                    },
                    tc.Content);

                // Assert
                Assert.Equal(CommandStatus.Exited, actual.Status);
                Assert.Equal(1, actual.ExitCode);
                Assert.Contains("Required option 's, connection-string' is missing.", actual.Error);
            }
        }

        [Fact]
        public void RequiresContainerOption()
        {
            // Arrange
            using (var tc = new TestContext())
            {
                // Act
                var actual = tc.ExecuteCommand(
                    new[]
                    {
                        "--connection-string", tc.ConnectionString,
                        "--path-format", tc.PathFormat
                    },
                    tc.Content);

                // Assert
                Assert.Equal(CommandStatus.Exited, actual.Status);
                Assert.Equal(1, actual.ExitCode);
                Assert.Contains("Required option 'c, container' is missing.", actual.Error);
            }
        }

        private class TestContext : IDisposable
        {
            public TestContext()
            {
                // data
                Content = "First line." + Environment.NewLine +
                          "Second line." + Environment.NewLine +
                          "Unique thing: " + Guid.NewGuid();
                ContentType = "text/plain";
                Prefix = Guid.NewGuid() + "/testpath";
                ConnectionString = TestSupport.ConnectionString;
                Container = TestSupport.Container;
                ToolPath = GetToolPath();
            }

            public CommandResult ExecuteCommand(IEnumerable<string> arguments, string stdin)
            {
                var runner = new CommandRunner();
                var command = new Command(ToolPath, arguments)
                {
                    Input = new MemoryStream(Encoding.UTF8.GetBytes(stdin))
                };

                return runner.Run(command);
            }

            private async Task<ContentAndContentType> GetContentAsync(string requestUri)
            {
                using (var httpClient = new HttpClient())
                using (var response = await httpClient.GetAsync(requestUri))
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var contentType = response.Content.Headers.ContentType.ToString();

                    return new ContentAndContentType
                    {
                        Content = content,
                        ContentType = contentType
                    };
                }
            }

            public async Task VerifyContent(CommandResult commandResult, bool direct, bool latest)
            {
                VerifyCommandResult(commandResult);

                if (direct)
                {
                    await VerifyLineUrl(commandResult, "Direct: ");
                }

                if (latest)
                {
                    await VerifyLineUrl(commandResult, "Latest: ");
                }
            }

            public void VerifyCommandResult(CommandResult commandResult)
            {
                Assert.Equal(CommandStatus.Exited, commandResult.Status);
                Assert.Equal(0, commandResult.ExitCode);
                Assert.Empty(commandResult.Error);
            }

            private async Task VerifyLineUrl(CommandResult commandResult, string linePrefix)
            {
                var directLine = commandResult.OutputLines.FirstOrDefault(l => l.StartsWith(linePrefix));
                Assert.NotNull(directLine);

                var directUrl = directLine.Split(new[] { ' ' }, 2)[1];
                var actualContent = await GetContentAsync(directUrl);

                Assert.Equal(Content, actualContent.Content);
                Assert.Equal(ContentType, actualContent.ContentType);
            }

            private static string GetToolPath()
            {
                var repositoryRoot = GetRepositoryRoot();

                return Path.Combine(
                    repositoryRoot,
                    "ToStorage.Tool",
                    "bin",
                    "Debug",
                    "ToStorage.exe");
            }

            private static string GetRepositoryRoot()
            {
                var current = Directory.GetCurrentDirectory();
                while (current != null &&
                       !Directory.GetFiles(current).Any(x => Path.GetFileName(x) == "ToStorage.sln"))
                {
                    current = Path.GetDirectoryName(current);
                }

                return current;
            }

            public string Content { get; set; }
            public string Prefix { get; }
            public string ConnectionString { get; }
            public string Container { get; }
            public string ToolPath { get; }
            public string PathFormat => $"{Prefix}/{{0}}.txt";
            public string ContentType { get; set; }

            public void Dispose()
            {
                TestSupport.DeleteBlobsWithPrefix(Prefix);
            }

            private class ContentAndContentType
            {
                public string Content { get; set; }
                public string ContentType { get; set; }
            }
        }
    }
}

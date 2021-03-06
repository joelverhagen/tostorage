﻿using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Knapcode.ToStorage.Core.Abstractions;
using Knapcode.ToStorage.Core.AzureBlobStorage;
using Microsoft.WindowsAzure.Storage;
using Moq;
using Xunit;

namespace Knapcode.ToStorage.Core.Test.AzureBlobStorage
{
    public class UniqueClientTests
    {
        [Fact]
        public async Task UniqueClient_UpdatesUniqueWithoutExisting()
        {
            // Arrange
            using (var tc = new TestContext())
            {
                // Act
                var actual = await tc.Target.UploadAsync(tc.UniqueUploadRequest);

                // Assert
                await tc.VerifyContentAsync(actual.DirectUri);
                await tc.VerifyContentAsync(actual.LatestUri);
            }
        }

        [Fact]
        public async Task UniqueClient_DoesNotOverwriteDirectTimestamp()
        {
            // Arrange
            using (var tc = new TestContext())
            {
                tc.UniqueUploadRequest.Type = UploadRequestType.Timestamp;
                tc.Content = "content";
                tc.UniqueUploadRequest.Stream = new MemoryStream(Encoding.UTF8.GetBytes(tc.Content));
                var uploadResult = await tc.Target.UploadAsync(tc.UniqueUploadRequest);
                tc.Content = "newerContent";
                tc.UniqueUploadRequest.Stream = new MemoryStream(Encoding.UTF8.GetBytes(tc.Content));

                // Act & Assert
                var exception = await Assert.ThrowsAsync<StorageException>(() => tc.Target.UploadAsync(tc.UniqueUploadRequest));
                Assert.Equal(409, exception.RequestInformation.HttpStatusCode);
                
                await tc.VerifyContentAsync(uploadResult.DirectUri, "content");
                await tc.VerifyContentAsync(uploadResult.LatestUri, "content");
            }
        }

        [Fact]
        public async Task UniqueClient_UpdatesUniqueWithExisting()
        {
            // Arrange
            using (var tc = new TestContext())
            {
                tc.UniqueUploadRequest.Stream = new MemoryStream(Encoding.UTF8.GetBytes("oldContent"));
                await tc.Target.UploadAsync(tc.UniqueUploadRequest);
                tc.UtcNow = tc.UtcNow.AddMinutes(1);
                tc.UniqueUploadRequest.Stream = new MemoryStream(Encoding.UTF8.GetBytes(tc.Content));

                // Act
                var actual = await tc.Target.UploadAsync(tc.UniqueUploadRequest);

                // Assert
                await tc.VerifyContentAsync(actual.DirectUri);
                await tc.VerifyContentAsync(actual.LatestUri);
            }
        }

        [Fact]
        public async Task UniqueClient_UsesMD5ToCompareContent()
        {
            // Arrange
            using (var tc = new TestContext())
            {
                await tc.Target.UploadAsync(tc.UniqueUploadRequest);
                tc.UniqueUploadRequest.Stream = new MemoryStream(Encoding.UTF8.GetBytes(tc.Content));

                // Act
                var actual = await tc.Target.UploadAsync(tc.UniqueUploadRequest);

                // Assert
                Assert.Null(actual);
                Assert.False(tc.EqualsAsyncCalled);
            }
        }

        [Fact]
        public async Task UniqueClient_UsesEqualsAsyncToCompareContent()
        {
            // Arrange
            using (var tc = new TestContext())
            {
                tc.Content = "a1";
                tc.UniqueUploadRequest.Stream = new MemoryStream(Encoding.UTF8.GetBytes(tc.Content));
                await tc.Target.UploadAsync(tc.UniqueUploadRequest);

                tc.Content = "a2";
                tc.UniqueUploadRequest.Stream = new MemoryStream(Encoding.UTF8.GetBytes(tc.Content));
                tc.UniqueUploadRequest.EqualsAsync = async x =>
                {
                    tc.EqualsAsyncCalled = true;
                    var content = await new StreamReader(x.Stream).ReadToEndAsync();
                    return content[0] == tc.Content[0];
                };

                // Act
                var actual = await tc.Target.UploadAsync(tc.UniqueUploadRequest);

                // Assert
                Assert.Null(actual);
                Assert.True(tc.EqualsAsyncCalled);
            }
        }

        [Fact]
        public async Task UniqueClient_AllowsNullEqualsAsync()
        {
            // Arrange
            using (var tc = new TestContext())
            {
                tc.Content = "a1";
                tc.UniqueUploadRequest.Stream = new MemoryStream(Encoding.UTF8.GetBytes(tc.Content));
                await tc.Target.UploadAsync(tc.UniqueUploadRequest);
                tc.UtcNow = tc.UtcNow.AddMinutes(1);

                tc.Content = "a2";
                tc.UniqueUploadRequest.Stream = new MemoryStream(Encoding.UTF8.GetBytes(tc.Content));
                tc.UniqueUploadRequest.EqualsAsync = null;

                // Act
                var actual = await tc.Target.UploadAsync(tc.UniqueUploadRequest);

                // Assert
                await tc.VerifyContentAsync(actual.DirectUri);
                await tc.VerifyContentAsync(actual.LatestUri);
            }
        }

        private class TestContext : IDisposable
        {
            public TestContext()
            {
                // data
                UtcNow = new DateTimeOffset(2015, 1, 2, 3, 4, 5, 6, TimeSpan.Zero);
                Content = "newContent";
                Container = TestSupport.GetTestContainer();
                EqualsAsyncCalled = false;
                UniqueUploadRequest = new UniqueUploadRequest
                {
                    ConnectionString = TestSupport.ConnectionString,
                    Container = Container,
                    ContentType = "text/plain",
                    PathFormat = "testpath/{0}.txt",
                    UploadDirect = true,
                    Stream = new MemoryStream(Encoding.UTF8.GetBytes(Content)),
                    Trace = TextWriter.Null,
                    EqualsAsync = async x =>
                    {
                        EqualsAsyncCalled = true;
                        var actualContent = await new StreamReader(x.Stream).ReadLineAsync();
                        return actualContent == Content;
                    }
                };
                GetLatestRequest = new GetLatestRequest
                {
                    ConnectionString = UniqueUploadRequest.ConnectionString,
                    Container = UniqueUploadRequest.Container,
                    PathFormat = UniqueUploadRequest.PathFormat,
                    Trace = TextWriter.Null
                };

                // dependencies
                SystemTime = new Mock<ISystemTime>();
                PathBuilder = new PathBuilder();
                Client = new Client(SystemTime.Object, PathBuilder);

                // setup
                SystemTime.Setup(x => x.UtcNow).Returns(() => UtcNow);

                // target
                Target = new UniqueClient(Client);
            }

            public PathBuilder PathBuilder { get; }

            public string Content { get; set; }

            public UniqueClient Target { get; }

            public GetLatestRequest GetLatestRequest { get; }
            
            public UniqueUploadRequest UniqueUploadRequest { get; }

            public Client Client { get; }

            public Mock<ISystemTime> SystemTime { get; }

            public DateTimeOffset UtcNow { get; set; }
            public bool EqualsAsyncCalled { get; set; }
            public string Container { get; }

            public async Task<HttpResponseMessage> GetBlobAsync(Uri uri)
            {
                using (var httpClient = new HttpClient())
                {
                    return await httpClient.GetAsync(uri);
                }
            }

            public async Task VerifyContentAsync(Uri uri, string content)
            {
                var response = await GetBlobAsync(uri);
                Assert.Equal(content, await response.Content.ReadAsStringAsync());
            }

            public async Task VerifyContentAsync(Uri uri)
            {
                var response = await GetBlobAsync(uri);
                Assert.Equal(UniqueUploadRequest.ContentType, response.Content.Headers.ContentType.ToString());
                Assert.Equal(Content, await response.Content.ReadAsStringAsync());
            }

            public void Dispose()
            {
                TestSupport.DeleteContainer(Container);
            }
        }
    }
}

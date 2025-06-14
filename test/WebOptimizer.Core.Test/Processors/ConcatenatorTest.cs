﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace WebOptimizer.Test.Processors
{
    public class ConcatenatorTest
    {
        [Fact2]
        public async Task Concatenate_MultipleSources_Success()
        {
            var processor = new Concatenator();
            var context = new Mock<IAssetContext>().SetupAllProperties();
            context.Object.Content = new Dictionary<string, byte[]> {
                { "/route1", "content".AsByteArray() },
                { "/route2", "content2".AsByteArray() }
            };

            var options = new Mock<WebOptimizerOptions>();

            await processor.ExecuteAsync(context.Object);

            Assert.Equal(1, context.Object.Content.Count);
            Assert.Equal("content\r\ncontent2\r\n", context.Object.Content.Values.First().AsString());
        }

        [Fact2]
        public async Task Concatenate_NoSources_Success()
        {
            var processor = new Concatenator();
            var context = new Mock<IAssetContext>().SetupAllProperties();
            context.Object.Content = new Dictionary<string, byte[]>();
            var options = new Mock<WebOptimizerOptions>();

            await processor.ExecuteAsync(context.Object);

            Assert.Equal(1, context.Object.Content.Count);
            Assert.Equal(string.Empty, context.Object.Content.Values.First().AsString());
        }

        [Fact2]
        public void AddConcatenate_Assets_Success()
        {
            var env = new HostingEnvironment { EnvironmentName = "Development" };
            var logger = new Mock<ILogger<Asset>>();
            var asset1 = new Asset("/file1", "text/css", new[] { "file.css" }, logger.Object);
            var asset2 = new Asset("/file2", "text/css", new[] { "file.css" }, logger.Object);
            var pipeline = new AssetPipeline();
            pipeline._assetLogger = logger.Object;
            var assets = pipeline.AddBundle(new[] { asset1, asset2 });

            assets = assets.Concatenate();

            Assert.Equal(2, assets.Count());

            foreach (var asset in assets)
            {
                Assert.Equal(1, asset.Processors.Count);
                Assert.True(asset.Processors.First() is Concatenator);
            }
        }
    }
}
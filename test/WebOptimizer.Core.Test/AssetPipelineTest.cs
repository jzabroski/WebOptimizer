﻿using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUglify.Helpers;
using Xunit;

namespace WebOptimizer.Test
{
    public class AssetPipelineTest
    {
        [Fact2]
        public void AddSingeAsset_Success()
        {
            var env = new HostingEnvironment { EnvironmentName = "Development" };
            var logger = new Mock<ILogger<Asset>>();
            var asset = new Asset("/route", "text/css", new[] { "file.css" }, logger.Object);
            var pipeline = new AssetPipeline();
            pipeline._assetLogger = logger.Object;
            pipeline.AddBundle(asset);

            Assert.Equal(1, pipeline.Assets.Count);
        }

        [Theory2]
        [InlineData("route", "route")]
        [InlineData("/route", "/route")]
        [InlineData("~/route", "/route")]
        [InlineData("~/route ", "/route")]
        [InlineData(" ~/route", "/route")]
        [InlineData(" ~/route ", "/route")]
        public void AddBundle_Success(string inputRoute, string normalizedRoute)
        {
            var logger = new Mock<ILogger<Asset>>();
            var asset = new Asset(inputRoute, "text/css", new[] { "file.css" }, logger.Object);
            var pipeline = new AssetPipeline();
            pipeline._assetLogger = logger.Object;
            pipeline.AddBundle(asset);
            if (!normalizedRoute.StartsWith("/"))
            {
                normalizedRoute = "/" + normalizedRoute;
            }
            Assert.Equal(normalizedRoute, pipeline.Assets.First().Route);
        }

        [Fact2]
        public void AddTwoAsset_Succes()
        {
            var env = new HostingEnvironment { EnvironmentName = "Development" };
            var logger = new Mock<ILogger<Asset>>();
            var asset1 = new Asset("/route1", "text/css", new[] { "file.css" }, logger.Object);
            var asset2 = new Asset("/route2", "text/css", new[] { "file.css" }, logger.Object);
            var pipeline = new AssetPipeline();
            pipeline._assetLogger = logger.Object;
            pipeline.AddBundle(new[] { asset1, asset2 });

            Assert.Equal(2, pipeline.Assets.Count);
        }

        [Fact2]
        public void AddTwoSameRoutes_Ignore()
        {
            var env = new HostingEnvironment { EnvironmentName = "Development" };
            var logger = new Mock<ILogger<Asset>>();
            var route = "/route";
            var asset1 = new Asset(route, "text/css", new[] { "file.css" }, logger.Object);
            var asset2 = new Asset(route, "text/css", new[] { "file.css" }, logger.Object);
            var pipeline = new AssetPipeline();
            pipeline._assetLogger = logger.Object;
            pipeline.AddBundle(new[] { asset1, asset2 });

            Assert.Equal(1, pipeline.Assets.Count);
        }

        [Fact2]
        public void AddBundleWithGlobRoute_Throws()
        {
            var pipeline = new AssetPipeline();
            string route = "/*.css";
            var ex = Assert.Throws<ArgumentException>(() => pipeline.AddBundle(route, "text/css", new[] { "source.css" }));

            Assert.Equal("route", ex.ParamName);
            Assert.Equal(0, pipeline.Assets.Count);
        }

        [Fact2]
        public void AddZeroSourceFilesToBundle_Fail()
        {
            var env = new HostingEnvironment { EnvironmentName = "Development" };
            var logger = new Mock<ILogger<Asset>>();
            IAsset asset = new Asset("/file.css", "text/css", Array.Empty<string>(), logger.Object);
            var pipeline = new AssetPipeline();

            Assert.Throws<ArgumentException>(() => pipeline.AddBundle(asset));
        }

        [Theory2]
        [InlineData("~/slash", "/slash")]
        [InlineData("~/slash", "slash")]
        [InlineData("~/slash", "~/slash")]
        [InlineData("/slash", "/slash")]
        [InlineData("/slash", "slash")]
        [InlineData("/slash", "~/slash")]
        [InlineData("noslash", "/noslash")]
        [InlineData("noslash", "noslash")]
        [InlineData("noslash", "~/noslash")]
        public void FromRoute_MixedSlashes_Success(string routeToAdd, string routeToCheck)
        {
            var pipeline = new AssetPipeline();
            var logger = new Mock<ILogger<Asset>>();
            pipeline._assetLogger = logger.Object;
            pipeline.AddBundle(routeToAdd, "text/css", "file.css");

            Assert.True(pipeline.TryGetAssetFromRoute(routeToCheck, out var a1), routeToCheck);
        }

        [Theory2]
        [InlineData("~/1", "/2")]
        [InlineData("~/1", "2")]
        [InlineData("~/1", "~/2")]
        [InlineData("/1", "/2")]
        [InlineData("/1", "2")]
        [InlineData("/1", "~/2")]
        [InlineData("1", "/2")]
        [InlineData("1", "2")]
        [InlineData("1", "~/2")]
        public void FromRoute_NotFound(string routeToAdd, string routeToCheck)
        {
            var pipeline = new AssetPipeline();
            var logger = new Mock<ILogger<Asset>>();
            pipeline._assetLogger = logger.Object;
            pipeline.AddBundle(routeToAdd, "text/css", "file.css");

            Assert.False(pipeline.TryGetAssetFromRoute(routeToCheck, out var a1), routeToCheck);
        }

        [Theory2]
        [InlineData("css/*.css", "/css/ost.css")]
        [InlineData("css/**/*.css", "css/a/b/c/ost.css")]
        [InlineData("**/*.css", "/css/a/b/c/ost.css")]
        [InlineData("*.css", "foo.css")]
        public void FromRoute_Globbing_Success(string pattern, string path)
        {
            var pipeline = new AssetPipeline();
            var logger = new Mock<ILogger<Asset>>();
            pipeline._assetLogger = logger.Object;
            pipeline.AddFiles("text/css", pattern);
            if (!path.StartsWith("/"))
            {
                path = "/" + path;
            }
            Assert.True(pipeline.TryGetAssetFromRoute(path, out var a1));
            Assert.Equal($"{path}", a1.Route);
        }

        [Theory2]
        [InlineData("scripts/*.css", "/scripts/ost.css")]
        [InlineData("scripts/**/*.css", "/scripts/a/b/c/ost.css")]
        [InlineData("**/*.css", "/scripts/a/b/c/ost.css")]
        [InlineData("*.css", "/foo.css")]
        public void FromRoute_Globbing_WithItems_Success(string pattern, string path)
        {
            var pipeline = new AssetPipeline();
            var logger = new Mock<ILogger<Asset>>();
            pipeline._assetLogger = logger.Object;
            pipeline.AddFiles("text/css", pattern).ForEach(x => x.UseContentRoot());

            Assert.True(pipeline.TryGetAssetFromRoute(path, out var a1));
            Assert.Equal($"/{path.TrimStart('/')}", a1.Route);
            Assert.Equal(1, a1.Items.Count);
            Assert.Contains(a1.Items, p => p.Key == "usecontentroot");
        }

        [Theory2]
        [InlineData("scripts/*.css", "/scripts/ost.css")]
        [InlineData("scripts/**/*.css", "/scripts/a/b/c/ost.css")]
        [InlineData("**/*.css", "/scripts/a/b/c/ost.css")]
        [InlineData("*.css", "/foo.css")]
        public void FromRoute_Globbing_WithProcessors_Success(string pattern, string path)
        {
            var pipeline = new AssetPipeline();
            var logger = new Mock<ILogger<Asset>>();
            pipeline._assetLogger = logger.Object;
            pipeline.AddFiles("text/css", pattern).MinifyCss();

            Assert.True(pipeline.TryGetAssetFromRoute(path, out var a1));
            Assert.Equal(1, a1.Processors.Count);
        }

        [Fact2]
        public void TryGetAssetFromRoute_Concurrency()
        {
            var pipeline = new AssetPipeline();
            pipeline._assets = new ConcurrentDictionary<string, IAsset>();
            var logger = new Mock<ILogger<Asset>>();
            pipeline._assetLogger = logger.Object;

            pipeline._assets.TryAdd("/**/*.less", new Asset("/**/*.less", "text/css; charset=UFT-8", new [] { "**/*.less" }, logger.Object));
            pipeline._assets.TryAdd("/**/*.css", new Asset("/**/*.css", "text/css; charset=UFT-8", new [] { "**/*.css" }, logger.Object));

            Parallel.For(0, 100, iteration =>
            {
                pipeline.TryGetAssetFromRoute($"/some_file{iteration}.less", out var a1);
            });
        }

        [Fact2]
        public void FromRoute_Null_Success()
        {
            var pipeline = new AssetPipeline();

            Assert.False(pipeline.TryGetAssetFromRoute(null, out var a1));
            Assert.Null(a1);
        }

        [Fact2]
        public void AddFilesNoContentType_Throws()
        {
            var pipeline = new AssetPipeline();
            string ct = null;
            var ex = Assert.Throws<ArgumentException>(() => pipeline.AddFiles(ct, new[] { "file.css" }));

            Assert.Equal("contentType", ex.ParamName);
            Assert.Equal(0, pipeline.Assets.Count);
        }

        [Fact2]
        public void AddFilesNoSourceFiles_Throws()
        {
            var pipeline = new AssetPipeline();
            var sourceFiles = new string[0];
            var ex = Assert.Throws<ArgumentException>(() => pipeline.AddFiles("text/css", sourceFiles));

            Assert.Equal("sourceFiles", ex.ParamName);
            Assert.Equal(0, pipeline.Assets.Count);
        }
    }
}
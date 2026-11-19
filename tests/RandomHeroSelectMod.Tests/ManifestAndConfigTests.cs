using System;
using System.IO;
using Newtonsoft.Json.Linq;
using Xunit;

namespace RandomHeroSelectMod.Tests
{
    /// <summary>
    /// 校验随包元数据：sidecar 与 config.json 必须一致，且配置项都有四语言说明。
    /// </summary>
    public sealed class ManifestAndConfigTests
    {
        private static string RepoRoot
        {
            get
            {
                // tests/RandomHeroSelectMod.Tests/bin/<config>/net8.0 → 上溯 5 级到仓库根
                return Path.GetFullPath(Path.Combine(
                    AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
            }
        }

        private static JObject LoadJson(string relativePath)
        {
            string path = Path.Combine(RepoRoot, relativePath);
            Assert.True(File.Exists(path), "缺少文件: " + path);
            return JObject.Parse(File.ReadAllText(path));
        }

        [Fact]
        public void Sidecar_MatchesManifest()
        {
            var sidecar = LoadJson("RandomHeroSelectMod.json");

            Assert.Equal("RandomHeroSelectMod", (string)sidecar["id"]);
            Assert.Equal("随机选人", (string)sidecar["name"]);
            Assert.Equal("hpenguins", (string)sidecar["author"]);
            Assert.False(string.IsNullOrWhiteSpace((string)sidecar["version"]));
            Assert.Equal(75, (int)sidecar["permissions"]);

            var platforms = (JArray)sidecar["platforms"];
            Assert.Equal(2, platforms.Count);
            Assert.Contains("windows", platforms);
            Assert.Contains("android", platforms);
        }

        [Fact]
        public void ConfigJson_HasEveryOption()
        {
            var config = LoadJson("config.json");

            string[] expected =
            {
                "enabled", "pveLevel", "requireOwned", "shuffleMinClicks", "shuffleMaxClicks",
                "clickIntervalMs", "delayMilliseconds", "confirmDelayMilliseconds",
                "rerollOnTaken", "maxRetry", "autoConfirmSkin", "triggerKey", "rerollKey", "randomSeed",
                "buttonAnchor", "buttonOffsetX", "buttonOffsetY"
            };

            foreach (var key in expected)
            {
                Assert.True(config.ContainsKey(key), "config.json 缺少 " + key);
            }
        }

        [Fact]
        public void ConfigJson_HasFourLanguageCommentsForEveryOption()
        {
            var config = LoadJson("config.json");
            var comments = config["_comments"] as JObject;
            Assert.NotNull(comments);

            string[] keys =
            {
                "enabled", "pveLevel", "requireOwned", "shuffleMinClicks", "shuffleMaxClicks",
                "clickIntervalMs", "delayMilliseconds", "confirmDelayMilliseconds",
                "rerollOnTaken", "maxRetry", "autoConfirmSkin", "triggerKey", "rerollKey", "randomSeed",
                "buttonAnchor", "buttonOffsetX", "buttonOffsetY"
            };
            string[] languages = { "zh-Hans", "zh-Hant", "en", "ja" };

            foreach (var key in keys)
            {
                Assert.True(comments.ContainsKey(key), "_comments 缺少 " + key);
                foreach (var language in languages)
                {
                    string text = comments[key]?[language]?.ToString();
                    Assert.False(string.IsNullOrWhiteSpace(text),
                        "_comments." + key + "." + language + " 为空");
                }
            }
        }

        [Fact]
        public void HotkeyOptions_EndWithKeySuffix()
        {
            // Toys 只按这个约定识别键位字段并显示捕获按键控件。
            var config = LoadJson("config.json");
            Assert.True(config.ContainsKey("triggerKey"));
            Assert.True(config.ContainsKey("rerollKey"));
            Assert.Equal(string.Empty, (string)config["triggerKey"]);
            Assert.Equal(string.Empty, (string)config["rerollKey"]);
        }

        [Fact]
        public void ConfigDefaults_AreWithinDocumentedRanges()
        {
            var config = LoadJson("config.json");

            Assert.True((bool)config["enabled"]);
            Assert.Equal(0, (int)config["pveLevel"]);
            Assert.Equal(22, (int)config["shuffleMinClicks"]);
            Assert.Equal(34, (int)config["shuffleMaxClicks"]);
            Assert.InRange((int)config["shuffleMinClicks"], 1, 99);
            Assert.InRange((int)config["shuffleMaxClicks"], 1, 99);
            Assert.InRange((int)config["clickIntervalMs"], 0, 2000);
            Assert.InRange((int)config["delayMilliseconds"], 0, 10000);
            Assert.InRange((int)config["confirmDelayMilliseconds"], 0, 10000);
            Assert.Equal(0, (int)config["randomSeed"]);
            Assert.True((int)config["shuffleMaxClicks"] >= (int)config["shuffleMinClicks"]);
            Assert.Equal("right", (string)config["buttonAnchor"]);
            Assert.Equal(0, (int)config["buttonOffsetX"]);
            Assert.Equal(0, (int)config["buttonOffsetY"]);
        }
    }
}

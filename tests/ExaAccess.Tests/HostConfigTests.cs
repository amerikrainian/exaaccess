using System;
using System.IO;
using Xunit;

namespace ExaAccess.Tests
{
    public class HostConfigTests : IDisposable
    {
        private readonly string _originalPath;
        private readonly string _tempFile;

        public HostConfigTests()
        {
            _originalPath = HostConfig.SettingsPath;
            _tempFile = Path.Combine(Path.GetTempPath(), "ExaAccessCfg-" + Guid.NewGuid().ToString("N") + ".json");
        }

        public void Dispose()
        {
            HostConfig.SettingsPath = _originalPath;
            HostConfig.ResetForTests();
            try { File.Delete(_tempFile); } catch { }
        }

        [Fact]
        public void MissingFileYieldsDefaults()
        {
            HostConfig.SettingsPath = _tempFile; // never written
            HostConfig.ResetForTests();
            Assert.Equal("auto", HostConfig.Get("speech.output", "auto"));
        }

        [Fact]
        public void ValuesComeBackByDottedKey()
        {
            File.WriteAllText(_tempFile, "{ \"speech.output\": \"sapi\", \"some.number\": 3 }");
            HostConfig.SettingsPath = _tempFile;
            HostConfig.ResetForTests();
            Assert.Equal("sapi", HostConfig.Get("speech.output", "auto"));
            Assert.Equal("3", HostConfig.Get("some.number", "0"));
        }

        [Fact]
        public void BrokenJsonYieldsDefaults()
        {
            File.WriteAllText(_tempFile, "{ nope");
            HostConfig.SettingsPath = _tempFile;
            HostConfig.ResetForTests();
            Assert.Equal("auto", HostConfig.Get("speech.output", "auto"));
        }
    }
}

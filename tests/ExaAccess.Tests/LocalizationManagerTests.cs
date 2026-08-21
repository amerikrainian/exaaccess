using System;
using System.IO;
using ExaAccess.Localization;
using Xunit;

namespace ExaAccess.Tests
{
    public class LocalizationManagerTests : IDisposable
    {
        private readonly string _root;

        public LocalizationManagerTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "ExaAccessLocTests-" + Guid.NewGuid().ToString("N"));
            Write("enGB", "ui.json", "{ \"a\": \"A-en\", \"only.en\": \"fallback works\" }");
            Write("enGB", "extra.json", "{ \"e\": \"E-en\" }");
            Write("deDE", "ui.json", "{ \"a\": \"A-de\" }");
            LocalizationManager.LanguageSource = null;
        }

        public void Dispose()
        {
            LocalizationManager.LanguageSource = null;
            LocalizationManager.Initialize(_root); // leave a sane state, then drop the files
            try { Directory.Delete(_root, true); } catch { }
        }

        private void Write(string lang, string file, string json)
        {
            string dir = Path.Combine(_root, lang);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, file), json);
        }

        [Fact]
        public void FallbackLanguageServesItsTables()
        {
            LocalizationManager.Initialize(_root);
            Assert.Equal("A-en", LocalizationManager.Get("ui", "a"));
            Assert.Equal("E-en", LocalizationManager.Get("extra", "e"));
        }

        [Fact]
        public void CurrentLanguageWinsAndMissingKeysFallBack()
        {
            LocalizationManager.LanguageSource = () => "deDE";
            LocalizationManager.Initialize(_root);
            Assert.Equal("A-de", LocalizationManager.Get("ui", "a"));
            Assert.Equal("fallback works", LocalizationManager.Get("ui", "only.en"));
        }

        [Fact]
        public void MissingEverywhereIsNullAndGetOrDefaultIsQuiet()
        {
            LocalizationManager.Initialize(_root);
            Assert.Null(LocalizationManager.Get("ui", "nope"));
            Assert.Equal("dflt", LocalizationManager.GetOrDefault("ui", "nope", "dflt"));
        }

        [Fact]
        public void TickSwapsLanguagesLive()
        {
            string lang = "enGB";
            LocalizationManager.LanguageSource = () => lang;
            LocalizationManager.Initialize(_root);
            Assert.Equal("A-en", LocalizationManager.Get("ui", "a"));

            lang = "deDE";
            LocalizationManager.Tick();
            Assert.Equal("A-de", LocalizationManager.Get("ui", "a"));
        }

        [Fact]
        public void UnknownLanguageFolderJustReadsFallback()
        {
            LocalizationManager.LanguageSource = () => "frFR";
            LocalizationManager.Initialize(_root);
            Assert.Equal("A-en", LocalizationManager.Get("ui", "a"));
        }

        [Fact]
        public void ABrokenFileDoesNotSinkTheOthers()
        {
            Write("enGB", "broken.json", "{ not json !!");
            LocalizationManager.Initialize(_root);
            Assert.Equal("A-en", LocalizationManager.Get("ui", "a"));
            Assert.Null(LocalizationManager.Get("broken", "x"));
        }

        [Fact]
        public void InitializeInstallsTheMessageResolver()
        {
            LocalizationManager.Initialize(_root);
            Assert.Equal("A-en", Message.Localized("ui", "a").Resolve());
        }
    }
}
